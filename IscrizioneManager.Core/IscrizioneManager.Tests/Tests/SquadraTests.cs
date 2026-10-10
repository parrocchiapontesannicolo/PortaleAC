using IscrizioniManager.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Supabase.Postgrest;

namespace IscrizioneManager.Tests.Tests;

public class SquadraTests
{
    private static Bambino Bambino(int id, string cognome, string nome, int? anno = null, string? annoDesc = null) =>
        new Bambino { Id = id, Cognome = cognome, Nome = nome, Anno = anno, AnnoDesc = annoDesc };

    [Theory]
    [InlineData("#FFFFFF", "#000000")]
    [InlineData("#FDD835", "#000000")]
    [InlineData("#000000", "#FFFFFF")]
    [InlineData("#1E88E5", "#FFFFFF")]
    [InlineData("#bbb", "#000000")]
    [InlineData("#222", "#FFFFFF")]
    [InlineData("#283593FF", "#FFFFFF")]
    [InlineData("rosso", "#000000")]
    [InlineData("", "#000000")]
    [InlineData(null, "#000000")]
    public void TestoLeggibile_ReturnsBlackOnLightAndWhiteOnDark(string? sfondo, string atteso)
    {
        Squadra.TestoLeggibile(sfondo).Should().Be(atteso);
    }

    [Theory]
    [InlineData("#abc", "#AABBCC")]
    [InlineData("#1e88e5", "#1E88E5")]
    [InlineData(" #1E88E5 ", "#1E88E5")]
    [InlineData("#1E88E5FF", "#1E88E5")]
    [InlineData("1E88E5", null)]
    [InlineData("#12345", null)]
    [InlineData("#GGHHII", null)]
    [InlineData(null, null)]
    public void NormalizzaColore_AcceptsShortLongAndAlphaHex(string? input, string? atteso)
    {
        Squadra.NormalizzaColore(input).Should().Be(atteso);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData(null, true)]
    [InlineData("ros", true)]
    [InlineData("ROSSI", true)]
    [InlineData("mario rossi", true)]
    [InlineData("rossi  mario", true)]
    [InlineData("nicolo", true)]
    [InlineData("rossi luca", false)]
    [InlineData("bianchi", false)]
    public void Corrisponde_MatchesEveryWordInNameOrSurname(string? ricerca, bool atteso)
    {
        var b = Bambino(1, "Rossi", "Mario Nicolò");

        Squadra.Corrisponde(b, ricerca).Should().Be(atteso);
    }

    [Fact]
    public void ConteggiPerAnno_GroupsByAnnoInOrder_WithFallbackDescription()
    {
        var s = new Squadra();
        s.Bambini.AddRange(new[]
        {
            Bambino(1, "A", "A", 2, "Seconda"),
            Bambino(2, "B", "B", 1, "Prima"),
            Bambino(3, "C", "C", 2, "Seconda"),
            Bambino(4, "D", "D"),
        });

        var conteggi = s.ConteggiPerAnno();

        conteggi.Should().Equal(
            new ConteggioAnno(1, "Prima", 1),
            new ConteggioAnno(2, "Seconda", 2),
            new ConteggioAnno(null, "N/D", 1));
    }

    [Fact]
    public void OrdinaBambini_SortsByAnnoThenCognomeThenNome_NullAnnoLast()
    {
        var s = new Squadra();
        s.Bambini.AddRange(new[]
        {
            Bambino(1, "Verdi", "Luca", 2),
            Bambino(2, "Bianchi", "Zoe", null),
            Bambino(3, "Rossi", "Anna", 1),
            Bambino(4, "Rossi", "Aldo", 1),
        });

        s.OrdinaBambini();

        s.Bambini.Select(b => b.Id).Should().Equal(4, 3, 1, 2);
    }

    [Fact]
    public void Serializzazione_Postgrest_InviaSoloLeColonneDellaTabella()
    {
        var settings = Client.SerializerSettings(new ClientOptions());
        ((PostgrestContractResolver)settings.ContractResolver!).SetState(true, false, false);
        var squadra = new Squadra { Id = 3, Nome = "Rossi", Color = "#E53935", event_id = 7 };
        squadra.Bambini.Add(Bambino(1, "A", "A"));

        var json = JObject.Parse(JsonConvert.SerializeObject(squadra, settings));

        json.Properties().Select(p => p.Name).Should().BeEquivalentTo("nome", "color", "event_id");
    }
}
