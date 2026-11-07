#!/usr/bin/env python3
"""Divide gli indirizzi esistenti in via, civico e comune e prepara il caricamento.

    python dividi_indirizzi.py dividi dati/esportazione.csv dati/revisione.csv
    python dividi_indirizzi.py genera dati/revisione.csv dati/carica.sql

I file in dati/ legano un bambino al suo indirizzo: non vanno mai committati (il repository
è pubblico). Passaggi completi nel README.md di questa cartella.
"""
from __future__ import annotations

import argparse
import collections
import csv
import difflib
import os
import re
import subprocess
import sys

CAMPI_REVISIONE = ["id_bambino", "stato", "nota", "indirizzo_originale", "comune_originale",
                   "via", "civico", "comune"]

TIPI_VIA = [
    (r"v\.?\s*le", "Viale"),
    (r"v\.?\s*lo|vic\.?", "Vicolo"),
    (r"p\.?\s*le", "Piazzale"),
    (r"p\.?\s*z{1,2}a", "Piazza"),
    (r"c\.?\s*so", "Corso"),
    (r"l\.?\s*go", "Largo"),
    (r"str\.?", "Strada"),
    (r"loc\.?", "Località"),
    (r"v\.?", "Via"),
]
RE_TIPO_ABBREVIATO = [(re.compile(rf"^(?:{p})(?=[\s.]|$)\.?\s*", re.I), n) for p, n in TIPI_VIA]
TIPI_INTERI = ("via", "viale", "vicolo", "piazza", "piazzale", "corso", "largo", "strada",
               "localita", "contra", "calle", "riviera", "galleria", "borgo", "frazione")

CIVICO = r"\d+(?:\s*[/\-]\s*[A-Za-z0-9]{1,3}|\s*(?:bis|ter)\b|\s*[A-Za-z]\b)?(?:\s*(?:int|scala|sc)\.?\s*\w{1,3})?"
SNC = r"s\.?\s*n\.?\s*c\.?|s\.?\s*n\.?"
RE_CIVICO_FINE = re.compile(
    rf"^(?P<via>.*?\S)[\s,]+(?:(?:n\.?|nr\.?|n°|num\.?|numero|civico)\s*)?(?P<civico>{CIVICO}|{SNC})\s*$", re.I)
RE_CIVICO_INIZIO = re.compile(rf"^(?P<civico>\d+\s*[A-Za-z]?)\s*[,\s]\s*(?P<via>[^\d].*)$", re.I)
RE_CODA_COMUNE = re.compile(
    rf"^(?P<resto>.*?\d\s*[A-Za-z/\d]*)\s*[,\-]\s*(?:\d{{5}}\s+)?(?P<comune>[^\d,]{{3,}})$")
RE_CIVICO_VALIDO = re.compile(r"^(?:\d+(?:[/\-][A-Za-z0-9]{1,3}|[A-Za-z]| ?bis| ?ter)?(?: (?:int|scala|sc)\.? ?\w{1,3})?|snc)$", re.I)

MINUSCOLE = {"di", "del", "dello", "della", "dei", "degli", "delle", "da", "dal", "dallo", "dalla",
             "dai", "dagli", "dalle", "de", "e", "ed", "al", "allo", "alla", "ai", "agli", "alle",
             "in", "sul", "sulla", "a", "con", "per", "tra", "il", "lo", "la", "i", "gli", "le"}
ELISIONI = {"dell", "dall", "nell", "sull", "all", "l", "un"}
RE_ROMANO = re.compile(r"^(?=[IVX])X{0,3}(?:IX|IV|V?I{0,3})$", re.I)
ACCENTI = str.maketrans("aeiou", "àèìòù")


def spazi(testo: str | None) -> str:
    return re.sub(r"\s+", " ", (testo or "").replace(" ", " ")).strip()


