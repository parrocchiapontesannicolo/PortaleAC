using IscrizioneManager.Core.Logic;
using IscrizioniManager.Models;

namespace IscrizioneManager.Tests.Tests;

public class DistribuisciSquadreTests
{
    private static IEnumerable<Bambino> NoShuffle(IEnumerable<Bambino> b) => b;

    private static Bambino Bambino(int id, string cognome, string nome, int? anno) =>
        new Bambino { Id = id, Cognome = cognome, Nome = nome, Anno = anno };

    private static Squadra Squadra(int id, string nome, params Bambino[] bambini)
    {
        var s = new Squadra { Id = id, Nome = nome };
        s.Bambini.AddRange(bambini);
        return s;
    }

    [Fact]
    public void Distribuisci_EvenCount_EachSquadraGetsSameNumber()
    {
        var bambini = Enumerable.Range(1, 6).Select(i => Bambino(i, $"C{i}", $"N{i}", 1)).ToList();
        var squadre = new List<Squadra>
        {
            Squadra(1, "Alpha"),
            Squadra(2, "Beta"),
            Squadra(0, "Senza squadra", bambini.ToArray())
        };

        SquadraDistributor.Distribuisci(squadre, NoShuffle);

        squadre.Where(s => s.Id != 0).Should().AllSatisfy(s => s.Bambini.Should().HaveCount(3));
        squadre.Single(s => s.Id == 0).Bambini.Should().BeEmpty();
    }

    [Fact]
    public void Distribuisci_UnevenCount_DiffersByAtMostOne()
    {
        var bambini = Enumerable.Range(1, 5).Select(i => Bambino(i, $"C{i}", $"N{i}", 1)).ToList();
        var squadre = new List<Squadra>
        {
            Squadra(1, "Alpha"),
            Squadra(2, "Beta"),
            Squadra(3, "Gamma"),
            Squadra(0, "Senza squadra", bambini.ToArray())
        };

        SquadraDistributor.Distribuisci(squadre, NoShuffle);

        var counts = squadre.Where(s => s.Id != 0).Select(s => s.Bambini.Count).ToList();
        counts.Sum().Should().Be(5);
        (counts.Max() - counts.Min()).Should().BeLessOrEqualTo(1);
    }

    [Fact]
    public void Distribuisci_TwoAnnoGroups_EachSquadraContainsAtLeastOneOfEachAnno()
    {
        var bambini = new List<Bambino>
        {
            Bambino(1, "A", "A", 1), Bambino(2, "B", "B", 1),
            Bambino(3, "C", "C", 2), Bambino(4, "D", "D", 2),
        };
        var squadre = new List<Squadra>
        {
            Squadra(1, "Alpha"),
            Squadra(2, "Beta"),
            Squadra(0, "Senza squadra", bambini.ToArray())
        };

        SquadraDistributor.Distribuisci(squadre, NoShuffle);

        foreach (var s in squadre.Where(s => s.Id != 0))
        {
            s.Bambini.Select(b => b.Anno).Should().Contain(1);
            s.Bambini.Select(b => b.Anno).Should().Contain(2);
        }
    }

    [Fact]
    public void Distribuisci_AllBambiniInSenzaSquadra_MovedToRealSquadre()
    {
        var bambini = Enumerable.Range(1, 4).Select(i => Bambino(i, $"C{i}", $"N{i}", 1)).ToList();
        var squadre = new List<Squadra>
        {
            Squadra(1, "Alpha"),
            Squadra(0, "Senza squadra", bambini.ToArray())
        };

        SquadraDistributor.Distribuisci(squadre, NoShuffle);

        squadre.Single(s => s.Id == 1).Bambini.Should().HaveCount(4);
        squadre.Single(s => s.Id == 0).Bambini.Should().BeEmpty();
    }

    [Fact]
    public void Distribuisci_NoBambini_DoesNotThrow()
    {
        var squadre = new List<Squadra>
        {
            Squadra(1, "Alpha"),
            Squadra(0, "Senza squadra")
        };

        var act = () => SquadraDistributor.Distribuisci(squadre, NoShuffle);

        act.Should().NotThrow();
        squadre.Single(s => s.Id == 1).Bambini.Should().BeEmpty();
    }

    [Fact]
    public void Distribuisci_ResultsAreSortedLikeTheLoadedList_ByAnnoThenByName()
    {
        var bambini = new List<Bambino>
        {
            Bambino(1, "Zanetti", "A", 2),
            Bambino(2, "Alberini", "B", 1),
            Bambino(3, "Bianchi", "C", 2),
        };
        var squadre = new List<Squadra>
        {
            Squadra(1, "Alpha"),
            Squadra(0, "Senza squadra", bambini.ToArray())
        };

        SquadraDistributor.Distribuisci(squadre, NoShuffle);

        var ordered = squadre.Single(s => s.Id == 1).Bambini;
        ordered.Select(b => b.Cognome).Should().Equal("Alberini", "Bianchi", "Zanetti");
    }

