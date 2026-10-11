# Piano rilascio v2 (aggiornato 2026-10-11)

## Stato
- 2026-10-11: **DB migrato** (verificato: login_accounts risponde, anonimo bloccato sulle tabelle)
  e **v2 pubblicata** su GitHub Pages (branch `v2-evoluzione`).
- Migrazione corretta e committata (`supabase/migrations/20261010120000_v2_account_permessi.sql`):
  - allineata allo schema reale (google_sheet bigint, RPC iscrizione reali, delete con squadra_bambino);
  - revisione rifatta il 2026-10-11: chiusa una falla per cui chi modifica un evento poteva collegarsi
    i genitori di un altro evento (`genitore_collegabile`).
  - Test su copia dello schema reale in Docker: applicata 2 volte, **113/113 controlli OK**.

## Da fare — Claude
- Niente in sospeso; aprire la PR verso `main` quando Michele lo chiede.

## Da fare — Michele
1. ~~Backup~~ · ~~Migrazione~~ · ~~Push~~ (fatti il 2026-10-11)
4. Deploy Edge Function `admin-accounts` (CLI o editor funzioni della Dashboard) — serve solo per creare/eliminare account e cambiare password.
5. Authentication → Providers → Email: disattivare "Allow new users to sign up".
6. Test: Responsabile (login, evento, lista, nuovo, modifica, squadre) e Animatore (solo squadre e documenti), anche da telefono.
7. Poi: creare un account amministratore dedicato, aprire la PR verso `main`.

## Note
- `get_grest_excel` (export con password, eseguibile da anonimo) non è toccata: se serve al foglio Google resta com'è; da rivedere.
- `genera_squadre_equilibrate` usa una tabella inesistente (`squadre`): già rotta, non usata dall'app.
