// =====================================================================
// Edge Function "admin-accounts" - gestione degli account condivisi
// =====================================================================
// Crea gli utenti di Supabase Auth, cambia la password ed elimina gli
// account. Serve la chiave service_role, che NON deve mai stare nel client:
// per questo queste operazioni passano da qui. Rinomina, attivo/disattivo,
// superadmin e ordine si fanno invece direttamente sulla tabella
// app_account (la RLS le consente solo al superadmin).
//
// Pubblicazione (dalla cartella del repository, con la Supabase CLI):
//   supabase login
//   supabase link --project-ref gazljtuofxyrrljwgixw
//   supabase functions deploy admin-accounts
//
// Variabili d'ambiente:
//   SUPABASE_URL, SUPABASE_ANON_KEY, SUPABASE_SERVICE_ROLE_KEY
//     -> fornite automaticamente da Supabase, non vanno impostate.
//   ACCOUNT_EMAIL_DOMAIN (facoltativa) -> dominio delle email "finte" degli
//     account condivisi, default "accounts.portale-ac.it":
//       supabase secrets set ACCOUNT_EMAIL_DOMAIN=accounts.miaparrocchia.it
//     Le email non ricevono posta: servono solo come identificativo per il
//     login (l'utente sceglie il nome dell'account dalla tendina).
//
// Chiamata: POST con header Authorization: Bearer <token utente> e corpo JSON
//   { "action": "create", "nome": "...", "password": "...", "is_superadmin"?: bool, "ordine"?: int }
//       -> 201 { "id": "<uuid>", "email": "..." }
//   { "action": "set_password", "account_id": "<uuid>", "password": "..." }
//       -> 200 { "ok": true }
//   { "action": "delete", "account_id": "<uuid>" }
//       -> 200 { "ok": true }
// Errori: { "error": "messaggio in italiano" } con il codice HTTP adatto.
// =====================================================================

import { createClient, type SupabaseClient } from "jsr:@supabase/supabase-js@2";

const PASSWORD_MIN = 8;
const PASSWORD_MAX = 72;
const NOME_MAX = 60;

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

class HttpError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { ...corsHeaders, "Content-Type": "application/json; charset=utf-8" },
  });
}

function env(nome: string): string {
  const valore = Deno.env.get(nome);
  if (!valore) throw new HttpError(500, `Configurazione mancante: ${nome}`);
  return valore;
}

// ------------------------------------------------------------------ validazioni

function validaPassword(password: unknown): string {
  if (typeof password !== "string" || password.length < PASSWORD_MIN) {
    throw new HttpError(400, `La password deve avere almeno ${PASSWORD_MIN} caratteri.`);
  }
  if (password.length > PASSWORD_MAX) {
    throw new HttpError(400, `La password può avere al massimo ${PASSWORD_MAX} caratteri.`);
  }
  if (password.trim() !== password) {
    throw new HttpError(400, "La password non può iniziare o finire con uno spazio.");
  }
  return password;
}

function validaNome(nome: unknown): string {
  const n = typeof nome === "string" ? nome.trim() : "";
  if (!n) throw new HttpError(400, "Il nome dell'account è obbligatorio.");
  if (n.length > NOME_MAX) throw new HttpError(400, `Il nome può avere al massimo ${NOME_MAX} caratteri.`);
  return n;
}

function validaId(id: unknown): string {
  const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  if (typeof id !== "string" || !uuid.test(id)) throw new HttpError(400, "Account non valido.");
  return id;
}

// "Animatori Campo è" -> "animatori-campo-e"
function slug(nome: string): string {
  const s = nome
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 30)
    .replace(/-+$/g, "");
  return s || "account";
}

function suffissoCasuale(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(3));
  return Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("");
}

// ------------------------------------------------------------------ verifica chiamante

async function verificaSuperadmin(req: Request, admin: SupabaseClient): Promise<string> {
  const authHeader = req.headers.get("Authorization");
  if (!authHeader?.startsWith("Bearer ")) throw new HttpError(401, "Accesso richiesto.");

  const utente = createClient(env("SUPABASE_URL"), env("SUPABASE_ANON_KEY"), {
    global: { headers: { Authorization: authHeader } },
    auth: { persistSession: false, autoRefreshToken: false },
  });
  const { data, error } = await utente.auth.getUser();
  if (error || !data?.user) throw new HttpError(401, "Sessione non valida o scaduta: esci e rientra.");

  const { data: account, error: errAccount } = await admin
    .from("app_account")
    .select("is_superadmin, attivo")
    .eq("id", data.user.id)
    .maybeSingle();
  if (errAccount) throw new HttpError(500, `Verifica dei permessi non riuscita: ${errAccount.message}`);
  if (!account?.is_superadmin || !account.attivo) {
    throw new HttpError(403, "Operazione riservata ai super amministratori.");
  }
  return data.user.id;
}