    [Fact]
    public void Distribuisci_NoRealSquadre_LeavesBambiniInSenzaSquadra()
    {
        var bambini = Enumerable.Range(1, 3).Select(i => Bambino(i, $"C{i}", $"N{i}", 1)).ToArray();
        var squadre = new List<Squadra> { Squadra(0, "Senza squadra", bambini) };

        var act = () => SquadraDistributor.Distribuisci(squadre, NoShuffle);

        act.Should().NotThrow();
        squadre.Single().Bambini.Should().HaveCount(3);
    }

    [Fact]
    public void Distribuisci_BambinoDuplicatedInTwoSquadre_IsAssignedOnlyOnce()
    {
        var doppio = Bambino(1, "A", "A", 1);
        var squadre = new List<Squadra>
        {
            Squadra(1, "Alpha", doppio),
            Squadra(2, "Beta", doppio, Bambino(2, "B", "B", 1)),
            Squadra(0, "Senza squadra")
        };

        SquadraDistributor.Distribuisci(squadre, NoShuffle);

        squadre.SelectMany(s => s.Bambini).Select(b => b.Id).Should().OnlyHaveUniqueItems().And.HaveCount(2);
    }

    [Fact]
    public void Distribuisci_MixedAnni_EachAnnoIsBalancedAcrossSquadre()
    {
        var bambini = new List<Bambino>();
        for (int i = 1; i <= 6; i++) bambini.Add(Bambino(i, $"A{i}", "x", 1));
        for (int i = 7; i <= 9; i++) bambini.Add(Bambino(i, $"B{i}", "x", 2));
        var squadre = new List<Squadra>
        {
            Squadra(1, "Alpha"), Squadra(2, "Beta"), Squadra(3, "Gamma"),
            Squadra(0, "Senza squadra", bambini.ToArray())
        };

        SquadraDistributor.Distribuisci(squadre, NoShuffle);

        foreach (var s in squadre.Where(s => s.Id != 0))
        {
            s.Bambini.Count(b => b.Anno == 1).Should().Be(2);
            s.Bambini.Count(b => b.Anno == 2).Should().Be(1);
        }
    }

    [Fact]
    public void Sposta_MovesBambinoAndKeepsDestinationSorted()
    {
        var squadre = new List<Squadra>
        {
            Squadra(1, "Alpha", Bambino(1, "Rossi", "Mario", 1)),
            Squadra(2, "Beta", Bambino(2, "Bianchi", "Anna", 1), Bambino(3, "Verdi", "Luca", 1)),
            Squadra(0, "Senza squadra")
        };

        var spostato = SquadraDistributor.Sposta(squadre, 1, 1, 2);

        spostato!.Id.Should().Be(1);
        squadre.Single(s => s.Id == 1).Bambini.Should().BeEmpty();
        squadre.Single(s => s.Id == 2).Bambini.Select(b => b.Cognome).Should().Equal("Bianchi", "Rossi", "Verdi");
    }

    [Fact]
    public void Sposta_ToSenzaSquadra_Works()
    {
        var squadre = new List<Squadra>
        {
            Squadra(1, "Alpha", Bambino(1, "Rossi", "Mario", 1)),
            Squadra(0, "Senza squadra")
        };

        SquadraDistributor.Sposta(squadre, 1, 1, 0).Should().NotBeNull();

        squadre.Single(s => s.Id == 0).Bambini.Should().ContainSingle(b => b.Id == 1);
    }

    [Theory]
    [InlineData(1, 1, 1)]   // stessa squadra
    [InlineData(99, 1, 2)]  // bambino inesistente
    [InlineData(1, 1, 99)]  // destinazione inesistente
    [InlineData(1, 2, 0)]   // bambino non presente nella squadra di origine
    public void Sposta_InvalidRequest_ReturnsNullAndChangesNothing(int bambinoId, int da, int a)
    {
        var squadre = new List<Squadra>
        {
            Squadra(1, "Alpha", Bambino(1, "Rossi", "Mario", 1)),
            Squadra(2, "Beta"),
            Squadra(0, "Senza squadra")
        };

        SquadraDistributor.Sposta(squadre, bambinoId, da, a).Should().BeNull();

        squadre.Single(s => s.Id == 1).Bambini.Should().HaveCount(1);
        squadre.Single(s => s.Id == 2).Bambini.Should().BeEmpty();
    }

    [Fact]
    public void Assegnazioni_ExcludesSenzaSquadraAndSetsEventId()
    {
        var squadre = new List<Squadra>
        {
            Squadra(1, "Alpha", Bambino(1, "A", "A", 1), Bambino(2, "B", "B", 1)),
            Squadra(2, "Beta", Bambino(3, "C", "C", 1)),
            Squadra(0, "Senza squadra", Bambino(4, "D", "D", 1))
        };

        var righe = SquadraDistributor.Assegnazioni(squadre, eventId: 7);

        righe.Select(r => (r.IdSquadra, r.IdBambino)).Should().BeEquivalentTo(new[] { (1, 1), (1, 2), (2, 3) });
        righe.Should().OnlyContain(r => r.event_id == 7);
    }

