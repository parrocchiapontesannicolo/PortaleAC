# Piano rilascio v2 (aggiornato 2026-10-11)

## Stato
- 2026-10-11: **DB migrato** (verificato: login_accounts risponde, anonimo bloccato sulle tabelle)
  e **v2 pubblicata** su GitHub Pages (branch `v2-evoluzione`).
- Migrazione corretta e committata (`supabase/migrations/20261010120000_v2_account_permessi.sql`):
  - allineata allo schema reale (google_sheet bigint, RPC iscrizione reali, delete con squadra_bambino);
  - revisione rifatta il 2026-10-11: chiusa una falla per cui chi modifica un evento poteva collegarsi
    i genitori di un altro evento (`genitore_collegabile`).
  - Test su copia dello schema reale in Docker: applicata 2 volte, **113/113 controlli OK**.

- 2026-10-11: **nuovo stile online** (colori/font del sito della parrocchia, login con foto della chiesa,
  tema Automatico/Chiaro/Scuro) + riparati menu account e selettore evento (MudBlazor 9).

## Dominio portale.parrocchiapontesannicolo.com
- 2026-10-11: record DNS creato, dominio pubblicato, certificato HTTPS emesso; github.io/PortaleAC reindirizza.
- Michele: GitHub → Settings → Pages → "Enforce HTTPS"; aggiungere "Portale AC" al menu del sito WordPress.
- Dopo il cambio tutti devono rifare il login una volta (le sessioni sono salvate per indirizzo).

## Da fare — Claude
- Aprire la PR verso `main` quando Michele lo chiede.

## Da fare — Michele
1. ~~Backup~~ · ~~Migrazione~~ · ~~Push~~ · ~~Edge Function admin-accounts~~ (fatti il 2026-10-11)
5. Authentication → Providers → Email: disattivare "Allow new users to sign up".
6. Test: Responsabile (login, evento, lista, nuovo, modifica, squadre) e Animatore (solo squadre e documenti), anche da telefono.
7. Poi: creare un account amministratore dedicato, aprire la PR verso `main`.

## Note
- `get_grest_excel` (export con password, eseguibile da anonimo) non è toccata: se serve al foglio Google resta com'è; da rivedere.
- `genera_squadre_equilibrate` usa una tabella inesistente (`squadre`): già rotta, non usata dall'app.
