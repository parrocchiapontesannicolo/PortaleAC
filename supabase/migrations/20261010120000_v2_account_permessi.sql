-- =====================================================================
-- Portale AC v2 - Account condivisi, permessi per evento, RLS lato server
-- =====================================================================
-- Cosa fa questa migrazione:
--   1. Crea le tabelle app_account, ruolo, account_evento.
--   2. Crea le funzioni di supporto per i permessi (has_perm, ...).
--   3. Abilita la Row Level Security su tutte le tabelle per evento e
--      SOSTITUISCE tutte le policy esistenti con policy basate sui permessi.
--   4. Rende le RPC esistenti (create/update/delete_iscrizione) SECURITY
--      INVOKER, così rispettano la RLS.
--   5. Toglie all'utente anonimo l'accesso diretto alle tabelle.
--   6. Importa i due account attuali (Responsabile, Animatore).
--
-- PRIMA DI APPLICARLA: fai un backup (Dashboard > Database > Backups) o
-- provala su un branch di Supabase. Vedi supabase/README.md.
--
-- IMPORTANTE: l'elenco dei permessi deve restare allineato con
-- IscrizioneManager.Core/Security/Permessi.cs
-- =====================================================================

begin;

-- ---------------------------------------------------------------------
-- 1. Tabelle
-- ---------------------------------------------------------------------

create table if not exists public.ruolo (
  id          serial primary key,
  nome        text not null unique,
  descrizione text,
  permessi    text[] not null default '{}'
);

create table if not exists public.app_account (
  id             uuid primary key references auth.users (id) on delete cascade,
  nome           text not null unique,
  is_superadmin  boolean not null default false,
  attivo         boolean not null default true,
  ordine         integer not null default 0,
  created_at     timestamptz not null default now()
);

create table if not exists public.account_evento (
  account_id uuid    not null references public.app_account (id) on delete cascade,
  event_id   integer not null references public.evento (id_evento) on delete cascade,
  ruolo_id   integer not null references public.ruolo (id),
  primary key (account_id, event_id)
);

create index if not exists account_evento_event_idx on public.account_evento (event_id);

-- ---------------------------------------------------------------------
-- 2. Funzioni di supporto
-- ---------------------------------------------------------------------

-- Elenco completo dei permessi (tenere allineato con Permessi.cs)
create or replace function public.tutti_permessi()
returns text[]
language sql
immutable
as $$
  select array[
    'iscritti.view',
    'iscritti.edit',
    'iscritti.delete',
    'iscritti.pagamenti',
    'iscritti.sanitario',
    'squadre.view',
    'squadre.edit',
    'google.sheet',
    'google.drive'
  ]::text[]
$$;

create or replace function public.is_superadmin()
returns boolean
language sql
stable
security definer
set search_path = public
as $$
  select coalesce(
    (select a.is_superadmin from public.app_account a
      where a.id = auth.uid() and a.attivo),
    false)
$$;

-- Permessi dell'utente corrente su un evento (il superadmin li ha tutti)
create or replace function public.permessi_evento(p_event_id integer)
returns text[]
language sql
stable
security definer
set search_path = public
as $$
  select case
    when public.is_superadmin() then public.tutti_permessi()
    else coalesce(
      (select r.permessi
         from public.account_evento ae
         join public.ruolo r       on r.id = ae.ruolo_id
         join public.app_account a on a.id = ae.account_id
        where ae.account_id = auth.uid()
          and ae.event_id = p_event_id
          and a.attivo),
      '{}'::text[])
  end
$$;

create or replace function public.has_perm(p_event_id integer, p_perm text)
returns boolean
language sql
stable
security definer
set search_path = public
as $$
  select p_perm = any (public.permessi_evento(p_event_id))
$$;

create or replace function public.has_any_perm(p_event_id integer, p_perms text[])
returns boolean
language sql
stable
security definer
set search_path = public
as $$
  select public.permessi_evento(p_event_id) && p_perms
$$;

create or replace function public.can_access_event(p_event_id integer)
returns boolean
language sql
stable
security definer
set search_path = public
as $$
  select cardinality(public.permessi_evento(p_event_id)) > 0
$$;

-- ---------------------------------------------------------------------
-- 3. RPC per l'applicazione
-- ---------------------------------------------------------------------

