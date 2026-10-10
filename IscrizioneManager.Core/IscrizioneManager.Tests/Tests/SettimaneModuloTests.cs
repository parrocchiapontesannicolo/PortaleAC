namespace IscrizioneManager.Tests.Tests;

public class SettimaneModuloTests
{
    private static Settimana S(int id, string desc, decimal? intero, decimal? baseCosto, int giorno = 1) => new()
    {
        Id = id,
        Desc = desc,
        CostoIntero = intero,
        CostoBase = baseCosto,
        DataInizio = new DateTime(2026, 6, giorno),
        DataFine = new DateTime(2026, 6, giorno + 4),
    };

    [Fact]
    public void Espandi_CreatesVariantsWithSignedIds()
    {
        var result = SettimaneModulo.Espandi(new[] { S(3, "Settimana 1", 60, 45) });

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(3);
        result[0].Desc.Should().Be("Settimana 1 (Con pranzo)");
        result[0].CostoIntero.Should().Be(60);
        result[0].CostoBase.Should().BeNull();
        result[1].Id.Should().Be(-3);
        result[1].Desc.Should().Be("Settimana 1 (Senza pranzo)");
        result[1].CostoIntero.Should().BeNull();
        result[1].CostoBase.Should().Be(45);
        result.Should().OnlyContain(s => s.DescBase == "Settimana 1" && !s.IsSelected);
    }

    [Fact]
    public void Espandi_OnlyAvailableVariants()
    {
        var result = SettimaneModulo.Espandi(new[] { S(1, "Solo intera", 60, null), S(2, "Solo base", null, 40, 8) });

        result.Select(s => s.Id).Should().Equal(1, -2);
    }

    [Fact]
    public void Espandi_OrdersByStartDate()
    {
        var result = SettimaneModulo.Espandi(new[] { S(9, "Seconda", 60, null, 8), S(4, "Prima", 60, null, 1) });

        result.Select(s => s.DescBase).Should().Equal("Prima", "Seconda");
    }

    [Fact]
    public void Espandi_MarksChosenVariants()
    {
        var scelte = new[]
        {
            new IscrizioneSettimana { IdSettimana = 1, Intero = true },
            new IscrizioneSettimana { IdSettimana = 2, Intero = false },
        };

        var result = SettimaneModulo.Espandi(new[] { S(1, "A", 60, 45), S(2, "B", 60, 45, 8) }, scelte);

        result.Where(s => s.IsSelected).Select(s => s.Id).Should().Equal(1, -2);
    }

    [Fact]
    public void Espandi_KeepsChosenVariantWithoutCost()
    {
        // La variante "con pranzo" era stata scelta ma poi il costo è stato tolto
        var scelte = new[] { new IscrizioneSettimana { IdSettimana = 1, Intero = true } };

        var result = SettimaneModulo.Espandi(new[] { S(1, "A", null, 45) }, scelte);

        var intera = result.Single(s => s.Id == 1);
        intera.IsSelected.Should().BeTrue();
        SettimaneModulo.IsConPranzo(intera).Should().BeTrue();
    }

    [Fact]
    public void Seleziona_DeselectsOtherVariantOfSameWeek()
    {
        var settimane = SettimaneModulo.Espandi(new[] { S(1, "A", 60, 45), S(2, "B", 60, 45, 8) });
        var conPranzoA = settimane.Single(s => s.Id == 1);
        var senzaPranzoA = settimane.Single(s => s.Id == -1);
        var conPranzoB = settimane.Single(s => s.Id == 2);

        SettimaneModulo.Seleziona(settimane, conPranzoA, true);
        SettimaneModulo.Seleziona(settimane, conPranzoB, true);
        SettimaneModulo.Seleziona(settimane, senzaPranzoA, true);

        conPranzoA.IsSelected.Should().BeFalse();
        senzaPranzoA.IsSelected.Should().BeTrue();
        conPranzoB.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void Seleziona_False_OnlyDeselects()
    {
        var settimane = SettimaneModulo.Espandi(new[] { S(1, "A", 60, 45) });
        settimane[0].IsSelected = true;

        SettimaneModulo.Seleziona(settimane, settimane[0], false);

        settimane.Should().OnlyContain(s => !s.IsSelected);
    }

    [Fact]
    public void Totale_SumsSelectedVariants()
    {
        var settimane = SettimaneModulo.Espandi(new[] { S(1, "A", 60, 45), S(2, "B", 70, 50, 8) });
        settimane.Single(s => s.Id == 1).IsSelected = true;
        settimane.Single(s => s.Id == -2).IsSelected = true;

        SettimaneModulo.Totale(settimane).Should().Be(110);
        new ModuloIscrizioneDto { Settimane = settimane }.TotaleSettimane.Should().Be(110);
        SettimaneModulo.Totale(null).Should().Be(0);
    }

    [Fact]
    public void Raggruppa_GroupsVariantsByWeek()
    {
        var settimane = SettimaneModulo.Espandi(new[] { S(1, "A", 60, 45), S(2, "B", null, 50, 8) });

        var gruppi = SettimaneModulo.Raggruppa(settimane);

        gruppi.Should().HaveCount(2);
        gruppi[0].IdSettimana.Should().Be(1);
        gruppi[0].Desc.Should().Be("A");
        gruppi[0].Varianti.Select(v => v.Id).Should().Equal(1, -1);
        gruppi[1].Desc.Should().Be("B");
        gruppi[1].Varianti.Should().ContainSingle();
    }

    [Fact]
    public void Raggruppa_WithoutDescBase_StripsSuffix()
    {
        var variante = new Settimana { Id = -5, Desc = "Settimana 5 (Senza pranzo)", CostoBase = 40 };

        SettimaneModulo.Raggruppa(new[] { variante }).Single().Desc.Should().Be("Settimana 5");
    }

    [Fact]
    public void SceltaDoppia_ReportsWeeksWithBothVariants()
    {
        var settimane = SettimaneModulo.Espandi(new[] { S(1, "A", 60, 45), S(2, "B", 60, 45, 8) });
        settimane.Where(s => Math.Abs(s.Id) == 1).ToList().ForEach(s => s.IsSelected = true);
        settimane.Single(s => s.Id == 2).IsSelected = true;

        SettimaneModulo.SceltaDoppia(settimane).Should().Equal("A");
    }
}