async function verificaAccountEsiste(admin: SupabaseClient, id: string): Promise<void> {
  const { data, error } = await admin.from("app_account").select("id").eq("id", id).maybeSingle();
  if (error) throw new HttpError(500, error.message);
  if (!data) throw new HttpError(404, "Account non trovato.");
}

// ------------------------------------------------------------------ azioni

async function crea(admin: SupabaseClient, body: Record<string, unknown>): Promise<Response> {
  const nome = validaNome(body.nome);
  const password = validaPassword(body.password);
  const isSuperadmin = body.is_superadmin === true;

  const { data: omonimi, error: errNome } = await admin.from("app_account").select("id").ilike("nome", nome.replace(/[\\%_]/g, "\\$&"));
  if (errNome) throw new HttpError(500, errNome.message);
  if (omonimi && omonimi.length > 0) throw new HttpError(409, "Esiste già un account con questo nome.");

  let ordine = typeof body.ordine === "number" && Number.isInteger(body.ordine) ? body.ordine : null;
  if (ordine === null) {
    const { data: ultimo } = await admin
      .from("app_account")
      .select("ordine")
      .order("ordine", { ascending: false })
      .limit(1)
      .maybeSingle();
    ordine = (ultimo?.ordine ?? 0) + 1;
  }

  const dominio = Deno.env.get("ACCOUNT_EMAIL_DOMAIN") ?? "accounts.portale-ac.it";
  const email = `${slug(nome)}-${suffissoCasuale()}@${dominio}`;

  const { data: creato, error: errCrea } = await admin.auth.admin.createUser({
    email,
    password,
    email_confirm: true,
    user_metadata: { nome },
  });
  if (errCrea || !creato?.user) {
    throw new HttpError(400, `Creazione dell'utente non riuscita: ${errCrea?.message ?? "errore sconosciuto"}`);
  }

  const { error: errInsert } = await admin.from("app_account").insert({
    id: creato.user.id,
    nome,
    is_superadmin: isSuperadmin,
    attivo: true,
    ordine,
  });
  if (errInsert) {
    // Niente utenti orfani: se la riga non entra, l'utente auth va tolto
    await admin.auth.admin.deleteUser(creato.user.id);
    if (errInsert.code === "23505") throw new HttpError(409, "Esiste già un account con questo nome.");
    throw new HttpError(500, `Salvataggio dell'account non riuscito: ${errInsert.message}`);
  }

  return json({ id: creato.user.id, email }, 201);
}

async function impostaPassword(admin: SupabaseClient, body: Record<string, unknown>): Promise<Response> {
  const id = validaId(body.account_id);
  const password = validaPassword(body.password);
  await verificaAccountEsiste(admin, id);

  const { error } = await admin.auth.admin.updateUserById(id, { password });
  if (error) throw new HttpError(400, `Cambio password non riuscito: ${error.message}`);
  return json({ ok: true });
}

async function elimina(admin: SupabaseClient, body: Record<string, unknown>, chiamanteId: string): Promise<Response> {
  const id = validaId(body.account_id);
  if (id === chiamanteId) throw new HttpError(400, "Non puoi eliminare l'account con cui sei collegato.");
  await verificaAccountEsiste(admin, id);

  // app_account e account_evento si cancellano a cascata
  const { error } = await admin.auth.admin.deleteUser(id);
  if (error) throw new HttpError(400, `Eliminazione non riuscita: ${error.message}`);
  return json({ ok: true });
}

// ------------------------------------------------------------------ entry point

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") return new Response("ok", { headers: corsHeaders });
  if (req.method !== "POST") return json({ error: "Metodo non consentito." }, 405);

  try {
    const admin = createClient(env("SUPABASE_URL"), env("SUPABASE_SERVICE_ROLE_KEY"), {
      auth: { persistSession: false, autoRefreshToken: false },
    });
    const chiamanteId = await verificaSuperadmin(req, admin);

    let body: Record<string, unknown>;
    try {
      body = await req.json();
    } catch {
      throw new HttpError(400, "Richiesta non valida: serve un corpo JSON.");
    }
    if (!body || typeof body !== "object") throw new HttpError(400, "Richiesta non valida.");

    switch (body.action) {
      case "create":
        return await crea(admin, body);
      case "set_password":
        return await impostaPassword(admin, body);
      case "delete":
        return await elimina(admin, body, chiamanteId);
      default:
        throw new HttpError(400, `Azione sconosciuta: ${String(body.action)}`);
    }
  } catch (e) {
    if (e instanceof HttpError) return json({ error: e.message }, e.status);
    console.error(e);
    return json({ error: `Errore interno: ${e instanceof Error ? e.message : String(e)}` }, 500);
  }
});
