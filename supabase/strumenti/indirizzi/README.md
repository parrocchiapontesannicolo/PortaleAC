# Anagrafica degli indirizzi: caricamento delle righe esistenti

Divide `bambino.indirizzo_residenza` e `comune_residenza` (testo libero) in **comune**, **via**
e **civico**, una sola riga per ogni via di ogni comune.

> ⚠️ **Il repository è pubblico** e questi file legano un bambino al suo indirizzo. Vanno solo in
> `dati/` (ignorata da git), mai altrove. Lo strumento si rifiuta di scrivere in un percorso del
> repository non ignorato. A lavoro finito tieni l'esportazione dove tieni i backup e cancella `dati/`.

## Ordine di rilascio

Il client già online continua a funzionare dopo la migrazione; il client nuovo invece ha bisogno
della migrazione. L'ordine è quindi obbligato:

1. **Backup** del database (Dashboard → Database → Backups).
2. **Migrazione**: `20261107120000_anagrafica_indirizzi.sql` (SQL Editor oppure `supabase db push`).
   Aggiunge soltanto: non cambia nessun dato esistente.
3. **Esporta**: esegui `1_esporta.sql` nel SQL Editor, *Download CSV*, salva in `dati/esportazione.csv`.
4. **Dividi** (da questa cartella):
   ```bash
   python dividi_indirizzi.py dividi dati/esportazione.csv dati/revisione.csv
   ```
5. **Rivedi** `dati/revisione.csv`. Le righe `rivedere` sono in cima, con il motivo in `nota`:
   correggi `via`, `civico`, `comune` e metti `stato` a `ok`. Le righe lasciate `rivedere` non si
   caricano e restano com'erano.
6. **Genera** ed esegui `dati/carica.sql` nel SQL Editor:
   ```bash
   python dividi_indirizzi.py genera dati/revisione.csv dati/carica.sql
   ```
   Il risultato mostra quante righe sono state aggiornate. `non_aggiornati` elenca gli id cambiati
   dopo l'esportazione: non vengono toccati.
7. **Pubblica il client nuovo** (vedi `supabase/README.md`).
8. **Ripeti 3-6** per le righe rimaste o modificate nel frattempo: l'esportazione prende solo quelle
   non ancora collegate.

## Cose da sapere

- **Excel** trasforma il civico `12/3` in una data. Meglio rivedere il file con un editor di testo o
  importare la colonna come testo. `genera` rifiuta i civici non validi, quindi l'errore non arriva
  al database, ma il valore originale va ricopiato da `indirizzo_originale`.
- I **doppioni evidenti** (`V. Roma`, `VIA ROMA`) sono unificati. Quelli **solo simili** (`Via
  Garibaldi` / `Via Garibladi`, `S. Antonio` / `Sant'Antonio`) non si uniscono mai da soli: vanno in
  `rivedere` perché potrebbero essere vie diverse. Per unirli scrivi lo stesso nome nelle due righe.
- Il caricamento **riscrive i vecchi campi di testo** nella forma pulita (`VIA ROMA,12` → `Via Roma
  12`), così `get_grest_excel` e il client online restano allineati. L'originale resta
  nell'esportazione e nel backup.

## Modifiche successive

`indirizzo_residenza` e `comune_residenza` restano perché li legge anche `get_grest_excel`, che non è
in questo repository. Si potranno togliere solo dopo averla aggiornata.

## Test

```bash
python -m unittest test_dividi_indirizzi
```
