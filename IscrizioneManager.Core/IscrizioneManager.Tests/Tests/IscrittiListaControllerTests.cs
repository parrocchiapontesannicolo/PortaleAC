using IscrizioneManager.Core.Dtos;
using IscrizioneManager.Core.Models;
using IscrizioneManager.Core.Repositories;
using IscrizioniManager.Controllers;
using IscrizioniManager.Models;

namespace IscrizioneManager.Tests.Tests;

public sealed class IscrittiListaControllerTests : IDisposable
{
    private readonly Mock<IIscrittiListaRepository> _repoMock = new();
    private readonly Mock<IEventMetadataRepository> _metaMock = new();
    private readonly IIscrittiListaRepository _originalRepo;
    private readonly IEventMetadataRepository _originalMeta;

    private static readonly List<AnnoScolastico> Anni = new()
    {
        new AnnoScolastico(1, "Prima Media"),
        new AnnoScolastico(2, "Seconda Media"),
    };

    public IscrittiListaControllerTests()
    {
        _originalRepo = IscrittiListaController._repo;
        _originalMeta = IscrizioneCompletaController._metaRepo;
        IscrittiListaController._repo = _repoMock.Object;
        IscrizioneCompletaController._metaRepo = _metaMock.Object;

        _metaMock.Setup(m => m.GetEventMetadataAsync())
                 .ReturnsAsync(new EventoMetadata { GradiScuolaAllowed = "[1,2,3]", View_Caparra = true });
        _metaMock.Setup(m => m.GetAnniScolasticiAsync(It.IsAny<int[]>()))
                 .ReturnsAsync(Anni);
    }

    public void Dispose()
    {
        IscrittiListaController._repo = _originalRepo;
        IscrizioneCompletaController._metaRepo = _originalMeta;
    }

    private static Bambino B(int id, string cognome, string nome, string data = "2010-01-01") =>
        new() { Id = id, Cognome = cognome, Nome = nome, DataNascita = data, event_id = 1 };

    private static Iscrizione I(int id, int idBambino, int? anno = 1, bool pagato = false, bool caparra = false,
        bool? priority = null, string? note = null) =>
        new() { Id = id, IdBambino = idBambino, Anno = anno, Pagato = pagato, CaparraPagata = caparra, Priority = priority, Note = note, event_id = 1 };

    private void SetupDati(List<Bambino> bambini, List<Iscrizione> iscrizioni)
    {
        _repoMock.Setup(r => r.GetBambiniAsync()).ReturnsAsync(bambini);
        _repoMock.Setup(r => r.GetIscrizioniAsync()).ReturnsAsync(iscrizioni);
    }

    [Fact]
    public async Task LoadIscrittiAsync_OneRowPerBambino()
    {
        SetupDati(
            new() { B(1, "Rossi", "Mario"), B(2, "Bianchi", "Luca") },
            new() { I(10, 1), I(20, 2, anno: 2, pagato: true) });

        var result = await IscrittiListaController.LoadIscrittiAsync();

        result.Should().HaveCount(2);
        result.Select(x => x.Cognome).Should().ContainInOrder("Bianchi", "Rossi");
    }

    [Fact]
    public async Task LoadIscrittiAsync_PopulatesIscrizioneFieldsAndAnnoDesc()
    {
        SetupDati(
            new() { B(1, "Rossi", "Mario", "2010-06-15") },
            new() { I(10, 1, anno: 1, pagato: true, caparra: true, priority: true, note: "allergico") });

        var item = (await IscrittiListaController.LoadIscrittiAsync()).Single();

        item.IdBambino.Should().Be(1);
        item.IdIscrizione.Should().Be(10);
        item.AnnoDesc.Should().Be("Prima Media");
        item.Pagato.Should().BeTrue();
        item.CaparraPagata.Should().BeTrue();
        item.Priority.Should().BeTrue();
        item.Note.Should().Be("allergico");
        item.DataNascita.Should().Be(new DateTime(2010, 6, 15));
        item.DataNascitaDesc.Should().Be("15/06/2010");
    }

