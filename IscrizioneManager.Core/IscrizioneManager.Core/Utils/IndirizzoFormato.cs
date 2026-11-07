using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using IscrizioneManager.Core.Models;

namespace IscrizioneManager.Core.Utils;

/// <summary>Indirizzo scritto nel modulo, già separato in comune, via e civico.</summary>
public record IndirizzoDiviso(string? Comune, string? Via, string? Civico);

/// <summary>
/// Scrittura uniforme di comune, via e civico. Le stesse regole, per le righe esistenti,
/// sono in supabase/strumenti/indirizzi/dividi_indirizzi.py; quale via sia "la stessa" lo
/// decide il database (public.normalizza_toponimo).
/// </summary>
public static partial class IndirizzoFormato
{
  private const int MaxSuggerimenti = 20;

  private static readonly HashSet<string> Minuscole = new(StringComparer.OrdinalIgnoreCase)
  {
    "di", "del", "dello", "della", "dei", "degli", "delle", "da", "dal", "dallo", "dalla", "dai", "dagli", "dalle",
    "de", "e", "ed", "al", "allo", "alla", "ai", "agli", "alle", "in", "sul", "sulla", "a", "con", "per", "tra",
    "il", "lo", "la", "i", "gli", "le",
  };

  private static readonly HashSet<string> Elisioni = new(StringComparer.OrdinalIgnoreCase)
  {
    "dell", "dall", "nell", "sull", "all", "l", "un",
  };

  private static readonly (Regex Schema, string Nome)[] TipiAbbreviati =
  {
    (new Regex(@"^(?:v\.?\s*le)(?=[\s.]|$)\.?\s*", RegexOptions.IgnoreCase), "Viale"),
    (new Regex(@"^(?:v\.?\s*lo|vic\.?)(?=[\s.]|$)\.?\s*", RegexOptions.IgnoreCase), "Vicolo"),
    (new Regex(@"^(?:p\.?\s*le)(?=[\s.]|$)\.?\s*", RegexOptions.IgnoreCase), "Piazzale"),
    (new Regex(@"^(?:p\.?\s*z{1,2}a)(?=[\s.]|$)\.?\s*", RegexOptions.IgnoreCase), "Piazza"),
    (new Regex(@"^(?:c\.?\s*so)(?=[\s.]|$)\.?\s*", RegexOptions.IgnoreCase), "Corso"),
    (new Regex(@"^(?:l\.?\s*go)(?=[\s.]|$)\.?\s*", RegexOptions.IgnoreCase), "Largo"),
    (new Regex(@"^(?:str\.?)(?=[\s.]|$)\.?\s*", RegexOptions.IgnoreCase), "Strada"),
    (new Regex(@"^(?:loc\.?)(?=[\s.]|$)\.?\s*", RegexOptions.IgnoreCase), "Località"),
    (new Regex(@"^(?:v\.?)(?=[\s.]|$)\.?\s*", RegexOptions.IgnoreCase), "Via"),
  };

  private const string Snc = @"s\.?\s*n\.?\s*c\.?|s\.?\s*n\.?";

  [GeneratedRegex(@"^(?<via>.*?\S)[\s,]+(?:(?:n\.?|nr\.?|n°|num\.?|numero|civico)\s*)?(?<civico>\d+(?:\s*[/\-]\s*[A-Za-z0-9]{1,3}|\s*(?:bis|ter)\b|\s*[A-Za-z]\b)?(?:\s*(?:int|scala|sc)\.?\s*\w{1,3})?|" + Snc + @")\s*$", RegexOptions.IgnoreCase)]
  private static partial Regex CivicoInFondo();

  [GeneratedRegex(@"^(?=[IVX])X{0,3}(?:IX|IV|V?I{0,3})$", RegexOptions.IgnoreCase)]
  private static partial Regex NumeroRomano();

  /// <summary>Via e civico da un testo unico, come "Via Roma 12/a". Senza numero in fondo è tutta via.</summary>
  public static (string? Via, string? Civico) Dividi(string? indirizzo)
  {
    var testo = Spazi(indirizzo);
    if (testo.Length == 0)
      return (null, null);

    var trovato = CivicoInFondo().Match(testo);
    return trovato.Success
      ? (Via(trovato.Groups["via"].Value), PulisciCivico(trovato.Groups["civico"].Value))
      : (Via(testo), null);
  }

  /// <summary>
  /// Quello che parte verso il database: se la via è stata scritta con il numero lo separa,
  /// così "Via Roma 12" non diventa una via nuova.
  /// </summary>
  public static IndirizzoDiviso PerSalvataggio(string? comune, string? via, string? civico)
  {
    var civicoScritto = Spazi(civico);
    var (viaFinale, civicoFinale) = civicoScritto.Length == 0
      ? Dividi(via)
      : (Via(via), PulisciCivico(civicoScritto));
    return new IndirizzoDiviso(Comune(comune), viaFinale, civicoFinale);
  }

  /// <summary>Indirizzo da mostrare nel modulo: dall'anagrafica o, se non è collegato, dal vecchio testo.</summary>
  public static IndirizzoDiviso DaBambino(string? indirizzoTesto, string? comuneTesto, int? idVia, string? civico,
    IEnumerable<VoceIndirizzo> elenco)
  {
    var voce = idVia == null ? null : elenco.FirstOrDefault(v => v.IdVia == idVia);
    if (voce != null)
      return new IndirizzoDiviso(voce.Comune, voce.Via, civico);

    var (via, civicoDiviso) = Dividi(indirizzoTesto);
    return new IndirizzoDiviso(comuneTesto, via, civicoDiviso);
  }