    [Fact]
    public void Assegnazioni_BambinoInTwoSquadre_KeepsOnlyTheFirst()
    {
        var doppio = Bambino(1, "A", "A", 1);
        var squadre = new List<Squadra>
        {
            Squadra(1, "Alpha", doppio),
            Squadra(2, "Beta", doppio),
        };

        var righe = SquadraDistributor.Assegnazioni(squadre, eventId: 1);

        righe.Should().ContainSingle().Which.IdSquadra.Should().Be(1);
    }

    [Fact]
    public void Distribuisci_BambiniAlreadyInRealSquadra_AlsoRedistributed()
    {
        var squadre = new List<Squadra>
        {
            Squadra(1, "Alpha", Bambino(1, "A", "A", 1), Bambino(2, "B", "B", 1)),
            Squadra(2, "Beta",  Bambino(3, "C", "C", 1), Bambino(4, "D", "D", 1)),
            Squadra(0, "Senza squadra")
        };

        SquadraDistributor.Distribuisci(squadre, NoShuffle);

        squadre.Where(s => s.Id != 0).SelectMany(s => s.Bambini).Should().HaveCount(4);
    }

    private static BambinoSquadre Riga(int id, string cognome, string nome, int? anno) =>
        new() { IdBambino = id, Cognome = cognome, Nome = nome, Anno = anno };

    [Fact]
    public void Componi_AssegnaIBambiniEAggiungeSenzaSquadraInFondo()
    {
        var squadre = new[] { new Squadra { Id = 1, Nome = "Rossi" }, new Squadra { Id = 2, Nome = "Blu" } };
        var righe = new[]
        {
            Riga(10, "Verdi", "Anna", 2),
            Riga(11, "Bianchi", "Luca", 1),
            Riga(12, "Neri", "Sara", null),
            Riga(13, "Gialli", "Marco", 1),
            Riga(14, "Viola", "Ugo", 3),
        };
        var assegnazioni = new[]
        {
            new SquadraBambino { IdSquadra = 1, IdBambino = 10 },
            new SquadraBambino { IdSquadra = 1, IdBambino = 11 },
            new SquadraBambino { IdSquadra = 2, IdBambino = 12 },
            new SquadraBambino { IdSquadra = 99, IdBambino = 14 }, // squadra non più esistente
        };
        var anni = new Dictionary<int, string> { [1] = "Prima", [2] = "Seconda" };

        var risultato = SquadraDistributor.Componi(squadre, righe, assegnazioni, anni);

        risultato.Select(s => s.Id).Should().Equal(1, 2, global::Squadra.IdSenzaSquadra);
        risultato[0].Bambini.Select(b => (b.Id, b.AnnoDesc)).Should().Equal((11, "Prima"), (10, "Seconda"));
        risultato[1].Bambini.Should().ContainSingle().Which.AnnoDesc.Should().BeEmpty();
        risultato[2].Nome.Should().Be(global::Squadra.NomeSenzaSquadra);
        risultato[2].Bambini.Select(b => b.Id).Should().Equal(13, 14);
        risultato[2].Bambini[1].AnnoDesc.Should().BeEmpty("l'anno 3 non ha una descrizione");
    }

    [Fact]
    public void Componi_BambinoDuplicatoONomiNulli_UnaRigaSolaSenzaNull()
    {
        var righe = new[]
        {
            new BambinoSquadre { IdBambino = 5, Nome = null, Cognome = null, Anno = null },
            Riga(5, "Doppio", "Doppio", 1),
        };

        var risultato = SquadraDistributor.Componi(
            Array.Empty<Squadra>(), righe, Array.Empty<SquadraBambino>(), new Dictionary<int, string>());

        var senza = risultato.Should().ContainSingle().Subject;
        senza.Bambini.Should().ContainSingle().Which.Nome.Should().BeEmpty();
    }

    [Fact]
    public void BambinoSquadre_DeserializzaLeColonneDellaRpc()
    {
        const string json = """[{"id_bambino":7,"nome":"Anna","cognome":"Rossi","anno":2},{"id_bambino":8,"nome":"Ugo","cognome":"Neri","anno":null}]""";

        var righe = Newtonsoft.Json.JsonConvert.DeserializeObject<List<BambinoSquadre>>(json)!;

        righe.Should().HaveCount(2);
        righe[0].Should().BeEquivalentTo(new BambinoSquadre { IdBambino = 7, Nome = "Anna", Cognome = "Rossi", Anno = 2 });
        righe[1].Anno.Should().BeNull();
    }
}