-- Elenco account mostrato nella pagina di login (unica funzione per anon)
create or replace function public.login_accounts()
returns table (id uuid, nome text, email text)
language sql
stable
security definer
set search_path = public, auth
as $$
  select a.id, a.nome, u.email::text
    from public.app_account a
    join auth.users u on u.id = a.id
   where a.attivo
   order by a.ordine, a.nome
$$;

-- Eventi accessibili all'utente corrente, con ruolo e permessi
create or replace function public.my_events()
returns table (event_id integer, nome text, isactive integer, ruolo text, permessi text[])
language sql
stable
security definer
set search_path = public
as $$
  select e.id_evento, e.nome, e.isactive,
         case when public.is_superadmin() then coalesce(r.nome, 'Super admin') else r.nome end,
         public.permessi_evento(e.id_evento)
    from public.evento e
    left join public.account_evento ae
           on ae.event_id = e.id_evento and ae.account_id = auth.uid()
    left join public.ruolo r on r.id = ae.ruolo_id
   where public.can_access_event(e.id_evento)
   order by e.isactive desc, e.id_evento desc
$$;

-- Account corrente
create or replace function public.my_account()
returns table (id uuid, nome text, is_superadmin boolean)
language sql
stable
security definer
set search_path = public
as $$
  select a.id, a.nome, a.is_superadmin
    from public.app_account a
   where a.id = auth.uid() and a.attivo
$$;

-- Aggiorna solo il flag "pagato": basta il permesso iscritti.pagamenti
create or replace function public.set_pagato(p_id_iscrizione integer, p_pagato boolean)
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
  v_event_id integer;
begin
  select i.event_id into v_event_id from public.iscrizione i where i.id_iscrizione = p_id_iscrizione;
  if v_event_id is null then
    raise exception 'Iscrizione non trovata';
  end if;
  if not public.has_any_perm(v_event_id, array['iscritti.pagamenti', 'iscritti.edit']) then
    raise exception 'Permesso negato' using errcode = '42501';
  end if;
  update public.iscrizione set pagato = p_pagato where id_iscrizione = p_id_iscrizione;
end
$$;

-- Ridistribuzione squadre in un'unica transazione: o tutto o niente.
-- p_assegnazioni = [{"id_squadra": 1, "id_bambino": 10}, ...]
create or replace function public.salva_squadre(p_event_id integer, p_assegnazioni jsonb)
returns void
language plpgsql
security definer
set search_path = public
as $$
begin
  if not public.has_perm(p_event_id, 'squadre.edit') then
    raise exception 'Permesso negato' using errcode = '42501';
  end if;

  delete from public.squadra_bambino where event_id = p_event_id;

  insert into public.squadra_bambino (id_squadra, id_bambino, event_id)
  select distinct on ((x ->> 'id_bambino')::int)
         (x ->> 'id_squadra')::int, (x ->> 'id_bambino')::int, p_event_id
    from jsonb_array_elements(coalesce(p_assegnazioni, '[]'::jsonb)) x
    join public.bambino b on b.id_bambino = (x ->> 'id_bambino')::int and b.event_id = p_event_id
    join public.squadra s on s.id_squadra = (x ->> 'id_squadra')::int and s.event_id = p_event_id;
end
$$;

-- Sposta un bambino in una squadra (p_id_squadra 0/null = senza squadra)
create or replace function public.sposta_bambino(p_event_id integer, p_id_bambino integer, p_id_squadra integer)
returns void
language plpgsql
security definer
set search_path = public
as $$
begin
  if not public.has_perm(p_event_id, 'squadre.edit') then
    raise exception 'Permesso negato' using errcode = '42501';
  end if;
  if not exists (select 1 from public.bambino where id_bambino = p_id_bambino and event_id = p_event_id) then
    raise exception 'Bambino non trovato in questo evento';
  end if;

  delete from public.squadra_bambino where event_id = p_event_id and id_bambino = p_id_bambino;

  if coalesce(p_id_squadra, 0) <> 0 then
    if not exists (select 1 from public.squadra where id_squadra = p_id_squadra and event_id = p_event_id) then
      raise exception 'Squadra non trovata in questo evento';
    end if;
    insert into public.squadra_bambino (id_squadra, id_bambino, event_id)
    values (p_id_squadra, p_id_bambino, p_event_id);
  end if;
