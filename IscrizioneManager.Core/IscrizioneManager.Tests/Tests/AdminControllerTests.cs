using IscrizioneManager.Core.Models;
using IscrizioneManager.Core.Models.Admin;
using IscrizioneManager.Core.Repositories;
using IscrizioneManager.Core.Security;
using IscrizioniManager.Controllers;
using IscrizioniManager.Models;

namespace IscrizioneManager.Tests.Tests;

public sealed class AdminControllerTests : IDisposable
{
    private readonly Mock<IAdminRepository> _repo = new();
    private readonly IAdminRepository _originale;

    public AdminControllerTests()
    {
        _originale = AdminController._repo;
        AdminController._repo = _repo.Object;
    }

    public void Dispose() => AdminController._repo = _originale;

    private static AppAccount Acc(string nome, int ordine = 1) => new() { Id = Guid.NewGuid(), Nome = nome, Ordine = ordine, Attivo = true };

    // ------------------------------------------------------------ account

    [Fact]
    public async Task CreaAccount_NomeDuplicato_NonChiamaLaFunzione()
    {
        var esistenti = new List<AppAccount> { Acc("Animatori") };

        var act = () => AdminController.CreaAccountAsync(" animatori ", "password123", false, esistenti);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Esiste già*");
        _repo.Verify(r => r.CreaAccountAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreaAccount_PasswordCorta_Errore()
    {
        var act = () => AdminController.CreaAccountAsync("Nuovo", "1234", false, new List<AppAccount>());
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*almeno*");
    }

    [Fact]
    public async Task CreaAccount_Valido_NomePulitoEOrdineInCoda()
    {
        var esistenti = new List<AppAccount> { Acc("A", 4) };
        _repo.Setup(r => r.CreaAccountAsync("Nuovo", "password123", true, 5))
             .ReturnsAsync(new NuovoAccountRisultato { Id = Guid.NewGuid(), Email = "nuovo@x" });

        var res = await AdminController.CreaAccountAsync("  Nuovo ", "password123", true, esistenti);

        res.Email.Should().Be("nuovo@x");
    }

    [Fact]
    public async Task RinominaAccount_StessoNomeDiSeStesso_Consentito()
    {
        var a = Acc("Animatori");
        await AdminController.RinominaAccountAsync(a, "ANIMATORI", new List<AppAccount> { a });

        _repo.Verify(r => r.RinominaAccountAsync(a.Id, "ANIMATORI"), Times.Once);
        a.Nome.Should().Be("ANIMATORI");
    }

    [Fact]
    public async Task EliminaAccount_SeStesso_Rifiutato()
    {
        var id = Guid.NewGuid();
        var act = () => AdminController.EliminaAccountAsync(id, id);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _repo.Verify(r => r.EliminaAccountAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ImpostaAttivo_DisattivareSeStesso_Rifiutato()
    {
        var a = Acc("Io");
        var act = () => AdminController.ImpostaAttivoAsync(a, false, a.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
        a.Attivo.Should().BeTrue();
    }

    [Fact]
    public async Task ImpostaSuperadmin_AltroAccount_AggiornaModello()
    {
        var a = Acc("Altro");
        await AdminController.ImpostaSuperadminAsync(a, true, Guid.NewGuid());

        _repo.Verify(r => r.ImpostaSuperadminAsync(a.Id, true), Times.Once);
        a.IsSuperadmin.Should().BeTrue();
    }

    [Fact]
    public async Task SpostaAccount_AggiornaSoloICambiati()
    {
        var lista = new List<AppAccount> { Acc("a", 1), Acc("b", 2), Acc("c", 3) };

        await AdminController.SpostaAccountAsync(lista, 1, 1);

        _repo.Verify(r => r.ImpostaOrdineAsync(lista[1].Id, 3), Times.Once);
        _repo.Verify(r => r.ImpostaOrdineAsync(lista[2].Id, 2), Times.Once);
        _repo.Verify(r => r.ImpostaOrdineAsync(lista[0].Id, It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SalvaAccessi_ApplicaUpsertERimozioni()
    {
        var id = Guid.NewGuid();
        var attuali = new Dictionary<int, int> { [1] = 1, [2] = 2 };
        var desiderati = new Dictionary<int, int?> { [1] = 2, [2] = null, [3] = 1 };

        var n = await AdminController.SalvaAccessiAsync(id, attuali, desiderati);

        n.Should().Be(3);
        _repo.Verify(r => r.SalvaAccessoAsync(id, 1, 2), Times.Once);
        _repo.Verify(r => r.SalvaAccessoAsync(id, 3, 1), Times.Once);
        _repo.Verify(r => r.RimuoviAccessoAsync(id, 2), Times.Once);
    }

    // ------------------------------------------------------------ ruoli

    [Fact]
    public async Task EliminaRuolo_InUso_Errore()
    {
        _repo.Setup(r => r.GetAccessiAsync()).ReturnsAsync(new List<AccountEvento>
        {
            new() { AccountId = Guid.NewGuid(), EventId = 1, RuoloId = 2 },
            new() { AccountId = Guid.NewGuid(), EventId = 2, RuoloId = 2 },
        });

        var act = () => AdminController.EliminaRuoloAsync(2);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*2 accessi*");
        _repo.Verify(r => r.EliminaRuoloAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task EliminaRuolo_NonUsato_Elimina()
    {
        _repo.Setup(r => r.GetAccessiAsync()).ReturnsAsync(new List<AccountEvento>
        {
            new() { AccountId = Guid.NewGuid(), EventId = 1, RuoloId = 1 },
        });

        await AdminController.EliminaRuoloAsync(2);

        _repo.Verify(r => r.EliminaRuoloAsync(2), Times.Once);
    }

    [Fact]
    public async Task SalvaRuolo_Nuovo_NormalizzaPermessiENonModificaOriginale()
    {
        Ruolo? inviato = null;
        _repo.Setup(r => r.CreaRuoloAsync(It.IsAny<Ruolo>()))
             .Callback<Ruolo>(r => inviato = r)
             .ReturnsAsync((Ruolo r) => r);
        var ruolo = new Ruolo { Nome = " Cuochi ", Descrizione = "  ", Permessi = new() { Permessi.SquadreView, "x", Permessi.IscrittiView } };

        await AdminController.SalvaRuoloAsync(ruolo, new List<Ruolo>());

        inviato!.Nome.Should().Be("Cuochi");
        inviato.Descrizione.Should().BeNull();
        inviato.Permessi.Should().Equal(Permessi.IscrittiView, Permessi.SquadreView);
        ruolo.Permessi.Should().HaveCount(3);
    }

    [Fact]
    public async Task SalvaRuolo_Esistente_Aggiorna()
    {
        var ruolo = new Ruolo { Id = 4, Nome = "Animatore", Permessi = new() { Permessi.SquadreView } };

        await AdminController.SalvaRuoloAsync(ruolo, new List<Ruolo> { ruolo });

        _repo.Verify(r => r.AggiornaRuoloAsync(It.Is<Ruolo>(x => x.Id == 4)), Times.Once);
        _repo.Verify(r => r.CreaRuoloAsync(It.IsAny<Ruolo>()), Times.Never);
    }

    // ------------------------------------------------------------ eventi

    [Fact]
    public async Task CreaEvento_SenzaModello_MetadataPredefinitoConTuttiIGradi()
    {
        _repo.Setup(r => r.CreaEventoAsync("GrEst 2027")).ReturnsAsync(new Evento { Id = 9, Nome = "GrEst 2027", IsActive = 1 });
        _repo.Setup(r => r.GetAnniScolasticiAsync()).ReturnsAsync(new List<AnnoScolastico>
        {
            new() { Id = 1, Desc = "Prima", GradoScuola = 1 },
            new() { Id = 6, Desc = "Prima media", GradoScuola = 2 },
        });
        EventoMetadata? creato = null;
        _repo.Setup(r => r.CreaMetadataAsync(It.IsAny<EventoMetadata>())).Callback<EventoMetadata>(m => creato = m).Returns(Task.CompletedTask);

        var res = await AdminController.CreaEventoAsync("GrEst 2027", null, new List<Evento>());

        res.Evento.Id.Should().Be(9);
        res.Avviso.Should().BeNull();
        creato!.event_id.Should().Be(9);
        creato.GradiScuolaAllowed.Should().Be("[1,2]");
        _repo.Verify(r => r.SalvaTagliaAsync(It.IsAny<TagliaAdmin>()), Times.Never);
    }

    [Fact]
    public async Task CreaEvento_ConModello_CopiaMetadataTaglieEConsensi()
    {
        _repo.Setup(r => r.CreaEventoAsync("Campo")).ReturnsAsync(new Evento { Id = 10, Nome = "Campo", IsActive = 1 });
        _repo.Setup(r => r.GetMetadataAsync(3)).ReturnsAsync(new EventoMetadata { Id = 30, event_id = 3, View_Taglia = true, GradiScuolaAllowed = "[2]" });
        _repo.Setup(r => r.GetTaglieAsync(3)).ReturnsAsync(new List<TagliaAdmin> { new() { Id = 1, EventId = 3, Codice = "M" } });
        _repo.Setup(r => r.GetTipiConsensoAsync(3)).ReturnsAsync(new List<TipoConsensoAdmin> { new() { Id = 1, EventId = 3, Descrizione = "Foto" } });

        await AdminController.CreaEventoAsync("Campo", 3, new List<Evento>());

        _repo.Verify(r => r.CreaMetadataAsync(It.Is<EventoMetadata>(m => m.event_id == 10 && m.Id == 0 && m.View_Taglia && m.GradiScuolaAllowed == "[2]")), Times.Once);
        _repo.Verify(r => r.SalvaTagliaAsync(It.Is<TagliaAdmin>(t => t.EventId == 10 && t.Id == 0 && t.Codice == "M")), Times.Once);
        _repo.Verify(r => r.SalvaTipoConsensoAsync(It.Is<TipoConsensoAdmin>(t => t.EventId == 10 && t.Id == 0 && t.Descrizione == "Foto")), Times.Once);
    }

    [Fact]
    public async Task CreaEvento_ConCopiaAccessi_CopiaSoloQuelliDellEventoModello()
    {
        var acc = Guid.NewGuid();
        _repo.Setup(r => r.CreaEventoAsync("Campo")).ReturnsAsync(new Evento { Id = 10, Nome = "Campo", IsActive = 1 });
        _repo.Setup(r => r.GetMetadataAsync(3)).ReturnsAsync(new EventoMetadata { Id = 30, event_id = 3, GradiScuolaAllowed = "[1]" });
        _repo.Setup(r => r.GetTaglieAsync(3)).ReturnsAsync(new List<TagliaAdmin>());
        _repo.Setup(r => r.GetTipiConsensoAsync(3)).ReturnsAsync(new List<TipoConsensoAdmin>());
        _repo.Setup(r => r.GetAccessiAsync()).ReturnsAsync(new List<AccountEvento>
        {
            new() { AccountId = acc, EventId = 3, RuoloId = 2 },
            new() { AccountId = Guid.NewGuid(), EventId = 4, RuoloId = 1 },
        });

        var res = await AdminController.CreaEventoAsync("Campo", 3, new List<Evento>(), copiaAccessi: true);

        res.Avviso.Should().BeNull();
        _repo.Verify(r => r.SalvaAccessoAsync(acc, 10, 2), Times.Once);
        _repo.Verify(r => r.SalvaAccessoAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task CreaEvento_ConfigurazioneFallita_EventoConAvviso()
    {
        _repo.Setup(r => r.CreaEventoAsync("X")).ReturnsAsync(new Evento { Id = 11, Nome = "X", IsActive = 1 });
        _repo.Setup(r => r.GetAnniScolasticiAsync()).ThrowsAsync(new Exception("rete"));

        var res = await AdminController.CreaEventoAsync("X", null, new List<Evento>());

        res.Evento.Id.Should().Be(11);
        res.Avviso.Should().Contain("creato").And.Contain("rete");
    }

    [Fact]
    public async Task CreaEvento_NomeDuplicato_Errore()
    {
        var act = () => AdminController.CreaEventoAsync("grest", null, new List<Evento> { new() { Id = 1, Nome = "GrEst" } });
        await act.Should().ThrowAsync<ArgumentException>();
        _repo.Verify(r => r.CreaEventoAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SalvaMetadata_Nuovo_CreaERilegge()
    {
        var m = new EventoMetadata();
        _repo.Setup(r => r.GetMetadataAsync(4)).ReturnsAsync(new EventoMetadata { Id = 40, event_id = 4 });

        var res = await AdminController.SalvaMetadataAsync(4, m, new[] { 3, 1 });

        _repo.Verify(r => r.CreaMetadataAsync(It.Is<EventoMetadata>(x => x.event_id == 4 && x.GradiScuolaAllowed == "[1,3]")), Times.Once);
        res.Id.Should().Be(40);
    }

    [Fact]
    public async Task SalvaMetadata_Esistente_Aggiorna()
    {
        var m = new EventoMetadata { Id = 5 };
        await AdminController.SalvaMetadataAsync(4, m, new[] { 1 });
        _repo.Verify(r => r.AggiornaMetadataAsync(m), Times.Once);
    }

    // ------------------------------------------------------------ link Google

    [Fact]
    public async Task SalvaLink_Nuovo_Crea()
    {
        await AdminController.SalvaLinkAsync(1, UrlTypes.GoogleDrive, " https://drive.google.com/x ", new List<LinkGoogleAdmin>());

        _repo.Verify(r => r.CreaLinkAsync(It.Is<LinkGoogleAdmin>(l => l.EventId == 1 && l.UrlType == 2 && l.Url == "https://drive.google.com/x")), Times.Once);
    }

    [Fact]
    public async Task SalvaLink_Esistente_AggiornaEDeduplica()
    {
        var esistenti = new List<LinkGoogleAdmin>
        {
            new() { Id = 3, EventId = 1, UrlType = 1, Url = "https://old" },
            new() { Id = 8, EventId = 1, UrlType = 1, Url = "https://dup" },
            new() { Id = 9, EventId = 1, UrlType = 2, Url = "https://drive" },
        };

        await AdminController.SalvaLinkAsync(1, UrlTypes.GoogleSheet, "https://new", esistenti);

        _repo.Verify(r => r.AggiornaLinkAsync(3, "https://new"), Times.Once);
        _repo.Verify(r => r.EliminaLinkAsync(8), Times.Once);
        _repo.Verify(r => r.EliminaLinkAsync(9), Times.Never);
    }

    [Fact]
    public async Task SalvaLink_Vuoto_EliminaQuelliDelTipo()
    {
        var esistenti = new List<LinkGoogleAdmin> { new() { Id = 3, EventId = 1, UrlType = 2, Url = "https://drive" } };

        await AdminController.SalvaLinkAsync(1, UrlTypes.GoogleDrive, "  ", esistenti);

        _repo.Verify(r => r.EliminaLinkAsync(3), Times.Once);
        _repo.Verify(r => r.CreaLinkAsync(It.IsAny<LinkGoogleAdmin>()), Times.Never);
    }

    [Fact]
    public async Task SalvaLink_UrlNonValido_Errore()
    {
        var act = () => AdminController.SalvaLinkAsync(1, UrlTypes.GoogleDrive, "drive.google.com", new List<LinkGoogleAdmin>());
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ------------------------------------------------------------ settimane, taglie, consensi

    [Fact]
    public async Task SalvaSettimana_FinePrimaDellInizio_Errore()
    {
        var s = new SettimanaAdmin { EventId = 1, DataInizio = new DateTime(2027, 6, 10), DataFine = new DateTime(2027, 6, 9) };
        var act = () => AdminController.SalvaSettimanaAsync(s);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SalvaTaglia_CodiceVuoto_Errore()
    {
        var act = () => AdminController.SalvaTagliaAsync(new TagliaAdmin { EventId = 1, Codice = " " });
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SalvaTipoConsenso_TestoPulito()
    {
        var t = new TipoConsensoAdmin { EventId = 1, Descrizione = "  Foto  " };
        await AdminController.SalvaTipoConsensoAsync(t);
        _repo.Verify(r => r.SalvaTipoConsensoAsync(It.Is<TipoConsensoAdmin>(x => x.Descrizione == "Foto")), Times.Once);
    }
}
