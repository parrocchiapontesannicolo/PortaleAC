# Piano rilascio v2 (aggiornato 2026-10-11)

## Stato
- Branch `v2-evoluzione`: GitHub Pages punta qui; online c'è ancora la **build v1** (funziona col DB attuale).
- DB live **non ancora migrato** (primo tentativo annullato con rollback completo, DB invariato).
- Migrazione corretta e committata (`supabase/migrations/20261010120000_v2_account_permessi.sql`):
  - allineata allo schema reale (google_sheet bigint, RPC iscrizione reali, delete con squadra_bambino);
  - revisione rifatta il 2026-10-11: chiusa una falla per cui chi modifica un evento poteva collegarsi
    i genitori di un altro evento (`genitore_collegabile`).
  - Test su copia dello schema reale in Docker: applicata 2 volte, **113/113 controlli OK**.
- Build v2 pronta e **committata in locale** (commit locali NON ancora pushati).

## Da fare — Claude
- Solo al "push" di Michele: `git push` di `v2-evoluzione`, poi verificare che il sito risponda con la v2.

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
