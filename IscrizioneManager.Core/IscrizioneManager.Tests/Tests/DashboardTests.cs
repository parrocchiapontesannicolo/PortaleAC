using IscrizioneManager.Core.Controllers;
using IscrizioneManager.Core.Logic;
using IscrizioneManager.Core.Models;
using IscrizioneManager.Core.Repositories;

namespace IscrizioneManager.Tests.Tests;

public class DashboardStatsTests
{
    private static Iscrizione Isc(int id, int bambino, int? anno, bool pagato = false, bool caparra = false) =>
        new() { Id = id, IdBambino = bambino, Anno = anno, Pagato = pagato, CaparraPagata = caparra };

    private static readonly List<AnnoScolastico> Anni = new()
    {
        new AnnoScolastico(1, "Prima elementare"),
        new AnnoScolastico(2, "Seconda elementare"),
        new AnnoScolastico(3, "Terza elementare"),
    };

    [Fact]
    public void Calcola_SenzaDati_TuttoAZero()
    {
        var stats = DashboardStats.Calcola(null, null, null, null, null);

        stats.HaIscritti.Should().BeFalse();
        stats.HaSquadre.Should().BeFalse();
        stats.TotaleIscritti.Should().Be(0);
        stats.PercentualePagati.Should().Be(0);
        stats.PerAnno.Should().BeEmpty();
        stats.Squadre.Should().BeEmpty();
        stats.SenzaSquadra.Should().Be(0);
    }

    [Fact]
    public void Calcola_Iscrizioni_TotaliEPagamenti()
    {
        var iscrizioni = new[]
        {
            Isc(1, 10, 1, pagato: true),
            Isc(2, 11, 1, pagato: false, caparra: true),
            Isc(3, 12, 2, pagato: false),
            Isc(4, 13, 3, pagato: true, caparra: true),
        };

        var stats = DashboardStats.Calcola(iscrizioni, Anni, null, null, null);

        stats.HaIscritti.Should().BeTrue();
        stats.HaSquadre.Should().BeFalse();
        stats.TotaleIscritti.Should().Be(4);
        stats.Pagati.Should().Be(2);
        stats.DaPagare.Should().Be(2);
        stats.ConCaparra.Should().Be(1);
        stats.PercentualePagati.Should().Be(50);
    }

    [Fact]
    public void Calcola_StessoBambinoDueVolte_ContatoUnaVolta()
    {
        var iscrizioni = new[] { Isc(5, 10, 1, pagato: true), Isc(9, 10, 2, pagato: false) };

        var stats = DashboardStats.Calcola(iscrizioni, Anni, null, null, null);

        stats.TotaleIscritti.Should().Be(1);
        stats.Pagati.Should().Be(1);
        stats.PerAnno.Should().ContainSingle().Which.Descrizione.Should().Be("Prima elementare");
    }

    [Fact]
    public void Calcola_PerAnno_OrdinatoConNonIndicatoInFondo()
    {
        var iscrizioni = new[]
        {
            Isc(1, 1, 3), Isc(2, 2, null), Isc(3, 3, 1), Isc(4, 4, 3), Isc(5, 5, 99),
        };

        var stats = DashboardStats.Calcola(iscrizioni, Anni, null, null, null);

        stats.PerAnno.Select(a => (a.Descrizione, a.Totale)).Should().Equal(
            ("Prima elementare", 1),
            ("Terza elementare", 2),
            ("Anno 99", 1),
            (DashboardStats.AnnoNonIndicato, 1));
    }