end
$$;

-- Dati minimi dei bambini per la pagina squadre: chi vede solo le squadre
-- non deve poter leggere indirizzi, note, contatti dalle tabelle complete.
create or replace function public.squadre_bambini(p_event_id integer)
returns table (id_bambino integer, nome text, cognome text, anno integer)
language sql
stable
security definer
set search_path = public
as $$
  select b.id_bambino, b.nome::text, b.cognome::text,
         (select i.anno from public.iscrizione i
           where i.id_bambino = b.id_bambino and i.event_id = p_event_id
           order by i.id_iscrizione desc limit 1)
    from public.bambino b
   where b.event_id = p_event_id
     and public.has_any_perm(p_event_id, array['squadre.view', 'squadre.edit', 'iscritti.view', 'iscritti.edit', 'iscritti.pagamenti'])
$$;

-- Controlli "stesso evento" per i riferimenti tra tabelle (security definer:
-- funzionano anche se il chiamante non può leggere la tabella riferita).
create or replace function public.bambino_in_evento(p_id integer, p_event_id integer)
returns boolean language sql stable security definer set search_path = public as $$
  select exists (select 1 from public.bambino where id_bambino = p_id and event_id = p_event_id)
$$;

create or replace function public.iscrizione_in_evento(p_id integer, p_event_id integer)
returns boolean language sql stable security definer set search_path = public as $$
  select exists (select 1 from public.iscrizione where id_iscrizione = p_id and event_id = p_event_id)
$$;

create or replace function public.squadra_in_evento(p_id integer, p_event_id integer)
returns boolean language sql stable security definer set search_path = public as $$
  select exists (select 1 from public.squadra where id_squadra = p_id and event_id = p_event_id)
$$;

create or replace function public.settimana_in_evento(p_id integer, p_event_id integer)
returns boolean language sql stable security definer set search_path = public as $$
  select exists (select 1 from public.settimana where id_settimana = p_id and event_id = p_event_id)
$$;

create or replace function public.taglia_in_evento(p_id integer, p_event_id integer)
returns boolean language sql stable security definer set search_path = public as $$
  select exists (select 1 from public.taglia where id_taglia = p_id and event_id = p_event_id)
$$;

create or replace function public.tipo_consenso_in_evento(p_id integer, p_event_id integer)
returns boolean language sql stable security definer set search_path = public as $$
  select exists (select 1 from public.tipo_consenso where id_tipo_consenso = p_id and event_id = p_event_id)
$$;

-- Ci deve sempre essere almeno un superadmin attivo
create or replace function public.app_account_guard_superadmin()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
declare
  v_resta_superadmin boolean := false;
begin
  -- Serializza le modifiche concorrenti ai superadmin
  perform pg_advisory_xact_lock(hashtext('public.app_account.superadmin_guard'));

  if tg_op = 'UPDATE' then
    v_resta_superadmin := new.is_superadmin and new.attivo;
  end if;

  if not v_resta_superadmin and not exists (
    select 1 from public.app_account
     where is_superadmin and attivo and id <> old.id
  ) then
    raise exception 'Deve rimanere almeno un super amministratore attivo';
  end if;

  if tg_op = 'DELETE' then
    return old;
  end if;
  return new;
end
$$;

drop trigger if exists app_account_guard_superadmin on public.app_account;
create trigger app_account_guard_superadmin
  before update of is_superadmin, attivo or delete on public.app_account
  for each row
  when (old.is_superadmin)
  execute function public.app_account_guard_superadmin();

-- ---------------------------------------------------------------------
-- 4. Row Level Security
-- ---------------------------------------------------------------------

-- Elimina TUTTE le policy esistenti sulle tabelle gestite
do $$
declare
  pol record;
begin
  for pol in
    select schemaname, tablename, policyname
      from pg_policies
     where schemaname = 'public'
       and tablename in (
         'evento', 'evento_metadata', 'anno_scolastico', 'google_sheet',
         'bambino', 'iscrizione', 'genitore', 'genitore_bambino',
         'iscrizione_settimana', 'iscrizione_taglia', 'consenso',
         'scheda_sanitaria', 'settimana', 'taglia', 'tipo_consenso',
         'squadra', 'squadra_bambino',
         'app_account', 'ruolo', 'account_evento')
  loop
    execute format('drop policy %I on %I.%I', pol.policyname, pol.schemaname, pol.tablename);
  end loop;
