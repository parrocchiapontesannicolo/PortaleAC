using IscrizioneManager.Core.Logic;
using IscrizioneManager.Core.Models;
using IscrizioneManager.Core.Models.Admin;
using IscrizioneManager.Core.Security;
using Supabase.Postgrest.Exceptions;

namespace IscrizioneManager.Tests.Tests;

public class AdminLogicaTests
{
    // ------------------------------------------------------------ validazioni

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidaNome_Vuoto_Errore(string? nome)
        => AdminLogica.ValidaNome(nome).Should().NotBeNull();

    [Fact]
    public void ValidaNome_TroppoLungo_Errore()
        => AdminLogica.ValidaNome(new string('a', AdminLogica.NomeMaxLength + 1)).Should().NotBeNull();

    [Fact]
    public void ValidaNome_Valido_Null()
        => AdminLogica.ValidaNome("  Animatori Campo ").Should().BeNull();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567")]
    [InlineData(" spazio iniziale")]
    public void ValidaPassword_NonValida_Errore(string? password)
        => AdminLogica.ValidaPassword(password).Should().NotBeNull();

    [Fact]
    public void ValidaPassword_OttoCaratteri_Valida()
        => AdminLogica.ValidaPassword("12345678").Should().BeNull();

    [Fact]
    public void ValidaPassword_TroppoLunga_Errore()
        => AdminLogica.ValidaPassword(new string('x', AdminLogica.PasswordMaxLength + 1)).Should().NotBeNull();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://drive.google.com/drive/folders/abc")]
    [InlineData("http://example.org")]
    public void ValidaUrl_Valido_Null(string? url)
        => AdminLogica.ValidaUrl(url).Should().BeNull();

    [Theory]
    [InlineData("drive.google.com/abc")]
    [InlineData("ftp://server/file")]
    [InlineData("non un link")]
    public void ValidaUrl_NonValido_Errore(string url)
        => AdminLogica.ValidaUrl(url).Should().NotBeNull();

    // ------------------------------------------------------------ password

    [Fact]
    public void GeneraPassword_RispettaLunghezzaEClassi()
    {
        for (var i = 0; i < 50; i++)
        {
            var pwd = AdminLogica.GeneraPassword(12);
            pwd.Should().HaveLength(12);
            pwd.Should().MatchRegex("[a-z]").And.MatchRegex("[A-Z]").And.MatchRegex("[2-9]");
            pwd.Should().NotContainAny("0", "O", "1", "l", "I");
            AdminLogica.ValidaPassword(pwd).Should().BeNull();
        }
    }

    [Fact]
    public void GeneraPassword_LunghezzaTroppoBassa_UsaMinimo()
        => AdminLogica.GeneraPassword(4).Should().HaveLength(AdminLogica.PasswordMinLength);

    [Fact]
    public void GeneraPassword_ConCasualeDeterministico_Ripetibile()
    {
        var a = AdminLogica.GeneraPassword(10, n => 0);
        var b = AdminLogica.GeneraPassword(10, n => 0);
        a.Should().Be(b);
    }

    // ------------------------------------------------------------ account

    private static AppAccount Acc(string nome, int ordine) => new() { Id = Guid.NewGuid(), Nome = nome, Ordine = ordine };

    [Fact]
    public void Ordina_PerOrdinePoiNome()
    {
        var lista = AdminLogica.Ordina(new[] { Acc("b", 2), Acc("c", 1), Acc("a", 2) });
        lista.Select(a => a.Nome).Should().Equal("c", "a", "b");
    }

    [Fact]
    public void ProssimoOrdine_ListaVuota_Uno()
        => AdminLogica.ProssimoOrdine(Array.Empty<AppAccount>()).Should().Be(1);

    [Fact]
    public void ProssimoOrdine_MassimoPiuUno()
        => AdminLogica.ProssimoOrdine(new[] { Acc("a", 3), Acc("b", 7) }).Should().Be(8);