def normalizza(testo: str | None) -> str:
    """Stessa chiave di public.normalizza_toponimo: due nomi uguali qui sono la stessa via."""
    t = (testo or "").lower().translate(str.maketrans("àáâãäèéêëìíîïòóôõöùúûüç", "aaaaaeeeeiiiiooooouuuuc"))
    t = re.sub(r"[^a-z0-9]+", " ", t).strip()
    for schema, nuovo in [(r"^(v le|vle) ", "viale "), (r"^(v lo|vlo|vic) ", "vicolo "), (r"^v ", "via "),
                          (r"^(p le|ple) ", "piazzale "), (r"^(p za|p zza|pza|pzza) ", "piazza "),
                          (r"^(c so|cso) ", "corso "), (r"^(l go|lgo) ", "largo "),
                          (r"^str ", "strada "), (r"^loc ", "localita ")]:
        t = re.sub(schema, nuovo, t)
    return re.sub(r"\bs\b", "san", t)


def _parola(parola: str, prima: bool) -> str:
    if RE_ROMANO.match(parola):
        return parola.upper()
    # "NICOLO'" scritto senza accento
    if re.fullmatch(r".{2,}[aeiouAEIOU]'", parola):
        parola = parola[:-2] + parola[-2].lower().translate(ACCENTI)
    if "'" in parola:
        testa, _, coda = parola.partition("'")
        testa_ok = testa.lower() if testa.lower() in ELISIONI and not prima else testa.capitalize()
        return f"{testa_ok}'{_parola(coda, False)}" if coda else f"{testa_ok}'"
    if parola.lower() in MINUSCOLE and not prima:
        return parola.lower()
    return parola[:1].upper() + parola[1:].lower()


def titolo(testo: str | None) -> str:
    """Maiuscole uniformi: "VIA DELL'OSPEDALE" -> "Via dell'Ospedale"."""
    t = re.sub(r"\.(?=\S)", ". ", spazi(testo).replace("’", "'").replace("`", "'"))
    t = re.sub(r"'\s+(?=\w)", "'", t)
    return " ".join(_parola(p, i == 0) for i, p in enumerate(t.split(" ")) if p)


def espandi_tipo(via: str) -> str:
    for schema, nome in RE_TIPO_ABBREVIATO:
        if schema.match(via):
            return f"{nome} {schema.sub('', via, count=1)}".strip()
    return via


def ha_tipo(via: str) -> bool:
    return normalizza(via).split(" ")[0] in TIPI_INTERI


def pulisci_comune(comune: str | None) -> str:
    c = re.sub(r"\b\d{5}\b", " ", spazi(comune))
    c = re.sub(r"\s*\(\s*[A-Za-z]{2}\s*\)\s*$", "", c)     # "Padova (PD)"
    c = re.sub(r"[\s,\-]+[A-Z]{2}$", "", c)                 # "Padova PD", "Padova - PD"
    return spazi(c).strip(" ,-")


def pulisci_civico(civico: str) -> str:
    c = spazi(civico)
    if re.fullmatch(SNC, c, re.I):
        return "snc"
    m = re.match(r"(\d+)(.*)$", c)
    if not m:
        return c
    numero, resto = m.group(1), m.group(2)
    interno = ""
    mi = re.search(r"\s*(int|scala|sc)\.?\s*(\w{1,3})$", resto, re.I)
    if mi:
        interno = f" {mi.group(1).lower()}. {mi.group(2)}"
        resto = resto[:mi.start()]
    resto = re.sub(r"\s+", "", resto)
    if re.fullmatch(r"bis|ter", resto, re.I):
        return f"{numero} {resto.lower()}{interno}"
    return f"{numero}{resto.upper()}{interno}"