  public static IEnumerable<string> SuggerisciComuni(IEnumerable<VoceIndirizzo> elenco, string? testo)
  {
    var cerca = Chiave(testo);
    return elenco
      .Select(v => v.Comune)
      .Where(c => !string.IsNullOrWhiteSpace(c) && Chiave(c).Contains(cerca))
      .Select(c => c!)
      .Distinct()
      .OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase)
      .Take(MaxSuggerimenti);
  }

  /// <summary>Vie del comune scelto; a comune vuoto, tutte.</summary>
  public static IEnumerable<string> SuggerisciVie(IEnumerable<VoceIndirizzo> elenco, string? comune, string? testo)
  {
    var cerca = Chiave(testo);
    var delComune = Chiave(comune);
    return elenco
      .Where(v => delComune.Length == 0 || Chiave(v.Comune) == delComune)
      .Select(v => v.Via)
      .Where(v => !string.IsNullOrWhiteSpace(v) && Chiave(v).Contains(cerca))
      .Select(v => v!)
      .Distinct()
      .OrderBy(v => v, StringComparer.CurrentCultureIgnoreCase)
      .Take(MaxSuggerimenti);
  }

  /// <summary>Maiuscole uniformi: "VIA DELL'OSPEDALE" diventa "Via dell'Ospedale".</summary>
  public static string Titolo(string? testo)
  {
    var pulito = Regex.Replace(Spazi(testo).Replace('’', '\'').Replace('`', '\''), @"\.(?=\S)", ". ");
    pulito = Regex.Replace(pulito, @"'\s+(?=\w)", "'");
    var parole = pulito.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    return string.Join(' ', parole.Select((parola, i) => Parola(parola, i == 0)));
  }

  private static string? Via(string? via)
  {
    var testo = Spazi(via).Trim(' ', ',', ';', '-');
    if (testo.Length == 0)
      return null;

    foreach (var (schema, nome) in TipiAbbreviati)
    {
      if (schema.IsMatch(testo))
      {
        testo = $"{nome} {schema.Replace(testo, "", 1)}".Trim();
        break;
      }
    }
    return Titolo(testo);
  }

  private static string? Comune(string? comune)
  {
    var testo = Regex.Replace(Spazi(comune), @"\b\d{5}\b", " ");
    testo = Regex.Replace(testo, @"\s*\(\s*[A-Za-z]{2}\s*\)\s*$", "");
    testo = Regex.Replace(testo, @"[\s,\-]+[A-Z]{2}$", "");
    testo = Spazi(testo).Trim(' ', ',', '-');
    return testo.Length == 0 ? null : Titolo(testo);
  }

  private static string? PulisciCivico(string? civico)
  {
    var testo = Spazi(civico);
    if (testo.Length == 0)
      return null;
    if (Regex.IsMatch(testo, $"^(?:{Snc})$", RegexOptions.IgnoreCase))
      return "snc";

    var parti = Regex.Match(testo, @"^(\d+)(.*)$");
    if (!parti.Success)
      return testo;

    var numero = parti.Groups[1].Value;
    var resto = parti.Groups[2].Value;
    var interno = "";
    var conInterno = Regex.Match(resto, @"\s*(int|scala|sc)\.?\s*(\w{1,3})$", RegexOptions.IgnoreCase);
    if (conInterno.Success)
    {
      interno = $" {conInterno.Groups[1].Value.ToLowerInvariant()}. {conInterno.Groups[2].Value}";
      resto = resto[..conInterno.Index];
    }
    resto = Regex.Replace(resto, @"\s+", "");
    return Regex.IsMatch(resto, "^(bis|ter)$", RegexOptions.IgnoreCase)
      ? $"{numero} {resto.ToLowerInvariant()}{interno}"
      : $"{numero}{resto.ToUpperInvariant()}{interno}";
  }

  private static string Parola(string parola, bool prima)
  {
    if (NumeroRomano().IsMatch(parola))
      return parola.ToUpperInvariant();

    // "NICOLO'" scritto senza accento
    if (parola.Length >= 4 && parola[^1] == '\'' && "aeiouAEIOU".Contains(parola[^2]))
      parola = parola[..^2] + "àèìòù"["aeiou".IndexOf(char.ToLowerInvariant(parola[^2]))];

    var apostrofo = parola.IndexOf('\'');
    if (apostrofo >= 0)
    {
      var testa = parola[..apostrofo];
      var coda = parola[(apostrofo + 1)..];
      var testaFinale = !prima && Elisioni.Contains(testa) ? testa.ToLowerInvariant() : Maiuscola(testa);
      return coda.Length == 0 ? $"{testaFinale}'" : $"{testaFinale}'{Parola(coda, false)}";
    }

    return !prima && Minuscole.Contains(parola) ? parola.ToLowerInvariant() : Maiuscola(parola);
  }

  private static string Maiuscola(string parola) =>
    parola.Length == 0 ? parola : char.ToUpperInvariant(parola[0]) + parola[1..].ToLowerInvariant();

  private static string Spazi(string? testo) =>
    Regex.Replace((testo ?? "").Replace(' ', ' '), @"\s+", " ").Trim();

  /// <summary>Solo per filtrare i suggerimenti: senza maiuscole, accenti e punteggiatura.</summary>
  private static string Chiave(string? testo)
  {
    var scomposto = (testo ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD);
    var pulito = new StringBuilder(scomposto.Length);
    foreach (var c in scomposto)
    {
      if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
        continue;
      pulito.Append(char.IsLetterOrDigit(c) ? c : ' ');
    }
    return Spazi(pulito.ToString());
  }
}
