-- Da eseguire nel SQL Editor di Supabase, poi "Download CSV" e salvare in dati/esportazione.csv.
-- Solo le righe non ancora collegate all'anagrafica: la prima volta sono tutte, dopo solo quelle
-- rimaste da sistemare. Niente nomi: alla divisione bastano identificativo e indirizzo.
--
-- Prima della migrazione la colonna id_via non esiste ancora: togliere la riga "id_via is null and".
select id_bambino, indirizzo_residenza, comune_residenza
  from public.bambino
 where id_via is null and
       (coalesce(btrim(indirizzo_residenza), '') <> '' or coalesce(btrim(comune_residenza), '') <> '')
 order by id_bambino;