    [Fact]
    public void Calcola_Squadre_ConteggiESenzaSquadra()
    {
        var squadre = new List<Squadra>
        {
            new() { Id = 2, Nome = "Blu", Color = "#0000ff" },
            new() { Id = 1, Nome = "Rossi", Color = "#ff0000" },
            new() { Id = 3, Nome = "Vuota", Color = "" },
        };
        var assegnazioni = new List<SquadraBambino>
        {
            new() { IdSquadra = 1, IdBambino = 10 },
            new() { IdSquadra = 1, IdBambino = 11 },
            new() { IdSquadra = 2, IdBambino = 12 },
            new() { IdSquadra = 2, IdBambino = 999 }, // bambino non più presente
        };

        var stats = DashboardStats.Calcola(null, null, new[] { 10, 11, 12, 13, 14 }, squadre, assegnazioni);

        stats.HaSquadre.Should().BeTrue();
        stats.HaIscritti.Should().BeFalse();
        stats.NumeroSquadre.Should().Be(3);
        stats.Squadre.Select(s => (s.Nome, s.Totale)).Should().Equal(("Rossi", 2), ("Blu", 1), ("Vuota", 0));
        stats.SenzaSquadra.Should().Be(2);
        stats.ColoriSquadre.Should().Equal("#ff0000", "#0000ff");
    }
}

public sealed class DashboardControllerTests : IDisposable
{
    private readonly Mock<IDashboardRepository> _repoMock = new();
    private readonly IDashboardRepository _originale;

    public DashboardControllerTests()
    {
        _originale = DashboardController._repo;
        DashboardController._repo = _repoMock.Object;

        _repoMock.Setup(r => r.GetIscrizioniAsync()).ReturnsAsync(new List<Iscrizione>
        {
            new() { Id = 1, IdBambino = 10, Anno = 1, Pagato = true },
            new() { Id = 2, IdBambino = 11, Anno = 1 },
        });
        _repoMock.Setup(r => r.GetAnniScolasticiAsync()).ReturnsAsync(new List<AnnoScolastico> { new(1, "Prima media") });
        _repoMock.Setup(r => r.GetIdBambiniAsync()).ReturnsAsync(new List<int> { 10, 11 });
        _repoMock.Setup(r => r.GetSquadreAsync()).ReturnsAsync(new List<Squadra> { new() { Id = 1, Nome = "Verdi", Color = "#0a0" } });
        _repoMock.Setup(r => r.GetSquadraBambiniAsync()).ReturnsAsync(new List<SquadraBambino> { new() { IdSquadra = 1, IdBambino = 10 } });
    }

    public void Dispose() => DashboardController._repo = _originale;

    [Fact]
    public async Task CaricaAsync_NessunPermesso_NonInterrogaIlDatabase()
    {
        var stats = await DashboardController.CaricaAsync(iscritti: false, squadre: false);

        stats.HaIscritti.Should().BeFalse();
        stats.HaSquadre.Should().BeFalse();
        _repoMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CaricaAsync_SoloSquadre_NonLeggeIscrizioni()
    {
        var stats = await DashboardController.CaricaAsync(iscritti: false, squadre: true);

        stats.HaSquadre.Should().BeTrue();
        stats.HaIscritti.Should().BeFalse();
        stats.SenzaSquadra.Should().Be(1);
        _repoMock.Verify(r => r.GetIscrizioniAsync(), Times.Never);
        _repoMock.Verify(r => r.GetAnniScolasticiAsync(), Times.Never);
    }

    [Fact]
    public async Task CaricaAsync_SoloIscritti_NonLeggeSquadre()
    {
        var stats = await DashboardController.CaricaAsync(iscritti: true, squadre: false);

        stats.HaIscritti.Should().BeTrue();
        stats.TotaleIscritti.Should().Be(2);
        stats.Pagati.Should().Be(1);
        stats.PerAnno.Should().ContainSingle().Which.Should().Be(new ConteggioAnno(1, "Prima media", 2));
        _repoMock.Verify(r => r.GetSquadreAsync(), Times.Never);
        _repoMock.Verify(r => r.GetSquadraBambiniAsync(), Times.Never);
        _repoMock.Verify(r => r.GetIdBambiniAsync(), Times.Never);
    }

    [Fact]
    public async Task CaricaAsync_ErroreRepository_SiPropaga()
    {
        _repoMock.Setup(r => r.GetSquadreAsync()).ThrowsAsync(new InvalidOperationException("boom"));

        var act = () => DashboardController.CaricaAsync(iscritti: true, squadre: true);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }
}
