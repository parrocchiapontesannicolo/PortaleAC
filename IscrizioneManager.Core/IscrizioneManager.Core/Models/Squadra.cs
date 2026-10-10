using System.Globalization;
using IscrizioniManager.Data;
using IscrizioniManager.Models;
using Supabase.Postgrest.Attributes;
using StjIgnore = System.Text.Json.Serialization.JsonIgnoreAttribute;
using NjIgnore = Newtonsoft.Json.JsonIgnoreAttribute;

[Table("squadra")]
public class Squadra : EventModel
{
  /// <summary>Id del contenitore virtuale "Senza squadra" (non esiste sul database).</summary>
  public const int IdSenzaSquadra = 0;
  public const string NomeSenzaSquadra = "Senza squadra";
  public const string ColoreSenzaSquadra = "#BDBDBD";

  [PrimaryKey("id_squadra")]
  public int Id { get; set; }

  [Column("nome")]
  public string Nome { get; set; } = string.Empty;

  [Column("color")]
  public string Color { get; set; } = string.Empty;

  // Proprietà solo lato client: escluse dalla serializzazione verso PostgREST (Newtonsoft)
  [NjIgnore, StjIgnore]
  public List<Bambino> Bambini { get; set; }

  [NjIgnore, StjIgnore]
  public bool IsDragOver { get; set; } = false;

  [NjIgnore, StjIgnore]
  public bool IsExpanded { get; set; } = true;

  [NjIgnore, StjIgnore]
  public bool IsSenzaSquadra => Id == IdSenzaSquadra;

  public Squadra()
  {
    Bambini = new List<Bambino>();
  }

  /// <summary>Ordine standard dei bambini in una squadra: anno scolastico, cognome, nome.</summary>
  public void OrdinaBambini()
  {
    Bambini = Bambini
      .OrderBy(x => x.Anno ?? int.MaxValue)
      .ThenBy(x => x.Cognome, StringComparer.CurrentCultureIgnoreCase)
      .ThenBy(x => x.Nome, StringComparer.CurrentCultureIgnoreCase)
      .ToList();
  }

  /// <summary>Numero di bambini per anno scolastico, in ordine di anno.</summary>
  public List<ConteggioAnnoSquadra> ConteggiPerAnno() =>
    Bambini
      .GroupBy(b => b.Anno)
      .OrderBy(g => g.Key ?? int.MaxValue)
      .Select(g => new ConteggioAnnoSquadra(
        g.Key,
        g.Select(b => b.AnnoDesc).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d)) ?? "N/D",
        g.Count()))
      .ToList();

  /// <summary>Colore del testo (nero o bianco) leggibile sopra il colore di sfondo dato.</summary>
  public static string TestoLeggibile(string? hex)
  {
    if (!TryParseColore(hex, out var r, out var g, out var b))
      return "#000000";

    double luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
    return luminance > 0.5 ? "#000000" : "#FFFFFF";
  }

  /// <summary>Normalizza un colore in formato "#RRGGBB". Accetta #RGB, #RRGGBB e #RRGGBBAA.</summary>
  public static string? NormalizzaColore(string? hex)
  {
    if (!TryParseColore(hex, out var r, out var g, out var b))
      return null;
    return $"#{r:X2}{g:X2}{b:X2}";
  }

  private static bool TryParseColore(string? hex, out int r, out int g, out int b)
  {
    r = g = b = 0;
    var s = hex?.Trim();
    if (string.IsNullOrEmpty(s) || s[0] != '#')
      return false;
    s = s.Substring(1);

    if (s.Length == 3)
      s = string.Concat(s.Select(c => new string(c, 2)));
    else if (s.Length == 8)
      s = s.Substring(0, 6);

    if (s.Length != 6)
      return false;

    return int.TryParse(s.AsSpan(0, 2), NumberStyles.HexNumber, null, out r)
      && int.TryParse(s.AsSpan(2, 2), NumberStyles.HexNumber, null, out g)
      && int.TryParse(s.AsSpan(4, 2), NumberStyles.HexNumber, null, out b);
  }

  /// <summary>
  /// Ricerca per nome: ogni parola cercata deve comparire nel nome o nel cognome
  /// (senza distinzione di maiuscole e accenti). Ricerca vuota = tutti.
  /// </summary>
  public static bool Corrisponde(Bambino bambino, string? ricerca)
  {
    var parole = ParoleRicerca(ricerca);
    if (parole.Length == 0)
      return true;

    var testo = $"{bambino.Cognome} {bambino.Nome}";
    return parole.All(p => Contiene(testo, p));
  }

  public static string[] ParoleRicerca(string? ricerca) =>
    (ricerca ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

  private static bool Contiene(string testo, string parola)
  {
    try
    {
      return CultureInfo.InvariantCulture.CompareInfo.IndexOf(
        testo, parola, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
    }
    catch (PlatformNotSupportedException)
    {
      return testo.Contains(parola, StringComparison.OrdinalIgnoreCase);
    }
  }
}

public record ConteggioAnnoSquadra(int? Anno, string Descrizione, int Numero);
