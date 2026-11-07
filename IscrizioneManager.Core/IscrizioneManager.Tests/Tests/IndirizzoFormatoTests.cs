using IscrizioneManager.Core.Models;
using IscrizioneManager.Core.Utils;

namespace IscrizioneManager.Tests.Tests;

public class IndirizzoFormatoTests
{
    private static readonly List<VoceIndirizzo> Elenco = new()
    {
        new() { IdComune = 1, Comune = "Ponte San Nicolò", IdVia = 10, Via = "Via Roma" },
        new() { IdComune = 1, Comune = "Ponte San Nicolò", IdVia = 11, Via = "Via dell'Orto" },
        new() { IdComune = 2, Comune = "Padova", IdVia = 20, Via = "Via Roma" },
        new() { IdComune = 2, Comune = "Padova", IdVia = 21, Via = "Piazza Duomo" },
        new() { IdComune = 3, Comune = "Legnaro" },
    };

    [Theory]
    [InlineData("Via Roma 12", "Via Roma", "12")]
    [InlineData("via roma, 12", "Via Roma", "12")]
    [InlineData("VIA ROMA N. 12", "Via Roma", "12")]
    [InlineData("Via Roma 12/a", "Via Roma", "12/A")]
    [InlineData("Via Roma 12 A", "Via Roma", "12A")]
    [InlineData("Via Roma 12 bis", "Via Roma", "12 bis")]
    [InlineData("Via Roma 12-14", "Via Roma", "12-14")]
    [InlineData("Via Roma s.n.c.", "Via Roma", "snc")]
    [InlineData("Via Roma 12 int. 3", "Via Roma", "12 int. 3")]
    [InlineData("Via 4 Novembre 12", "Via 4 Novembre", "12")]
    [InlineData("Via XX Settembre, 3", "Via XX Settembre", "3")]
    [InlineData("Via Roma", "Via Roma", null)]
    [InlineData("Via 4 Novembre", "Via 4 Novembre", null)]
    [InlineData("V. Roma 1", "Via Roma", "1")]
    [InlineData("V.le Europa 1", "Viale Europa", "1")]
    [InlineData("P.zza Liberta' 1", "Piazza Libertà", "1")]
    [InlineData("C.so Italia 1", "Corso Italia", "1")]
    public void Dividi_separa_via_e_civico(string testo, string via, string? civico)
    {
        IndirizzoFormato.Dividi(testo).Should().Be((via, civico));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Dividi_testo_vuoto(string? testo)
    {
        IndirizzoFormato.Dividi(testo).Should().Be(((string?)null, (string?)null));
    }

    [Theory]
    [InlineData("VIA DELL'OSPEDALE", "Via dell'Ospedale")]
    [InlineData("via sant'antonio", "Via Sant'Antonio")]
    [InlineData("VIA G.VERDI", "Via G. Verdi")]
    [InlineData("via gabriele d'annunzio", "Via Gabriele D'Annunzio")]
    [InlineData("PONTE DI BRENTA", "Ponte di Brenta")]
    [InlineData("via giovanni xxiii", "Via Giovanni XXIII")]
    [InlineData("PONTE SAN NICOLO'", "Ponte San Nicolò")]
    public void Titolo_uniforma_le_maiuscole(string testo, string atteso)
    {
        IndirizzoFormato.Titolo(testo).Should().Be(atteso);
    }

    [Fact]
    public void PerSalvataggio_stacca_il_numero_scritto_nella_via()
    {
        IndirizzoFormato.PerSalvataggio("padova", "via roma 12", null)
            .Should().Be(new IndirizzoDiviso("Padova", "Via Roma", "12"));
    }

    [Fact]
    public void PerSalvataggio_rispetta_il_civico_scritto_a_parte()
    {
        // Il civico c'è già: il 4 fa parte del nome e la via non va riletta
        IndirizzoFormato.PerSalvataggio("Padova", "via 4", "7/b")
            .Should().Be(new IndirizzoDiviso("Padova", "Via 4", "7/B"));
    }

    [Theory]
    [InlineData("Padova (PD)")]
    [InlineData("Padova - PD")]
    [InlineData("35100 Padova")]
    [InlineData("PADOVA")]
    public void PerSalvataggio_ripulisce_il_comune(string comune)
    {
        IndirizzoFormato.PerSalvataggio(comune, null, null).Comune.Should().Be("Padova");
    }

    [Fact]
    public void PerSalvataggio_tutto_vuoto_svuota_i_campi()
    {
        IndirizzoFormato.PerSalvataggio(" ", "", null).Should().Be(new IndirizzoDiviso(null, null, null));
    }

    [Fact]
    public void DaBambino_collegato_usa_l_anagrafica()
    {
        IndirizzoFormato.DaBambino("testo vecchio 1", "altro", 21, "3", Elenco)
            .Should().Be(new IndirizzoDiviso("Padova", "Piazza Duomo", "3"));
    }

    [Fact]
    public void DaBambino_non_collegato_divide_il_vecchio_testo()
    {
        IndirizzoFormato.DaBambino("VIA ROMA 12", "Padova", null, null, Elenco)
            .Should().Be(new IndirizzoDiviso("Padova", "Via Roma", "12"));
    }

    [Fact]
    public void DaBambino_via_assente_dall_elenco_non_perde_il_testo()
    {
        IndirizzoFormato.DaBambino("Via Roma 12", "Padova", 999, "12", Elenco)
            .Should().Be(new IndirizzoDiviso("Padova", "Via Roma", "12"));
    }

    [Fact]
    public void SuggerisciComuni_ignora_accenti_e_non_ripete()
    {
        IndirizzoFormato.SuggerisciComuni(Elenco, "nicolo").Should().Equal("Ponte San Nicolò");
        IndirizzoFormato.SuggerisciComuni(Elenco, "").Should().Equal("Legnaro", "Padova", "Ponte San Nicolò");
    }

    [Fact]
    public void SuggerisciVie_solo_del_comune_scelto()
    {
        IndirizzoFormato.SuggerisciVie(Elenco, "PADOVA", "").Should().Equal("Piazza Duomo", "Via Roma");
        IndirizzoFormato.SuggerisciVie(Elenco, "Ponte San Nicolò", "orto").Should().Equal("Via dell'Orto");
    }

    [Fact]
    public void SuggerisciVie_senza_comune_tutte_una_volta_sola()
    {
        IndirizzoFormato.SuggerisciVie(Elenco, null, "roma").Should().Equal("Via Roma");
    }

    private static ModuloIscrizioneDto Modulo(string? comune, string? via, string? civico) => new()
    {
        Cognome = "Rossi",
        Nome = "Mario",
        DataNascita = new DateTime(2015, 1, 1),
        Genere = 1,
        ComuneResidenza = comune,
        ViaResidenza = via,
        CivicoResidenza = civico,
    };

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("Padova", null, null)]
    [InlineData("Padova", "Via Roma", null)]
    [InlineData("Padova", "Via Roma", "12")]
    public void Validatore_accetta_indirizzi_coerenti(string? comune, string? via, string? civico)
    {
        ModuloIscrizioneValidator.Validate(Modulo(comune, via, civico)).Should().BeEmpty();
    }

    [Fact]
    public void Validatore_via_senza_comune()
    {
        ModuloIscrizioneValidator.Validate(Modulo(null, "Via Roma", "1")).Should().Contain("comune");
    }

    [Fact]
    public void Validatore_civico_senza_via()
    {
        ModuloIscrizioneValidator.Validate(Modulo("Padova", " ", "1")).Should().Contain("via");
    }

    [Fact]
    public void SuggerisciVie_comune_nuovo_nessun_suggerimento()
    {
        IndirizzoFormato.SuggerisciVie(Elenco, "Comune Nuovo", "").Should().BeEmpty();
    }
}
