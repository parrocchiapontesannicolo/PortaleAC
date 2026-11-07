-- Fonte: Comune di Ponte San Nicolò, "Stradario comunale", aggiornato al 04-11-2026, licenza CC BY 4.0
-- https://opendata.boxxapps.com/en/dataset/4333_stradario
-- Solo nomi di vie, nessun dato personale. Nomi riportati in maiuscole/minuscole; esclusi i 3 mercati,
-- "Via I. Nievo" (doppione di Via Ippolito Nievo) e "Viale Viale Filandia" (doppione errato di Viale Finlandia).
-- Porta le vie abbreviate al nome ufficiale e riallinea il testo dell'indirizzo dei bambini collegati.
-- Un'unica istruzione: tutto o niente. Se la riesegui non trova più i vecchi nomi e non cambia nulla.
with mappa(vecchio, nuovo) as (values
  ('Piazza Papa Luciani', 'Piazza Papa Albino Luciani'),
  ('Via A. Briosco', 'Via Andrea Briosco'),
  ('Via A. Canova', 'Via Antonio Canova'),
  ('Via A. Manzoni', 'Via Alessandro Manzoni'),
  ('Via A. Vespucci', 'Via Amerigo Vespucci'),
  ('Via Angelo Scapin', 'Via Don Angelo Scapin'),
  ('Via Bachelet', 'Via Vittorio Bachelet'),
  ('Via Botticelli', 'Via Sandro Botticelli'),
  ('Via Carducci', 'Via Giosuè Carducci'),
  ('Via Cavour', 'Via Camillo Cavour'),
  ('Via Dante', 'Via Dante Alighieri'),
  ('Via Don Orione', 'Via Don Luigi Orione'),
  ('Via Faggin', 'Via Leandro Faggin'),
  ('Via G. Marconi', 'Via Guglielmo Marconi'),
  ('Via G. Mazzini', 'Via Giuseppe Mazzini'),
  ('Via G. Rossa', 'Via Guido Rossa'),
  ('Via Garibaldi', 'Via Giuseppe Garibaldi'),
  ('Via Kolbe', 'Via Massimiliano Kolbe'),
  ('Via M. Toffanin', 'Via Matteo Toffanin'),
  ('Via Mantegna', 'Via Andrea Mantegna'),
  ('Via Rossini', 'Via Gioacchino Rossini'),
  ('Via S. Pellico', 'Via Silvio Pellico'),
  ('Via Tiepolo', 'Via Giambattista Tiepolo')
), rinominate as (
  update public.via v
     set nome = m.nuovo
    from mappa m, public.comune c
   where c.nome_norm = public.normalizza_toponimo('Ponte San Nicolò')
     and v.id_comune = c.id_comune
     and v.nome = m.vecchio
  returning v.id_via
), bambini as (
  update public.bambino b
     set indirizzo_residenza = m.nuovo || coalesce(' ' || b.civico, '')
    from public.via v
    join mappa m on m.vecchio = v.nome
    join public.comune c on c.id_comune = v.id_comune
   where c.nome_norm = public.normalizza_toponimo('Ponte San Nicolò')
     and b.id_via = v.id_via
  returning b.id_bambino
)
select (select count(*) from rinominate) as vie_rinominate,
       (select count(*) from bambini) as bambini_aggiornati;