    [Fact]
    public void Sposta_GiuDiUno_RinumeraSoloICambiati()
    {
        var lista = new List<AppAccount> { Acc("a", 1), Acc("b", 2), Acc("c", 3) };

        var modifiche = AdminLogica.Sposta(lista, 0, 1);

        modifiche.Should().BeEquivalentTo(new[] { (lista[1].Id, 1), (lista[0].Id, 2) });
    }

    [Fact]
    public void Sposta_OrdiniDisordinati_NormalizzaTutti()
    {
        var lista = new List<AppAccount> { Acc("a", 0), Acc("b", 0), Acc("c", 5) };

        var modifiche = AdminLogica.Sposta(lista, 2, -1);

        modifiche.Should().BeEquivalentTo(new[] { (lista[0].Id, 1), (lista[2].Id, 2), (lista[1].Id, 3) });
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(2, 1)]
    [InlineData(5, -1)]
    public void Sposta_FuoriLimiti_NessunaModifica(int indice, int delta)
    {
        var lista = new List<AppAccount> { Acc("a", 1), Acc("b", 2), Acc("c", 3) };
        AdminLogica.Sposta(lista, indice, delta).Should().BeEmpty();
    }

    [Fact]
    public void CalcolaModificheAccessi_AggiungeCambiaRimuove()
    {
        var attuali = new Dictionary<int, int> { [1] = 1, [2] = 2, [3] = 3 };
        var desiderati = new Dictionary<int, int?> { [1] = 1, [2] = 3, [3] = null, [4] = 2, [5] = null };

        var m = AdminLogica.CalcolaModificheAccessi(attuali, desiderati);

        m.DaSalvare.Should().Equal((2, 3), (4, 2));
        m.DaRimuovere.Should().Equal(3);
        m.Vuote.Should().BeFalse();
    }

    [Fact]
    public void CalcolaModificheAccessi_NessunCambio_Vuote()
    {
        var attuali = new Dictionary<int, int> { [1] = 1 };
        var desiderati = new Dictionary<int, int?> { [1] = 1, [2] = null };

        AdminLogica.CalcolaModificheAccessi(attuali, desiderati).Vuote.Should().BeTrue();
    }

    [Fact]
    public void CalcolaModificheAccessi_EventoNonInElenco_NonToccato()
    {
        var attuali = new Dictionary<int, int> { [9] = 1 };
        var desiderati = new Dictionary<int, int?> { [1] = 2 };

        var m = AdminLogica.CalcolaModificheAccessi(attuali, desiderati);

        m.DaRimuovere.Should().BeEmpty();
        m.DaSalvare.Should().Equal((1, 2));
    }

    // ------------------------------------------------------------ permessi

    [Fact]
    public void NormalizzaPermessi_FiltraIgnotiDoppioniEOrdina()
    {
        var res = AdminLogica.NormalizzaPermessi(new[] { Permessi.GoogleDrive, "inventato", Permessi.IscrittiView, Permessi.GoogleDrive });
        res.Should().Equal(Permessi.IscrittiView, Permessi.GoogleDrive);
    }

    [Fact]
    public void NormalizzaPermessi_Null_ListaVuota()
        => AdminLogica.NormalizzaPermessi(null).Should().BeEmpty();

    [Fact]
    public void NotaPermesso_ModificaIscritti_SpiegaSchedaSanitaria()
        => AdminLogica.NotaPermesso(Permessi.IscrittiEdit).Should().Contain("scheda sanitaria");

    [Fact]
    public void GruppiPermessi_CopronoTuttiIPermessi()
        => AdminLogica.GruppiPermessi().SelectMany(g => g).Should().HaveCount(Permessi.Tutti.Count);

    // ------------------------------------------------------------ configurazione evento

