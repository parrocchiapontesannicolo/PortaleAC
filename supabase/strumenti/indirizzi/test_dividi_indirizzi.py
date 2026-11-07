"""Tutti i nomi qui sono inventati. python -m unittest test_dividi_indirizzi"""
import unittest

from dividi_indirizzi import dividi_riga, normalizza, titolo, unifica

PSN = "Ponte San Nicolò"


class DividiRiga(unittest.TestCase):
    def controlla(self, indirizzo, comune, via, civico, comune_atteso=PSN, stato="ok"):
        r = dividi_riga(indirizzo, comune)
        self.assertEqual((r["via"], r["civico"], r["comune"], r["stato"]),
                         (via, civico, comune_atteso, stato), r["nota"])

    def test_forme_del_civico(self):
        for testo, civico in [("Via Roma 12", "12"), ("via roma, 12", "12"), ("VIA ROMA N. 12", "12"),
                              ("Via Roma n°12", "12"), ("Via Roma 12/a", "12/A"), ("Via Roma 12 A", "12A"),
                              ("Via Roma 12a", "12A"), ("Via Roma 12 bis", "12 bis"), ("Via Roma 12-14", "12-14"),
                              ("Via Roma snc", "snc"), ("Via Roma s.n.c.", "snc"),
                              ("Via Roma 12 int. 3", "12 int. 3")]:
            with self.subTest(testo):
                self.controlla(testo, PSN, "Via Roma", civico)

    def test_abbreviazioni(self):
        for testo, via in [("V. Roma 1", "Via Roma"), ("V.le Europa 1", "Viale Europa"),
                           ("P.zza Liberta' 1", "Piazza Libertà"), ("Pza Duomo 1", "Piazza Duomo"),
                           ("C.so Italia 1", "Corso Italia"), ("Vic. Stretto 1", "Vicolo Stretto"),
                           ("L.go Marconi 1", "Largo Marconi")]:
            with self.subTest(testo):
                self.controlla(testo, PSN, via, "1")

    def test_numeri_nel_nome_della_via(self):
        self.controlla("Via 4 Novembre 12", PSN, "Via 4 Novembre", "12")
        self.controlla("Via XX Settembre, 3", PSN, "Via XX Settembre", "3")
        self.controlla("Via Papa Giovanni XXIII 3", PSN, "Via Papa Giovanni XXIII", "3")
        # Senza civico il numero è parte del nome, non va staccato
        r = dividi_riga("Via 4 Novembre", PSN)
        self.assertEqual((r["via"], r["civico"]), ("Via 4 Novembre", ""))

    def test_senza_civico_resta_ok_con_nota(self):
        r = dividi_riga("Via Roma", PSN)
        self.assertEqual((r["stato"], r["civico"]), ("ok", ""))
        self.assertIn("senza civico", r["nota"])

    def test_comune_ripulito(self):
        for scritto in ["PONTE SAN NICOLO'", "Ponte San Nicolò (PD)", "35020 Ponte San Nicolò", "Ponte San Nicolò - PD"]:
            with self.subTest(scritto):
                self.assertEqual(dividi_riga("Via Roma 1", scritto)["comune"], PSN)

    def test_comune_dentro_indirizzo(self):
        r = dividi_riga("Via Roma 12, Padova", "")
        self.assertEqual((r["via"], r["civico"], r["comune"], r["stato"]), ("Via Roma", "12", "Padova", "ok"))
        r = dividi_riga("Via Roma 12, Padova", "Padova (PD)")
        self.assertEqual((r["via"], r["civico"], r["comune"], r["stato"]), ("Via Roma", "12", "Padova", "ok"))
        self.assertEqual(dividi_riga("Via Roma 12, Padova", PSN)["stato"], "rivedere")

    def test_casi_da_rivedere(self):
        self.assertEqual(dividi_riga("Via Roma 12", "")["stato"], "rivedere")
        self.assertEqual(dividi_riga("12 Via Roma", PSN)["stato"], "rivedere")
        self.assertEqual(dividi_riga("Via Roma, Padova", PSN)["stato"], "rivedere")
        r = dividi_riga(PSN, "Via Roma 12")
        self.assertEqual((r["via"], r["civico"], r["comune"], r["stato"]), ("Via Roma", "12", PSN, "rivedere"))

    def test_vuoti(self):
        self.assertEqual(dividi_riga(None, " ")["stato"], "vuoto")
        r = dividi_riga("", "Padova")
        self.assertEqual((r["via"], r["comune"], r["stato"]), ("", "Padova", "ok"))


class Titolo(unittest.TestCase):
    def test_maiuscole(self):
        for testo, atteso in [("VIA DELL'OSPEDALE", "Via dell'Ospedale"), ("via sant'antonio", "Via Sant'Antonio"),
                              ("VIA G.VERDI", "Via G. Verdi"), ("via gabriele d'annunzio", "Via Gabriele D'Annunzio"),
                              ("PONTE DI BRENTA", "Ponte di Brenta"), ("via giovanni xxiii", "Via Giovanni XXIII")]:
            with self.subTest(testo):
                self.assertEqual(titolo(testo), atteso)


class Normalizza(unittest.TestCase):
    def test_stessa_via(self):
        for a, b in [("V. Roma", "via  ROMA"), ("Via Dell'Orto", "via dell orto"), ("Ponte S.Nicolò", "PONTE SAN NICOLO'"),
                     ("P.zza Duomo", "piazza duomo"), ("V.le Europa", "viale europa")]:
            with self.subTest(a):
                self.assertEqual(normalizza(a), normalizza(b))

    def test_vie_diverse(self):
        self.assertNotEqual(normalizza("Via Roma"), normalizza("Viale Roma"))
        self.assertNotEqual(normalizza("Via Roma"), normalizza("Piazza Roma"))


class Unifica(unittest.TestCase):
    @staticmethod
    def righe(*coppie):
        return [dividi_riga(i, c) for i, c in coppie]

    def test_sceglie_la_scrittura_piu_completa(self):
        righe = self.righe(("Via Roma 1", "Ponte S. Nicolò"), ("Via Roma 2", "Ponte S. Nicolò"), ("Via Roma 3", PSN))
        unifica(righe)
        self.assertEqual({r["comune"] for r in righe}, {PSN})

    def test_nome_senza_via_si_aggancia_a_quella_esistente(self):
        righe = self.righe(("Via Roma 1", PSN), ("Roma 2", PSN))
        unifica(righe)
        self.assertEqual((righe[1]["via"], righe[1]["stato"]), ("Via Roma", "ok"))

    def test_nome_senza_via_e_senza_corrispondenza(self):
        righe = self.righe(("Roma 2", PSN))
        unifica(righe)
        self.assertEqual(righe[0]["stato"], "rivedere")

    def test_segnala_i_doppioni_e_non_li_fonde(self):
        righe = self.righe(("Via Garibaldi 1", PSN), ("Via Garibaldi 2", PSN), ("Via Garibladi 3", PSN))
        unifica(righe)
        self.assertEqual(righe[2]["via"], "Via Garibladi")
        self.assertEqual(righe[2]["stato"], "rivedere")
        self.assertIn("Via Garibaldi", righe[2]["nota"])


if __name__ == "__main__":
    unittest.main()