    [Fact]
    public async Task LoadIscrittiAsync_BambinoWithoutIscrizione_IsKept()
    {
        SetupDati(new() { B(1, "Rossi", "Mario") }, new());

        var item = (await IscrittiListaController.LoadIscrittiAsync()).Single();

        item.IdIscrizione.Should().BeNull();
        item.Pagato.Should().BeFalse();
        item.AnnoDesc.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadIscrittiAsync_EmptyList_ReturnsEmpty()
    {
        SetupDati(new(), new());

        var result = await IscrittiListaController.LoadIscrittiAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CaricaListaAsync_ReturnsMetadataAndAnni()
    {
        SetupDati(new() { B(1, "Rossi", "Mario") }, new() { I(10, 1) });

        var dati = await IscrittiListaController.CaricaListaAsync();

        dati.Metadata.View_Caparra.Should().BeTrue();
        dati.AnniScolastici.Should().HaveCount(2);
        dati.Iscritti.Should().HaveCount(1);
        _metaMock.Verify(m => m.GetAnniScolasticiAsync(It.Is<int[]>(g => g.SequenceEqual(new[] { 1, 2, 3 }))), Times.Once);
    }

    [Fact]
    public void Componi_InvalidDate_LeavesDataNascitaNull()
    {
        var result = IscrittiListaController.Componi(new[] { B(1, "Rossi", "Mario", "non una data") }, Array.Empty<Iscrizione>(), Anni);

        result.Single().DataNascita.Should().BeNull();
        result.Single().DataNascitaDesc.Should().BeEmpty();
    }

    [Fact]
    public async Task AggiornaPagatoAsync_CallsRepository()
    {
        await IscrittiListaController.AggiornaPagatoAsync(10, true);

        _repoMock.Verify(r => r.SetPagatoAsync(10, true), Times.Once);
    }

    [Fact]
    public async Task AggiornaPagatoAsync_InvalidId_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => IscrittiListaController.AggiornaPagatoAsync(0, true));
        _repoMock.Verify(r => r.SetPagatoAsync(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
    }

    private static List<IscrittoListaItem> Esempio() => IscrittiListaController.Componi(
        new[]
        {
            B(1, "Rossi", "Mario", "2010-01-01"),
            B(2, "Bianchi", "Luca", "2011-05-15"),
            B(3, "Verdi", "Nicolò", "2009-03-03"),
            B(4, "Neri", "Anna", "2012-12-12"),
        },
        new[]
        {
            I(10, 1, anno: 1, pagato: true, caparra: true),
            I(20, 2, anno: 2, pagato: false, priority: true, note: "Fratello di Marco"),
            I(30, 3, anno: 1, pagato: false),
            I(40, 4, anno: null, pagato: true),
        },
        Anni);

    [Fact]
    public void Filtra_NoFilter_SortsByCognome()
    {
        var result = IscrittiListaController.Filtra(Esempio(), new FiltroIscritti());

        result.Select(x => x.Cognome).Should().Equal("Bianchi", "Neri", "Rossi", "Verdi");
    }

    [Fact]
    public void Filtra_TextMatchesNameInAnyOrderAndIgnoresCase()
    {
        var result = IscrittiListaController.Filtra(Esempio(), new FiltroIscritti { Testo = "mario ROSSI" });

        result.Should().ContainSingle().Which.IdBambino.Should().Be(1);
    }

    [Fact]
    public void Filtra_TextIgnoresAccents()
    {
        var result = IscrittiListaController.Filtra(Esempio(), new FiltroIscritti { Testo = "nicolo" });

        result.Should().ContainSingle().Which.IdBambino.Should().Be(3);
    }

    [Fact]
    public void Filtra_TextSearchesNoteAnnoAndBirthDate()
    {
        IscrittiListaController.Filtra(Esempio(), new FiltroIscritti { Testo = "marco" })
            .Should().ContainSingle().Which.IdBambino.Should().Be(2);
        IscrittiListaController.Filtra(Esempio(), new FiltroIscritti { Testo = "seconda" })
            .Should().ContainSingle().Which.IdBambino.Should().Be(2);
        IscrittiListaController.Filtra(Esempio(), new FiltroIscritti { Testo = "12/12/2012" })
            .Should().ContainSingle().Which.IdBambino.Should().Be(4);
    }

    [Fact]
    public void Filtra_ByAnno()
    {
        var result = IscrittiListaController.Filtra(Esempio(), new FiltroIscritti { Anno = 1 });

        result.Select(x => x.IdBambino).Should().BeEquivalentTo(new[] { 1, 3 });
    }

    [Fact]
    public void Filtra_ByAnnoNonIndicato()
    {
        var result = IscrittiListaController.Filtra(Esempio(), new FiltroIscritti { Anno = FiltroIscritti.AnnoNonIndicato });

        result.Should().ContainSingle().Which.IdBambino.Should().Be(4);
    }

    [Fact]
    public void Filtra_ByPagatoCaparraPriority()
    {
        IscrittiListaController.Filtra(Esempio(), new FiltroIscritti { Pagato = false })
            .Select(x => x.IdBambino).Should().BeEquivalentTo(new[] { 2, 3 });
        IscrittiListaController.Filtra(Esempio(), new FiltroIscritti { Caparra = true })
            .Should().ContainSingle().Which.IdBambino.Should().Be(1);
        IscrittiListaController.Filtra(Esempio(), new FiltroIscritti { Priority = true })
            .Should().ContainSingle().Which.IdBambino.Should().Be(2);
    }

    [Fact]
    public void Filtra_CombinesFilters()
    {
        var result = IscrittiListaController.Filtra(Esempio(), new FiltroIscritti { Anno = 1, Pagato = true });

        result.Should().ContainSingle().Which.IdBambino.Should().Be(1);
    }

    [Fact]
    public void Filtra_OrderRecenti_ByIdIscrizioneDescending()
    {
        var result = IscrittiListaController.Filtra(Esempio(), new FiltroIscritti { Ordinamento = OrdinamentoIscritti.Recenti });

        result.Select(x => x.IdIscrizione).Should().Equal(40, 30, 20, 10);
    }

    [Fact]
    public void Filtra_OrderAnno_PutsMissingAnnoLast()
    {
        var result = IscrittiListaController.Filtra(Esempio(), new FiltroIscritti { Ordinamento = OrdinamentoIscritti.Anno });

        result.Select(x => x.IdBambino).Should().Equal(1, 3, 2, 4);
    }

    [Fact]
    public void FiltroIscritti_FiltriAttiviAndAzzera()
    {
        var filtro = new FiltroIscritti { Testo = "x", Anno = 1, Pagato = true };
        filtro.FiltriAttivi.Should().Be(3);

        var copia = filtro.Clona();
        filtro.Azzera();

        filtro.FiltriAttivi.Should().Be(0);
        copia.FiltriAttivi.Should().Be(3);
    }

    [Fact]
    public void Riepiloga_CountsTotalsAndPerAnno()
    {
        var riepilogo = IscrittiListaController.Riepiloga(Esempio(), Anni);

        riepilogo.Totale.Should().Be(4);
        riepilogo.Pagati.Should().Be(2);
        riepilogo.DaPagare.Should().Be(2);
        riepilogo.CaparraPagata.Should().Be(1);
        riepilogo.Priority.Should().Be(1);
        riepilogo.PerAnno.Should().Equal(
            new ConteggioAnno(1, "Prima Media", 2),
            new ConteggioAnno(2, "Seconda Media", 1),
            new ConteggioAnno(null, "Non indicato", 1));
    }

    [Fact]
    public void Riepiloga_Empty()
    {
        var riepilogo = IscrittiListaController.Riepiloga(new List<IscrittoListaItem>(), Anni);

        riepilogo.Totale.Should().Be(0);
        riepilogo.PerAnno.Should().BeEmpty();
    }
}
