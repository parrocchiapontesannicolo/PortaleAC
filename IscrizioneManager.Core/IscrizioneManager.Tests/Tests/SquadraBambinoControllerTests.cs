using IscrizioneManager.Core.Repositories;

namespace IscrizioneManager.Tests.Tests;

public sealed class SquadraBambinoControllerTests : IDisposable
{
    private readonly Mock<ISquadraBambinoRepository> _repoMock = new();
    private readonly ISquadraBambinoRepository _originalRepo;

    public SquadraBambinoControllerTests()
    {
        _originalRepo = SquadraBambinoController._repo;
        SquadraBambinoController._repo = _repoMock.Object;
        _repoMock.Setup(r => r.CreaSquadraAsync(It.IsAny<string>(), It.IsAny<string>()))
                 .ReturnsAsync((string n, string c) => new Squadra { Id = 5, Nome = n, Color = c });
    }

    public void Dispose() => SquadraBambinoController._repo = _originalRepo;

    [Fact]
    public async Task CreaSquadraAsync_TrimsNameAndNormalizesColor()
    {
        var s = await SquadraBambinoController.CreaSquadraAsync("  Leoni ", "#e53");

        s.Nome.Should().Be("Leoni");
        _repoMock.Verify(r => r.CreaSquadraAsync("Leoni", "#EE5533"), Times.Once);
    }

    [Theory]
    [InlineData("", "#FFFFFF")]
    [InlineData("   ", "#FFFFFF")]
    [InlineData(null, "#FFFFFF")]
    [InlineData("Senza squadra", "#FFFFFF")]
    [InlineData("senza SQUADRA", "#FFFFFF")]
    [InlineData("Leoni", "rosso")]
    [InlineData("Leoni", null)]
    public async Task CreaSquadraAsync_InvalidInput_ThrowsAndDoesNotCallRepository(string? nome, string? colore)
    {
        var act = () => SquadraBambinoController.CreaSquadraAsync(nome, colore);

        await act.Should().ThrowAsync<ArgumentException>();
        _repoMock.Verify(r => r.CreaSquadraAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreaSquadraAsync_NameTooLong_Throws()
    {
        var nome = new string('x', SquadraBambinoController.LunghezzaMassimaNome + 1);

        var act = () => SquadraBambinoController.CreaSquadraAsync(nome, "#FFFFFF");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ModificaSquadraAsync_ValidInput_CallsRepository()
    {
        await SquadraBambinoController.ModificaSquadraAsync(3, " Aquile", "#1e88e5");

        _repoMock.Verify(r => r.ModificaSquadraAsync(3, "Aquile", "#1E88E5"), Times.Once);
    }

    [Fact]
    public async Task ModificaSquadraAsync_SenzaSquadra_Throws()
    {
        var act = () => SquadraBambinoController.ModificaSquadraAsync(Squadra.IdSenzaSquadra, "X", "#FFFFFF");

        await act.Should().ThrowAsync<ArgumentException>();
        _repoMock.Verify(r => r.ModificaSquadraAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task EliminaSquadraAsync_SenzaSquadra_Throws()
    {
        var act = () => SquadraBambinoController.EliminaSquadraAsync(Squadra.IdSenzaSquadra);

        await act.Should().ThrowAsync<ArgumentException>();
        _repoMock.Verify(r => r.EliminaSquadraAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task EliminaSquadraAsync_RealSquadra_CallsRepository()
    {
        await SquadraBambinoController.EliminaSquadraAsync(4);

        _repoMock.Verify(r => r.EliminaSquadraAsync(4), Times.Once);
    }
}