def dividi_riga(indirizzo: str | None, comune: str | None) -> dict:
    """Propone via, civico e comune di una riga. stato: ok, rivedere o vuoto."""
    indirizzo, comune = spazi(indirizzo), pulisci_comune(comune)
    note: list[str] = []
    rivedere = False

    if not indirizzo and not comune:
        return dict(via="", civico="", comune="", stato="vuoto", nota="")

    # Comune e indirizzo nelle colonne sbagliate
    if comune and ha_tipo(espandi_tipo(comune)) and not ha_tipo(espandi_tipo(indirizzo)):
        indirizzo, comune = comune, pulisci_comune(indirizzo)
        note.append("colonne scambiate")
        rivedere = True

    via, civico = indirizzo, ""
    if indirizzo:
        # "Via Roma 12, Padova": il comune finito dentro l'indirizzo
        m = RE_CODA_COMUNE.match(indirizzo)
        if m and not re.match(r"(?:int|scala|sc)\b", m.group("comune").strip(), re.I):
            coda = pulisci_comune(m.group("comune"))
            if not comune:
                comune = coda
                note.append("comune preso dall'indirizzo")
                via = m.group("resto")
            elif normalizza(coda) == normalizza(comune):
                via = m.group("resto")
            else:
                note.append(f"nell'indirizzo c'è anche «{coda}»")
                rivedere = True
                via = m.group("resto")

        fine = RE_CIVICO_FINE.match(via)
        inizio = RE_CIVICO_INIZIO.match(via) if not fine else None
        if fine:
            via, civico = fine.group("via"), pulisci_civico(fine.group("civico"))
        elif inizio:
            via, civico = inizio.group("via"), pulisci_civico(inizio.group("civico"))
            note.append("civico trovato all'inizio")
            rivedere = True
        elif re.search(r"\d", via) and not re.search(r"\b\d{1,2}\s+\w+", via):
            note.append("numero non riconosciuto")
            rivedere = True

        via = titolo(espandi_tipo(via.strip(" ,;-")))
        if not civico:
            note.append("senza civico")
        if len(normalizza(via)) < 3 or re.search(r"[,;/()]", via):
            note.append("via da controllare")
            rivedere = True
        if not comune:
            note.append("senza comune")
            rivedere = True

    return dict(via=via, civico=civico, comune=titolo(comune), nota="; ".join(note),
                stato="rivedere" if rivedere else "ok")


def _forma_migliore(contatore: collections.Counter) -> str:
    """Tra le scritture della stessa via: la più completa ("San" invece di "S.", con gli accenti)."""
    return max(contatore, key=lambda n: (len(n), sum(ord(ch) > 127 for ch in n), contatore[n]))


def _segna(riga: dict, nota: str) -> None:
    riga["nota"] = "; ".join(x for x in (riga["nota"], nota) if x)
    if riga["stato"] == "ok":
        riga["stato"] = "rivedere"


def _simili(nomi) -> dict[str, str]:
    """Per ogni nome, quello quasi uguale con più righe (possibile doppione)."""
    trovati: dict[str, str] = {}
    chiavi = sorted(nomi, key=lambda n: (-nomi[n], n))
    for i, forte in enumerate(chiavi):
        for debole in chiavi[i + 1:]:
            if debole not in trovati and difflib.SequenceMatcher(None, forte, debole).ratio() >= 0.85:
                trovati[debole] = forte
    return trovati