end
$$;

alter table public.evento               enable row level security;
alter table public.evento_metadata      enable row level security;
alter table public.anno_scolastico      enable row level security;
alter table public.google_sheet         enable row level security;
alter table public.bambino              enable row level security;
alter table public.iscrizione           enable row level security;
alter table public.genitore             enable row level security;
alter table public.genitore_bambino     enable row level security;
alter table public.iscrizione_settimana enable row level security;
alter table public.iscrizione_taglia    enable row level security;
alter table public.consenso             enable row level security;
alter table public.scheda_sanitaria     enable row level security;
alter table public.settimana            enable row level security;
alter table public.taglia               enable row level security;
alter table public.tipo_consenso        enable row level security;
alter table public.squadra              enable row level security;
alter table public.squadra_bambino      enable row level security;
alter table public.app_account          enable row level security;
alter table public.ruolo                enable row level security;
alter table public.account_evento       enable row level security;

-- evento: visibile se si ha accesso, modificabile solo dal superadmin
create policy evento_select on public.evento for select to authenticated
  using (public.can_access_event(id_evento));
create policy evento_admin on public.evento for all to authenticated
  using (public.is_superadmin()) with check (public.is_superadmin());

-- Configurazione evento: lettura per chi ha accesso, scrittura superadmin
create policy evento_metadata_select on public.evento_metadata for select to authenticated
  using (public.can_access_event(event_id));
create policy evento_metadata_admin on public.evento_metadata for all to authenticated
  using (public.is_superadmin()) with check (public.is_superadmin());

create policy settimana_select on public.settimana for select to authenticated
  using (public.can_access_event(event_id));
create policy settimana_admin on public.settimana for all to authenticated
  using (public.is_superadmin()) with check (public.is_superadmin());

create policy taglia_select on public.taglia for select to authenticated
  using (public.can_access_event(event_id));
create policy taglia_admin on public.taglia for all to authenticated
  using (public.is_superadmin()) with check (public.is_superadmin());

create policy tipo_consenso_select on public.tipo_consenso for select to authenticated
  using (public.can_access_event(event_id));
create policy tipo_consenso_admin on public.tipo_consenso for all to authenticated
  using (public.is_superadmin()) with check (public.is_superadmin());

-- Anni scolastici: tabella globale di sola lettura
create policy anno_scolastico_select on public.anno_scolastico for select to authenticated
  using (true);
create policy anno_scolastico_admin on public.anno_scolastico for all to authenticated
  using (public.is_superadmin()) with check (public.is_superadmin());

-- Link Google: ciascun tipo ha il suo permesso
create policy google_sheet_select on public.google_sheet for select to authenticated
  using (
    (url_type = 1 and public.has_perm(event_id, 'google.sheet'))
    or (url_type = 2 and public.has_perm(event_id, 'google.drive')));
create policy google_sheet_admin on public.google_sheet for all to authenticated
  using (public.is_superadmin()) with check (public.is_superadmin());

-- Bambino e iscrizione: solo con i permessi iscritti. Chi vede solo le
-- squadre usa la RPC squadre_bambini (nome, cognome, classe).
create policy bambino_select on public.bambino for select to authenticated
  using (public.has_any_perm(event_id, array['iscritti.view', 'iscritti.edit', 'iscritti.pagamenti']));
create policy bambino_insert on public.bambino for insert to authenticated
  with check (public.has_perm(event_id, 'iscritti.edit'));
create policy bambino_update on public.bambino for update to authenticated
  using (public.has_perm(event_id, 'iscritti.edit')) with check (public.has_perm(event_id, 'iscritti.edit'));
create policy bambino_delete on public.bambino for delete to authenticated
  using (public.has_perm(event_id, 'iscritti.delete'));

create policy iscrizione_select on public.iscrizione for select to authenticated
  using (public.has_any_perm(event_id, array['iscritti.view', 'iscritti.edit', 'iscritti.pagamenti']));
create policy iscrizione_insert on public.iscrizione for insert to authenticated
  with check (public.has_perm(event_id, 'iscritti.edit') and public.bambino_in_evento(id_bambino, event_id));
