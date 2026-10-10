using IscrizioneManager.Core.Models;

namespace IscrizioneManager.Core.Logic;

public sealed record ConteggioAnno(int? AnnoId, string Descrizione, int Totale);

public sealed record ConteggioSquadra(int Id, string Nome, string Colore, int Totale);

/// <summary>Numeri della home. Le sezioni non caricate (per permessi) restano vuote.</summary>
public sealed class DashboardStats
{
    public const string AnnoNonIndicato = "Non indicato";

    /// <summary>True se sono stati caricati gli iscritti (totali, pagamenti, anni).</summary>
    public bool HaIscritti { get; init; }

    /// <summary>True se sono state caricate le squadre.</summary>
    public bool HaSquadre { get; init; }

    public int TotaleIscritti { get; init; }
    public int Pagati { get; init; }
    public int DaPagare { get; init; }

    /// <summary>Non ancora pagati ma con la caparra versata.</summary>
    public int ConCaparra { get; init; }

    public IReadOnlyList<ConteggioAnno> PerAnno { get; init; } = Array.Empty<ConteggioAnno>();
    public IReadOnlyList<ConteggioSquadra> Squadre { get; init; } = Array.Empty<ConteggioSquadra>();
    public int SenzaSquadra { get; init; }

    public int NumeroSquadre => Squadre.Count;

    public int PercentualePagati => TotaleIscritti == 0 ? 0 : (int)Math.Round(Pagati * 100.0 / TotaleIscritti);

    public IReadOnlyList<string> ColoriSquadre => Squadre
        .Select(s => s.Colore)
        .Where(c => !string.IsNullOrWhiteSpace(c))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    public static DashboardStats Vuote { get; } = new();

    /// <summary>
    /// Aggrega i dati grezzi. Passare null per le sezioni non caricate.
    /// Un bambino con più righe di iscrizione è contato una volta sola.
    /// </summary>
    public static DashboardStats Calcola(
        IEnumerable<Iscrizione>? iscrizioni,
        IEnumerable<AnnoScolastico>? anniScolastici,
        IEnumerable<int>? idBambini,
        IEnumerable<Squadra>? squadre,
        IEnumerable<SquadraBambino>? squadraBambini)
    {
        var haIscritti = iscrizioni != null;
        var haSquadre = squadre != null;

        var perBambino = (iscrizioni ?? Enumerable.Empty<Iscrizione>())
            .GroupBy(i => i.IdBambino)
            .Select(g => g.OrderBy(i => i.Id).First())
            .ToList();

        var anni = (anniScolastici ?? Enumerable.Empty<AnnoScolastico>())
            .Where(a => a.Id.HasValue)
            .GroupBy(a => a.Id!.Value)
            .ToDictionary(g => g.Key, g => g.First().Desc);

        var perAnno = perBambino
            .GroupBy(i => i.Anno)
            .Select(g => new ConteggioAnno(
                g.Key,
                g.Key == null
                    ? AnnoNonIndicato
                    : anni.TryGetValue(g.Key.Value, out var desc) && !string.IsNullOrWhiteSpace(desc) ? desc : $"Anno {g.Key}",
                g.Count()))
            .OrderBy(a => a.AnnoId == null)
            .ThenBy(a => a.AnnoId)
            .ToList();

        var pagati = perBambino.Count(i => i.Pagato);

        var bambini = idBambini?.ToHashSet();
        var assegnazioni = (squadraBambini ?? Enumerable.Empty<SquadraBambino>())
            .Where(sb => bambini == null || bambini.Contains(sb.IdBambino))
            .ToList();

        var conteggiSquadre = (squadre ?? Enumerable.Empty<Squadra>())
            .OrderBy(s => s.Id)
            .Select(s => new ConteggioSquadra(
                s.Id,
                s.Nome,
                s.Color,
                assegnazioni.Where(sb => sb.IdSquadra == s.Id).Select(sb => sb.IdBambino).Distinct().Count()))
            .ToList();

        var assegnati = assegnazioni.Select(sb => sb.IdBambino).ToHashSet();
        var senzaSquadra = haSquadre && bambini != null ? bambini.Count(id => !assegnati.Contains(id)) : 0;

        return new DashboardStats
        {
            HaIscritti = haIscritti,
            HaSquadre = haSquadre,
            TotaleIscritti = perBambino.Count,
            Pagati = pagati,
            DaPagare = perBambino.Count - pagati,
            ConCaparra = perBambino.Count(i => !i.Pagato && i.CaparraPagata),
            PerAnno = perAnno,
            Squadre = conteggiSquadre,
            SenzaSquadra = senzaSquadra,
        };
    }
}
