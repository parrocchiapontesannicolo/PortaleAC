# Piano rilascio v2 (aggiornato 2026-10-10)

## Stato
- Branch `v2-evoluzione` pushato; GitHub Pages ora punta a questo branch, ma nella root c'è ancora la **build v1** (funziona col DB attuale).
- Primo tentativo di migrazione fallito (`has_perm(bigint, unknown)`): **rollback completo, DB invariato**.
- Schema reale esportato → migrazione corretta (NON ancora committata):
  - `google_sheet.event_id` è bigint → cast nella policy.
  - `create_iscrizione` / `update_iscrizione` riscritte dalle versioni reali + controllo permesso;
    update toglie solo i genitori rimossi (non perde i genitori condivisi tra eventi).
  - `delete_iscrizione` toglie prima `squadra_bambino` (FK senza cascade).
- Test su copia dello schema reale in Docker: migrazione applicata 2 volte, **109/109 controlli OK**
  (harness: scratchpad `real/run.sh`).
- In corso: revisione indipendente (2 agenti) della migrazione.

## Da fare — Claude
1. Leggere l'esito della revisione, correggere eventuali problemi, rilanciare i test.
2. Committare la migrazione corretta (+ README) su `v2-evoluzione`.
3. Preparare la build: `dotnet publish -c Release`, copiare `wwwroot` nella root
   (`_framework/`, `index.html`, `404.html`, `css/`, `js/`), `<base href="/PortaleAC/" />` in entrambi gli html.
   Commit locale, **push solo dopo la migrazione**.

## Da fare — Michele
1. Backup: Dashboard → Database → Backups.
2. SQL Editor: incollare ed eseguire `supabase/migrations/20261010120000_v2_account_permessi.sql` (versione corretta).
3. Dire a Claude "push" → il sito passa alla v2 (1–2 minuti).
4. Deploy Edge Function `admin-accounts` (CLI o editor funzioni della Dashboard) — serve solo per creare/eliminare account e cambiare password.
5. Authentication → Providers → Email: disattivare "Allow new users to sign up".
6. Test: Responsabile (login, evento, lista, nuovo, modifica, squadre) e Animatore (solo squadre e documenti), anche da telefono.
7. Poi: creare un account amministratore dedicato, aprire la PR verso `main`.

## Note
- Tra il passo 2 e 3 il sito vecchio non funziona (pochi minuti): farli di seguito.
- `get_grest_excel` (export con password, eseguibile da anonimo) non è toccata: se serve al foglio Google resta com'è; da rivedere.
- `genera_squadre_equilibrate` usa una tabella inesistente (`squadre`): già rotta, non usata dall'app.