create policy iscrizione_update on public.iscrizione for update to authenticated
  using (public.has_perm(event_id, 'iscritti.edit'))
  with check (public.has_perm(event_id, 'iscritti.edit') and public.bambino_in_evento(id_bambino, event_id));
create policy iscrizione_delete on public.iscrizione for delete to authenticated
  using (public.has_perm(event_id, 'iscritti.delete'));

-- Dati di dettaglio dell'iscrizione (genitori, settimane, taglia, consensi).
-- update_iscrizione può cancellare e reinserire le righe figlie, quindi
-- anche iscritti.edit può cancellarle.
-- Ogni riga figlia deve riferirsi a righe dello stesso evento.
do $$
declare
  r record;
begin
  for r in
    select * from (values
      ('genitore',             'true'),
      ('genitore_bambino',     'public.bambino_in_evento(id_bambino, event_id)'),
      ('iscrizione_settimana', 'public.iscrizione_in_evento(id_iscrizione, event_id) and public.settimana_in_evento(id_settimana, event_id)'),
      ('iscrizione_taglia',    'public.iscrizione_in_evento(id_iscrizione, event_id) and public.taglia_in_evento(id_taglia, event_id)'),
      ('consenso',             'public.iscrizione_in_evento(id_iscrizione, event_id) and public.tipo_consenso_in_evento(id_tipo_consenso, event_id)')
    ) as t(tabella, stesso_evento)
  loop
    execute format($f$
      create policy %1$s_select on public.%1$I for select to authenticated
        using (public.has_any_perm(event_id, array['iscritti.view', 'iscritti.edit']));
      create policy %1$s_insert on public.%1$I for insert to authenticated
        with check (public.has_perm(event_id, 'iscritti.edit') and %2$s);
      create policy %1$s_update on public.%1$I for update to authenticated
        using (public.has_perm(event_id, 'iscritti.edit'))
        with check (public.has_perm(event_id, 'iscritti.edit') and %2$s);
      create policy %1$s_delete on public.%1$I for delete to authenticated
        using (public.has_any_perm(event_id, array['iscritti.edit', 'iscritti.delete']));
    $f$, r.tabella, r.stesso_evento);
  end loop;
end
$$;

-- Genitori collegati da un altro evento (dati v1): restano visibili e
-- modificabili da chi gestisce l'evento del bambino, altrimenti il
-- salvataggio dell'iscrizione perderebbe il collegamento.
drop policy genitore_select on public.genitore;
drop policy genitore_update on public.genitore;
create policy genitore_select on public.genitore for select to authenticated
  using (
    public.has_any_perm(event_id, array['iscritti.view', 'iscritti.edit'])
    or exists (
      select 1 from public.genitore_bambino gb
       where gb.id_genitore = genitore.id_genitore
         and public.has_any_perm(gb.event_id, array['iscritti.view', 'iscritti.edit'])));
create policy genitore_update on public.genitore for update to authenticated
  using (
    public.has_perm(event_id, 'iscritti.edit')
    or exists (
      select 1 from public.genitore_bambino gb
       where gb.id_genitore = genitore.id_genitore
         and public.has_perm(gb.event_id, 'iscritti.edit')))
  with check (
    public.has_perm(event_id, 'iscritti.edit')
    or exists (
      select 1 from public.genitore_bambino gb
       where gb.id_genitore = genitore.id_genitore
         and public.has_perm(gb.event_id, 'iscritti.edit')));

-- Scheda sanitaria: dato sensibile, solo con permesso dedicato (o modifica)
create policy scheda_sanitaria_select on public.scheda_sanitaria for select to authenticated
  using (public.has_any_perm(event_id, array['iscritti.sanitario', 'iscritti.edit']));
create policy scheda_sanitaria_insert on public.scheda_sanitaria for insert to authenticated
  with check (public.has_perm(event_id, 'iscritti.edit') and public.iscrizione_in_evento(id_iscrizione, event_id));
create policy scheda_sanitaria_update on public.scheda_sanitaria for update to authenticated
  using (public.has_perm(event_id, 'iscritti.edit'))
  with check (public.has_perm(event_id, 'iscritti.edit') and public.iscrizione_in_evento(id_iscrizione, event_id));
create policy scheda_sanitaria_delete on public.scheda_sanitaria for delete to authenticated
  using (public.has_any_perm(event_id, array['iscritti.edit', 'iscritti.delete']));