def unifica(righe: list[dict]) -> None:
    """Stessa scrittura per stessa via e comune; segnala i possibili doppioni da controllare."""
    comuni = collections.defaultdict(collections.Counter)
    for r in righe:
        if r["comune"]:
            comuni[normalizza(r["comune"])][r["comune"]] += 1
    nome_comune = {k: _forma_migliore(v) for k, v in comuni.items()}
    for r in righe:
        if r["comune"]:
            r["comune"] = nome_comune[normalizza(r["comune"])]

    conteggio_comuni = {k: sum(v.values()) for k, v in comuni.items()}
    comuni_simili = _simili(conteggio_comuni)

    # "Roma 12" senza "Via": va con "Via Roma" se quella via esiste già nel comune
    vie = collections.defaultdict(collections.Counter)
    for r in righe:
        if r["via"] and r["comune"] and ha_tipo(r["via"]):
            vie[normalizza(r["comune"])][normalizza(r["via"])] += 1
    for r in righe:
        if r["via"] and not ha_tipo(r["via"]):
            candidata = f"via {normalizza(r['via'])}"
            if r["comune"] and candidata in vie[normalizza(r["comune"])]:
                r["via"] = titolo(f"Via {r['via']}")
                r["nota"] = "; ".join(x for x in (r["nota"], "aggiunto «Via»") if x)
            else:
                _segna(r, "manca il tipo (via, piazza...)")

    scritture = collections.defaultdict(collections.Counter)
    for r in righe:
        if r["via"] and r["comune"]:
            scritture[(normalizza(r["comune"]), normalizza(r["via"]))][r["via"]] += 1
    nome_via = {k: _forma_migliore(v) for k, v in scritture.items()}
    per_comune = collections.defaultdict(dict)
    for (c, v), cont in scritture.items():
        per_comune[c][v] = sum(cont.values())
    vie_simili = {c: _simili(nomi) for c, nomi in per_comune.items()}

    for r in righe:
        c = normalizza(r["comune"])
        if c in comuni_simili:
            _segna(r, f"comune simile a «{nome_comune[comuni_simili[c]]}»")
        if r["via"] and r["comune"]:
            v = normalizza(r["via"])
            r["via"] = nome_via[(c, v)]
            if v in vie_simili[c]:
                _segna(r, f"via simile a «{nome_via[(c, vie_simili[c][v])]}»")


def leggi_csv(percorso: str) -> list[dict]:
    with open(percorso, encoding="utf-8-sig", newline="") as f:
        prima = f.readline()
        f.seek(0)
        return list(csv.DictReader(f, delimiter=";" if prima.count(";") > prima.count(",") else ","))


def _controlla_percorso(percorso: str) -> None:
    """Dentro il repository (pubblico) si scrive solo in cartelle ignorate da git."""
    cartella = os.path.dirname(os.path.abspath(percorso))
    try:
        esito = subprocess.run(["git", "-C", cartella, "check-ignore", "-q", os.path.abspath(percorso)],
                               capture_output=True)
    except FileNotFoundError:
        return
    if esito.returncode == 1:
        sys.exit(f"{percorso}: git non ignora questo file e il repository è pubblico. Usa la cartella dati/.")


def comando_dividi(ingresso: str, uscita: str) -> None:
    _controlla_percorso(uscita)
    righe = []
    for r in leggi_csv(ingresso):
        proposta = dividi_riga(r.get("indirizzo_residenza"), r.get("comune_residenza"))
        righe.append({"id_bambino": r["id_bambino"], "indirizzo_originale": r.get("indirizzo_residenza") or "",
                      "comune_originale": r.get("comune_residenza") or "", **proposta})
    unifica(righe)
    ordine = {"rivedere": 0, "ok": 1, "vuoto": 2}
    righe.sort(key=lambda r: (ordine[r["stato"]], r["comune"], r["via"]))
    with open(uscita, "w", encoding="utf-8-sig", newline="") as f:
        w = csv.DictWriter(f, CAMPI_REVISIONE, delimiter=";")
        w.writeheader()
        w.writerows(righe)
    stati = collections.Counter(r["stato"] for r in righe)
    vie = {(normalizza(r["comune"]), normalizza(r["via"])) for r in righe if r["via"] and r["comune"]}
    print(f"{len(righe)} righe: {stati['ok']} ok, {stati['rivedere']} da rivedere, {stati['vuoto']} vuote")
    print(f"{len({normalizza(r['comune']) for r in righe if r['comune']})} comuni, {len(vie)} vie distinte")
    print(f"Revisione: {uscita}")


def _sql(valore: str | None) -> str:
    return "null" if not valore else "'" + valore.replace("'", "''") + "'"


