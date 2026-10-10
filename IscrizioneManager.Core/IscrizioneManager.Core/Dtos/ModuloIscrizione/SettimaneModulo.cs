/// <summary>Una settimana con le sue varianti selezionabili (con / senza pranzo).</summary>
public record GruppoSettimana(int IdSettimana, string Desc, DateTime DataInizio, DateTime DataFine, List<Settimana> Varianti);

/// <summary>
/// Settimane del modulo: ogni settimana diventa una variante "Con pranzo"
/// (Id positivo, CostoIntero valorizzato) e/o "Senza pranzo" (Id negativo,
/// CostoBase valorizzato). Le RPC ricevono id_settimana = |Id| e intero = CostoIntero != null.
/// </summary>
public static class SettimaneModulo
{
  public const string SuffissoConPranzo = " (Con pranzo)";
  public const string SuffissoSenzaPranzo = " (Senza pranzo)";

  public static List<Settimana> Espandi(IEnumerable<Settimana> settimane, IEnumerable<IscrizioneSettimana>? scelte = null)
  {
    var scelteList = scelte?.ToList() ?? new List<IscrizioneSettimana>();
    var result = new List<Settimana>();

    foreach (var t in settimane.OrderBy(s => s.DataInizio).ThenBy(s => s.Id))
    {
      var sceltaIntera = scelteList.Any(x => x.Intero && x.IdSettimana == t.Id);
      var sceltaBase = scelteList.Any(x => !x.Intero && x.IdSettimana == t.Id);

      // Una variante già scelta resta visibile anche se nel frattempo è stato tolto il costo
      if (t.CostoIntero != null || sceltaIntera)
      {
        result.Add(new Settimana
        {
          Id = t.Id,
          event_id = t.event_id,
          Desc = $"{t.Desc}{SuffissoConPranzo}",
          DescBase = t.Desc,
          CostoIntero = t.CostoIntero ?? 0m,
          CostoBase = null,
          DataInizio = t.DataInizio,
          DataFine = t.DataFine,
          IsSelected = sceltaIntera,
        });
      }
      if (t.CostoBase != null || sceltaBase)
      {
        result.Add(new Settimana
        {
          Id = -t.Id,
          event_id = t.event_id,
          Desc = $"{t.Desc}{SuffissoSenzaPranzo}",
          DescBase = t.Desc,
          CostoIntero = null,
          CostoBase = t.CostoBase ?? 0m,
          DataInizio = t.DataInizio,
          DataFine = t.DataFine,
          IsSelected = sceltaBase,
        });
      }
    }

    return result;
  }

  /// <summary>Seleziona una variante; le altre varianti della stessa settimana vengono deselezionate.</summary>
  public static void Seleziona(IEnumerable<Settimana> settimane, Settimana variante, bool selezionata)
  {
    variante.IsSelected = selezionata;
    if (!selezionata)
      return;

    foreach (var altra in settimane)
    {
      if (!ReferenceEquals(altra, variante) && Math.Abs(altra.Id) == Math.Abs(variante.Id))
        altra.IsSelected = false;
    }
  }

  public static decimal Totale(IEnumerable<Settimana>? settimane) =>
    settimane?.Where(s => s.IsSelected).Sum(s => s.CostoIntero ?? s.CostoBase ?? 0m) ?? 0m;

  public static bool IsConPranzo(Settimana variante) => variante.CostoIntero != null;

  public static List<GruppoSettimana> Raggruppa(IEnumerable<Settimana>? settimane)
  {
    if (settimane == null)
      return new List<GruppoSettimana>();

    return settimane
      .GroupBy(s => Math.Abs(s.Id))
      .Select(g =>
      {
        var prima = g.First();
        return new GruppoSettimana(g.Key, DescrizioneBase(prima), prima.DataInizio, prima.DataFine,
          g.OrderBy(v => IsConPranzo(v) ? 0 : 1).ToList());
      })
      .ToList();
  }

  /// <summary>Settimane per cui risultano scelte entrambe le varianti (non ammesso).</summary>
  public static List<string> SceltaDoppia(IEnumerable<Settimana>? settimane) =>
    Raggruppa(settimane)
      .Where(g => g.Varianti.Count(v => v.IsSelected) > 1)
      .Select(g => g.Desc)
      .ToList();

  private static string DescrizioneBase(Settimana s)
  {
    if (!string.IsNullOrEmpty(s.DescBase))
      return s.DescBase;
    var desc = s.Desc ?? string.Empty;
    foreach (var suffisso in new[] { SuffissoConPranzo, SuffissoSenzaPranzo })
    {
      if (desc.EndsWith(suffisso, StringComparison.Ordinal))
        return desc[..^suffisso.Length];
    }
    return desc;
  }
}