-- Squadre
create policy squadra_select on public.squadra for select to authenticated
  using (public.has_any_perm(event_id, array['squadre.view', 'squadre.edit']));
create policy squadra_write on public.squadra for all to authenticated
  using (public.has_perm(event_id, 'squadre.edit')) with check (public.has_perm(event_id, 'squadre.edit'));

create policy squadra_bambino_select on public.squadra_bambino for select to authenticated
  using (public.has_any_perm(event_id, array['squadre.view', 'squadre.edit']));
create policy squadra_bambino_write on public.squadra_bambino for all to authenticated
  using (public.has_perm(event_id, 'squadre.edit'))
  with check (public.has_perm(event_id, 'squadre.edit')
              and public.bambino_in_evento(id_bambino, event_id)
              and public.squadra_in_evento(id_squadra, event_id));

-- Gestione account (scritture solo superadmin; utenti auth creati dalla
-- Edge Function admin-accounts con la service key)
create policy app_account_select on public.app_account for select to authenticated
  using (id = auth.uid() or public.is_superadmin());
create policy app_account_admin on public.app_account for all to authenticated
  using (public.is_superadmin()) with check (public.is_superadmin());

create policy ruolo_select on public.ruolo for select to authenticated
  using (true);
create policy ruolo_admin on public.ruolo for all to authenticated
  using (public.is_superadmin()) with check (public.is_superadmin());

create policy account_evento_select on public.account_evento for select to authenticated
  using (account_id = auth.uid() or public.is_superadmin());
create policy account_evento_admin on public.account_evento for all to authenticated
  using (public.is_superadmin()) with check (public.is_superadmin());

-- La vista deve rispettare la RLS delle tabelle sottostanti (PG15+)
do $$
begin
  if exists (select 1 from pg_views where schemaname = 'public' and viewname = 'v_iscrizione_completa') then
    execute 'alter view public.v_iscrizione_completa set (security_invoker = true)';
  end if;
end
$$;

-- ---------------------------------------------------------------------
-- 5. RPC esistenti: eseguite con i permessi del chiamante
-- ---------------------------------------------------------------------
do $$
declare
  f record;
begin
  for f in
    select p.oid::regprocedure as sig
      from pg_proc p
      join pg_namespace n on n.oid = p.pronamespace
     where n.nspname = 'public'
       and p.proname in ('create_iscrizione', 'update_iscrizione')
  loop
    execute format('alter function %s security invoker', f.sig);
    execute format('revoke all on function %s from anon, public', f.sig);
    execute format('grant execute on function %s to authenticated', f.sig);
  end loop;
end
$$;

-- delete_iscrizione cancella righe di tabelle che il chiamante potrebbe non
-- vedere (squadra, scheda sanitaria...): l'originale resta security definer
-- con il nome delete_iscrizione_interna e la nuova delete_iscrizione controlla
-- il permesso iscritti.delete sull'evento del bambino prima di chiamarla.
do $$
declare
  v_oid oid;
  v_args text;
  v_result text;
begin
  if not exists (select 1 from pg_proc where pronamespace = 'public'::regnamespace and proname = 'delete_iscrizione_interna') then
    select p.oid into v_oid from pg_proc p
     where p.pronamespace = 'public'::regnamespace and p.proname = 'delete_iscrizione';
    if v_oid is null then
      return;
    end if;
    execute format('alter function %s rename to delete_iscrizione_interna', v_oid::regprocedure);
  end if;

  select p.oid, pg_get_function_arguments(p.oid), pg_get_function_result(p.oid)
    into v_oid, v_args, v_result
    from pg_proc p
   where p.pronamespace = 'public'::regnamespace and p.proname = 'delete_iscrizione_interna';

  execute format('alter function %s security definer', v_oid::regprocedure);
  execute format('alter function %s set search_path = public', v_oid::regprocedure);
  execute format('revoke all on function %s from public, anon, authenticated', v_oid::regprocedure);

  execute format($f$
    create or replace function public.delete_iscrizione(%1$s)
    returns %2$s
    language plpgsql
    security definer
    set search_path = public
    as $body$
    declare
      v_event_id integer;
    begin
      select b.event_id into v_event_id from public.bambino b where b.id_bambino = p_id_bambino;
      if v_event_id is null then
        raise exception 'Iscritto non trovato';
      end if;
      if not public.has_perm(v_event_id, 'iscritti.delete') then
        raise exception 'Permesso negato' using errcode = '42501';
      end if;
      return public.delete_iscrizione_interna(p_id_bambino);
    end
    $body$;
  $f$, v_args, v_result);

  execute format('revoke all on function public.delete_iscrizione(%s) from public, anon', v_args);
  execute format('grant execute on function public.delete_iscrizione(%s) to authenticated', v_args);