def comando_genera(ingresso: str, uscita: str) -> None:
    _controlla_percorso(uscita)
    errori, valori, visti = [], [], set()
    saltate = collections.Counter()
    for r in leggi_csv(ingresso):
        stato = (r.get("stato") or "").strip().lower()
        if stato != "ok":
            saltate[stato] += 1
            continue
        ident, via = (r.get("id_bambino") or "").strip(), spazi(r.get("via"))
        civico, comune = spazi(r.get("civico")), spazi(r.get("comune"))
        etichetta = f"id_bambino {ident}"
        if not ident.isdigit() or ident in visti:
            errori.append(f"{etichetta}: identificativo non valido o ripetuto")
        visti.add(ident)
        # Excel trasforma "12/3" in "12-mar": meglio fermarsi che caricare un civico sbagliato
        if civico and not RE_CIVICO_VALIDO.match(civico):
            errori.append(f"{etichetta}: civico «{civico}» non valido")
        if via and not comune:
            errori.append(f"{etichetta}: via senza comune")
        if civico and not via:
            errori.append(f"{etichetta}: civico senza via")
        if not comune:
            errori.append(f"{etichetta}: riga ok senza comune")
        valori.append(f"({ident}, {_sql(r.get('indirizzo_originale'))}, {_sql(r.get('comune_originale'))}, "
                      f"{_sql(comune)}, {_sql(via)}, {_sql(civico)})")
    if errori:
        sys.exit("Correggi la revisione e riprova:\n  " + "\n  ".join(errori))
    if not valori:
        sys.exit("Nessuna riga con stato ok.")

    testo = _MODELLO_SQL.replace("--RIGHE--", ",\n".join(valori))
    with open(uscita, "w", encoding="utf-8", newline="\n") as f:
        f.write(testo)
    print(f"{len(valori)} righe in {uscita}; non caricate: {dict(saltate)}")


_MODELLO_SQL = """-- GENERATO da dividi_indirizzi.py. Contiene dati personali di minori: non committare.
-- Tutto o niente. Una riga cambiata dopo l'esportazione non viene toccata: l'id finisce in "non_aggiornati".
begin;

create temp table _indirizzi (
  id_bambino integer primary key,
  orig_indirizzo text, orig_comune text,
  comune text not null, via text, civico text
) on commit drop;

insert into _indirizzi values
--RIGHE--
;

insert into public.comune(nome)
  select distinct on (public.normalizza_toponimo(comune)) comune
    from _indirizzi
   order by public.normalizza_toponimo(comune), comune
  on conflict (nome_norm) do nothing;

insert into public.via(id_comune, nome)
  select distinct on (c.id_comune, public.normalizza_toponimo(i.via)) c.id_comune, i.via
    from _indirizzi i
    join public.comune c on c.nome_norm = public.normalizza_toponimo(i.comune)
   where i.via is not null
   order by c.id_comune, public.normalizza_toponimo(i.via), i.via
  on conflict (id_comune, nome_norm) do nothing;

create temp table _esito (righe_file integer, aggiornati integer, non_aggiornati integer[]);

with aggiornati as (
  update public.bambino b
     set id_comune = c.id_comune,
         id_via = v.id_via,
         civico = i.civico,
         comune_residenza = c.nome,
         indirizzo_residenza = case when v.id_via is null then null
                                    else v.nome || coalesce(' ' || i.civico, '') end
    from _indirizzi i
    join public.comune c on c.nome_norm = public.normalizza_toponimo(i.comune)
    left join public.via v on v.id_comune = c.id_comune and v.nome_norm = public.normalizza_toponimo(i.via)
   where b.id_bambino = i.id_bambino
     and coalesce(b.indirizzo_residenza, '') = coalesce(i.orig_indirizzo, '')
     and coalesce(b.comune_residenza, '') = coalesce(i.orig_comune, '')
  returning b.id_bambino
)
insert into _esito
select (select count(*) from _indirizzi),
       (select count(*) from aggiornati),
       (select coalesce(array_agg(i.id_bambino), '{}') from _indirizzi i
         where i.id_bambino not in (select id_bambino from aggiornati));

commit;

select * from _esito;
"""


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("comando", choices=["dividi", "genera"])
    parser.add_argument("ingresso")
    parser.add_argument("uscita")
    args = parser.parse_args()
    (comando_dividi if args.comando == "dividi" else comando_genera)(args.ingresso, args.uscita)


if __name__ == "__main__":
    main()
