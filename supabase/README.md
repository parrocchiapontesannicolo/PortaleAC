# Database Supabase – Portale AC v2

Lo schema del database adesso è versionato qui, in `supabase/migrations/`.
Ogni modifica al database va in un nuovo file `AAAAMMGGhhmmss_descrizione.sql`.

## Cosa cambia con la v2

| Prima | Dopo |
|---|---|
| Ruoli fissi nel codice (Responsabile/Animatore → 2 email cablate) | Tabella `app_account`: account condivisi gestiti dall'app |
| Evento scelto prima del login | Evento scelto dopo il login, cambiabile dal menu in alto |
| Isolamento tra eventi solo nel client (`GetAll<T>()`) | **RLS sul database**: ogni riga è visibile solo a chi ha i permessi su quell'evento |
| Permessi = ruolo cablato | `ruolo` = insieme di permessi; `account_evento` = quale ruolo ha un account in quale evento |
| L'utente anonimo poteva leggere la tabella `evento` | L'anonimo può solo chiamare `login_accounts()` |

Permessi disponibili (tenere allineati `public.tutti_permessi()` e `Core/Security/Permessi.cs`):

| Chiave | Cosa abilita |
|---|---|
| `iscritti.view` | Lista iscritti e dettaglio in sola lettura |
| `iscritti.edit` | Nuova iscrizione e modifica (include la scheda sanitaria) |
| `iscritti.delete` | Eliminare un iscritto |
| `iscritti.pagamenti` | Segnare pagato (RPC `set_pagato`) |
| `iscritti.sanitario` | Vedere la scheda sanitaria |
| `squadre.view` / `squadre.edit` | Vedere / gestire le squadre |
| `google.sheet` / `google.drive` | Link al foglio Google / cartella Drive dell'evento |

Un account **super amministratore** ha tutti i permessi su tutti gli eventi e
vede il menu *Amministrazione*.

## Come applicare la migrazione

> ⚠️ La migrazione **sostituisce tutte le policy RLS esistenti** e rende le RPC
> `create_iscrizione`, `update_iscrizione`, `delete_iscrizione` *security invoker*.
> Fai prima un backup o provala su un branch di Supabase.

1. **Backup**: Dashboard → *Database* → *Backups*, oppure
   `supabase db dump -f backup_prima_v2.sql --data-only` (e `--schema-only` per lo schema).
2. **Salva lo schema attuale nel repository** (consigliato, così le RPC esistenti
   sono finalmente versionate):
   ```bash
   supabase login
   supabase link --project-ref gazljtuofxyrrljwgixw
   supabase db dump --schema public -f supabase/migrations/20261010000000_baseline.sql
   ```
   Poi segna la baseline come già applicata:
   `supabase migration repair --status applied 20261010000000`
3. **Applica la v2**:
   `supabase db push`
   (in alternativa: copia il contenuto di
   `migrations/20261010120000_v2_account_permessi.sql` nel *SQL Editor* della
   Dashboard ed eseguilo.)
4. **Verifica** con l'account Responsabile: login, scelta evento, lista iscritti,
   nuova iscrizione, modifica, squadre. Poi con l'account Animatore: deve vedere
   solo squadre e documenti.

### Account esistenti

La migrazione importa i due utenti già presenti in *Authentication*:

- `Responsabile` → super amministratore, ruolo *Responsabile* su tutti gli eventi
- `Animatore` → ruolo *Animatore* su tutti gli eventi

Le password restano quelle attuali. Dalla pagina *Amministrazione → Account*
puoi crearne altri (es. "Animatori Campo 2026") e scegliere per ogni evento il
ruolo, oppure nessun accesso.

Consiglio: crea un account amministratore dedicato e togli il flag super
amministratore all'account `Responsabile` condiviso.

### Se le RPC di iscrizione danno "permission denied" / "violates row-level security"

Le RPC adesso girano con i permessi di chi le chiama. Se una RPC scrive in una
tabella non elencata nella migrazione, aggiungi le policy per quella tabella
seguendo lo stesso schema (`has_perm(event_id, 'iscritti.edit')`).

## Edge Function `admin-accounts`

Serve alla pagina *Amministrazione → Account* per creare utenti, cambiare
password ed eliminarli (operazioni che richiedono la service key, che non
deve mai stare nel client).

```bash
supabase functions deploy admin-accounts
# opzionale: dominio delle email fittizie degli account condivisi
supabase secrets set ACCOUNT_EMAIL_DOMAIN=accounts.portale-ac.it
```

In *Authentication → Providers → Email* conviene disattivare
"Allow new users to sign up": gli account li crea solo l'amministratore.