end
$$;

-- ---------------------------------------------------------------------
-- 6. Privilegi: l'anonimo vede solo login_accounts()
-- ---------------------------------------------------------------------
revoke all on all tables in schema public from anon;

revoke all on function public.login_accounts() from public;
grant execute on function public.login_accounts() to anon, authenticated;

revoke all on function public.my_events() from public, anon;
revoke all on function public.my_account() from public, anon;
revoke all on function public.set_pagato(integer, boolean) from public, anon;
revoke all on function public.salva_squadre(integer, jsonb) from public, anon;
grant execute on function public.salva_squadre(integer, jsonb) to authenticated;
-- Funzioni di supporto: usate dalle policy, eseguibili solo da utenti autenticati
do $$
declare
  f text;
begin
  foreach f in array array[
    'public.tutti_permessi()', 'public.is_superadmin()', 'public.permessi_evento(integer)',
    'public.has_perm(integer, text)', 'public.has_any_perm(integer, text[])', 'public.can_access_event(integer)',
    'public.bambino_in_evento(integer, integer)', 'public.iscrizione_in_evento(integer, integer)',
    'public.squadra_in_evento(integer, integer)', 'public.settimana_in_evento(integer, integer)',
    'public.taglia_in_evento(integer, integer)', 'public.tipo_consenso_in_evento(integer, integer)']
  loop
    execute format('revoke all on function %s from public, anon', f);
    execute format('grant execute on function %s to authenticated', f);
  end loop;
end
$$;
revoke all on function public.app_account_guard_superadmin() from public, anon;

revoke all on function public.sposta_bambino(integer, integer, integer) from public, anon;
grant execute on function public.sposta_bambino(integer, integer, integer) to authenticated;
revoke all on function public.squadre_bambini(integer) from public, anon;
grant execute on function public.squadre_bambini(integer) to authenticated;
grant execute on function public.my_events() to authenticated;
grant execute on function public.my_account() to authenticated;
grant execute on function public.set_pagato(integer, boolean) to authenticated;

grant select, insert, update, delete on public.app_account, public.ruolo, public.account_evento to authenticated;
grant usage, select on sequence public.ruolo_id_seq to authenticated;

-- ---------------------------------------------------------------------
-- 7. Dati iniziali
-- ---------------------------------------------------------------------
insert into public.ruolo (id, nome, descrizione, permessi) values
  (1, 'Responsabile', 'Accesso completo all''evento', public.tutti_permessi()),
  (2, 'Animatore', 'Vede le squadre e i documenti Google', array['squadre.view', 'google.sheet', 'google.drive']),
  (3, 'Utente base', 'Vede solo le squadre', array['squadre.view'])
on conflict (id) do nothing;

select setval('public.ruolo_id_seq', greatest((select max(id) from public.ruolo), 1));

-- Account esistenti (erano cablati nell'app). Il Responsabile diventa
-- superadmin per non restare chiusi fuori: crea poi un account
-- amministratore dedicato e togli il flag se preferisci.
insert into public.app_account (id, nome, is_superadmin, ordine)
select u.id, 'Responsabile', true, 1 from auth.users u
 where u.email = 'michelecandian23@gmail.com'
on conflict (id) do nothing;

insert into public.app_account (id, nome, is_superadmin, ordine)
select u.id, 'Animatore', false, 2 from auth.users u
 where u.email = 'michelecandian26@gmail.com'
on conflict (id) do nothing;

insert into public.account_evento (account_id, event_id, ruolo_id)
select a.id, e.id_evento, case a.nome when 'Responsabile' then 1 else 2 end
  from public.app_account a
 cross join public.evento e
 where a.nome in ('Responsabile', 'Animatore')
on conflict do nothing;

commit;