    [Fact]
    public void CampiModulo_CopronoTutteLeColonneView()
    {
        var colonne = typeof(EventoMetadata).GetProperties()
            .Where(p => p.Name.StartsWith("View_") && p.PropertyType == typeof(bool))
            .ToList();

        AdminLogica.CampiModulo.Should().HaveCount(colonne.Count);

        // Ogni campo deve leggere e scrivere una colonna diversa
        foreach (var colonna in colonne)
        {
            var m = new EventoMetadata();
            colonna.SetValue(m, true);
            AdminLogica.CampiModulo.Count(c => c.Get(m)).Should().Be(1, colonna.Name);
        }
    }

    [Fact]
    public void CopiaMetadata_CopiaFlagEGradi_NuovoEvento()
    {
        var origine = new EventoMetadata { Id = 7, event_id = 1, View_Taglia = true, View_Caparra = true, GradiScuolaAllowed = "[2,1,2]" };

        var copia = AdminLogica.CopiaMetadata(origine, 5);

        copia.Id.Should().Be(0);
        copia.event_id.Should().Be(5);
        copia.View_Taglia.Should().BeTrue();
        copia.View_Caparra.Should().BeTrue();
        copia.View_Genitori.Should().BeFalse();
        copia.GradiScuolaAllowed.Should().Be("[1,2]");
    }

    [Fact]
    public void MetadataPredefinito_GradiOrdinatiSenzaDoppioni()
    {
        var m = AdminLogica.MetadataPredefinito(3, new[] { 2, 1, 2 });
        m.event_id.Should().Be(3);
        m.GradiScuolaAllowed.Should().Be("[1,2]");
        m.View_Genitori.Should().BeTrue();
    }

    [Theory]
    [InlineData("[1,2]", new[] { 1, 2 })]
    [InlineData("[3, 1]", new[] { 1, 3 })]
    [InlineData("", new int[0])]
    [InlineData(null, new int[0])]
    [InlineData("non json", new int[0])]
    public void ParseGradi(string? json, int[] atteso)
        => AdminLogica.ParseGradi(json).Should().Equal(atteso);

    // ------------------------------------------------------------ errori

    [Fact]
    public void MessaggioDaJson_ErroreFunzione()
        => AdminLogica.MessaggioDaJson("{\"error\":\"Password troppo corta\"}").Should().Be("Password troppo corta");

    [Fact]
    public void MessaggioDaJson_Trigger_RestituisceMessaggio()
        => AdminLogica.MessaggioDaJson("{\"code\":\"P0001\",\"details\":null,\"hint\":null,\"message\":\"Deve rimanere almeno un super amministratore attivo\"}")
            .Should().Be("Deve rimanere almeno un super amministratore attivo");

    [Fact]
    public void MessaggioDaJson_Duplicato_MessaggioItaliano()
        => AdminLogica.MessaggioDaJson("{\"code\":\"23505\",\"message\":\"duplicate key value violates unique constraint\"}")
            .Should().Contain("Esiste già");

    [Fact]
    public void MessaggioDaJson_ChiaveEsterna_MessaggioItaliano()
        => AdminLogica.MessaggioDaJson("{\"code\":\"23503\",\"message\":\"violates foreign key constraint\"}")
            .Should().Contain("collegato");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("testo semplice")]
    [InlineData("{rotto")]
    public void MessaggioDaJson_NonJson_Null(string? testo)
        => AdminLogica.MessaggioDaJson(testo).Should().BeNull();

    [Fact]
    public void MessaggioErrore_PostgrestException_UsaCorpoJson()
    {
        var ex = new PostgrestException("{\"code\":\"P0001\",\"message\":\"Deve rimanere almeno un super amministratore attivo\"}");
        AdminLogica.MessaggioErrore(ex).Should().Be("Deve rimanere almeno un super amministratore attivo");
    }

    [Fact]
    public void MessaggioErrore_AltraEccezione_Messaggio()
        => AdminLogica.MessaggioErrore(new InvalidOperationException("boom")).Should().Be("boom");
}
