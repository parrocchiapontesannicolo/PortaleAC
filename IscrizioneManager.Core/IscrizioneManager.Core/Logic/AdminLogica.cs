using System.Security.Cryptography;
using IscrizioneManager.Core.Models;
using IscrizioneManager.Core.Models.Admin;
using IscrizioneManager.Core.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Supabase.Functions.Exceptions;
using Supabase.Postgrest.Exceptions;

namespace IscrizioneManager.Core.Logic;

/// <summary>Regole pure delle pagine di amministrazione (validazioni, differenze, errori).</summary>
public static class AdminLogica
{
  public const int PasswordMinLength = 8;
  public const int PasswordMaxLength = 72;
  public const int NomeMaxLength = 60;

  // ---------------------------------------------------------------- validazioni

  public static string? ValidaNome(string? nome)
  {
    var n = nome?.Trim() ?? string.Empty;
    if (n.Length == 0)
      return "Il nome è obbligatorio.";
    if (n.Length > NomeMaxLength)
      return $"Il nome può avere al massimo {NomeMaxLength} caratteri.";
    return null;
  }

  public static string? ValidaPassword(string? password)
  {
    if (string.IsNullOrEmpty(password) || password.Length < PasswordMinLength)
      return $"La password deve avere almeno {PasswordMinLength} caratteri.";
    if (password.Length > PasswordMaxLength)
      return $"La password può avere al massimo {PasswordMaxLength} caratteri.";
    if (password.Trim() != password)
      return "La password non può iniziare o finire con uno spazio.";
    return null;
  }

  /// <summary>Vuoto = nessun link. Altrimenti deve essere un indirizzo http(s) completo.</summary>
  public static string? ValidaUrl(string? url)
  {
    if (string.IsNullOrWhiteSpace(url))
      return null;
    if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
      return "Inserisci un indirizzo completo, ad esempio https://drive.google.com/...";
    return null;
  }

  // ---------------------------------------------------------------- password

  private const string Minuscole = "abcdefghjkmnpqrstuvwxyz";
  private const string Maiuscole = "ABCDEFGHJKLMNPQRSTUVWXYZ";
  private const string Cifre = "23456789";

  /// <summary>
  /// Password casuale facile da dettare (senza caratteri ambigui come l/1/O/0),
  /// con almeno una minuscola, una maiuscola e una cifra.
  /// </summary>
  public static string GeneraPassword(int lunghezza = 12, Func<int, int>? casuale = null)
  {
    if (lunghezza < PasswordMinLength)
      lunghezza = PasswordMinLength;
    casuale ??= RandomNumberGenerator.GetInt32;

    var tutti = Minuscole + Maiuscole + Cifre;
    var caratteri = new char[lunghezza];
    caratteri[0] = Minuscole[casuale(Minuscole.Length)];
    caratteri[1] = Maiuscole[casuale(Maiuscole.Length)];
    caratteri[2] = Cifre[casuale(Cifre.Length)];
    for (var i = 3; i < lunghezza; i++)
      caratteri[i] = tutti[casuale(tutti.Length)];

    // Mescola per non avere sempre lo stesso schema iniziale
    for (var i = lunghezza - 1; i > 0; i--)
    {
      var j = casuale(i + 1);
      (caratteri[i], caratteri[j]) = (caratteri[j], caratteri[i]);
    }
    return new string(caratteri);
  }

  // ---------------------------------------------------------------- account

  /// <summary>Stesso ordine della tendina di login (ordine, poi nome).</summary>
  public static List<AppAccount> Ordina(IEnumerable<AppAccount> accounts) =>
    accounts.OrderBy(a => a.Ordine).ThenBy(a => a.Nome, StringComparer.CurrentCultureIgnoreCase).ToList();

  public static int ProssimoOrdine(IEnumerable<AppAccount> accounts) =>
    accounts.Select(a => a.Ordine).DefaultIfEmpty(0).Max() + 1;

  /// <summary>
  /// Sposta l'account in posizione <paramref name="indice"/> di <paramref name="delta"/> posti
  /// e rinumera 1..n. Restituisce solo gli account il cui ordine cambia.
  /// </summary>
  public static List<(Guid Id, int Ordine)> Sposta(IReadOnlyList<AppAccount> ordinati, int indice, int delta)
  {
    var lista = ordinati.ToList();
    var destinazione = indice + delta;
    if (indice < 0 || indice >= lista.Count || destinazione < 0 || destinazione >= lista.Count)
      return new();

    var elemento = lista[indice];
    lista.RemoveAt(indice);
    lista.Insert(destinazione, elemento);

    var modifiche = new List<(Guid, int)>();
    for (var i = 0; i < lista.Count; i++)
    {
      if (lista[i].Ordine != i + 1)
        modifiche.Add((lista[i].Id, i + 1));
    }
    return modifiche;
  }

  public record ModificheAccessi(IReadOnlyList<(int EventId, int RuoloId)> DaSalvare, IReadOnlyList<int> DaRimuovere)
  {
    public bool Vuote => DaSalvare.Count == 0 && DaRimuovere.Count == 0;
  }

  /// <summary>
  /// Confronta gli accessi attuali (evento → ruolo) con quelli scelti
  /// (evento → ruolo, null = nessun accesso).
  /// </summary>
  public static ModificheAccessi CalcolaModificheAccessi(
    IReadOnlyDictionary<int, int> attuali, IReadOnlyDictionary<int, int?> desiderati)
  {
    var salvare = new List<(int, int)>();
    var rimuovere = new List<int>();

    foreach (var (eventId, ruoloId) in desiderati.OrderBy(d => d.Key))
    {
      var haAttuale = attuali.TryGetValue(eventId, out var attuale);
      if (ruoloId is int nuovo)
      {
        if (!haAttuale || attuale != nuovo)
          salvare.Add((eventId, nuovo));
      }
      else if (haAttuale)
      {
        rimuovere.Add(eventId);
      }
    }
    return new ModificheAccessi(salvare, rimuovere);
  }

  // ---------------------------------------------------------------- ruoli e permessi

  /// <summary>Tiene solo i permessi noti, senza doppioni, nell'ordine di <see cref="Permessi.Tutti"/>.</summary>
  public static List<string> NormalizzaPermessi(IEnumerable<string>? permessi)
  {
    var set = new HashSet<string>(permessi ?? Enumerable.Empty<string>());
    return Permessi.Tutti.Select(p => p.Chiave).Where(set.Contains).ToList();
  }

  public static IEnumerable<IGrouping<string, Permessi.Info>> GruppiPermessi() =>
    Permessi.Tutti.GroupBy(p => p.Gruppo);

  /// <summary>Cosa comporta un permesso oltre al suo nome (le regole sono nella RLS del database).</summary>
  public static string? NotaPermesso(string chiave) => chiave switch
  {
    Permessi.IscrittiEdit => "Permette anche di vedere la scheda sanitaria, anche senza il permesso dedicato.",
    Permessi.IscrittiPagamenti => "Permette di vedere la lista iscritti per segnare i pagamenti.",
    Permessi.SquadreEdit => "Include la visione delle squadre.",
    _ => null,
  };

  public static string NomePermesso(string chiave) =>
    Permessi.Tutti.FirstOrDefault(p => p.Chiave == chiave)?.Nome ?? chiave;

  // ---------------------------------------------------------------- configurazione evento

  /// <summary>Interruttore del modulo di iscrizione (colonne view_* di evento_metadata).</summary>
  public record CampoModulo(string Nome, string Descrizione, Func<EventoMetadata, bool> Get, Action<EventoMetadata, bool> Set);

  public static readonly IReadOnlyList<CampoModulo> CampiModulo = new List<CampoModulo>
  {
    new("Dati dei genitori", "Nome, cognome e telefono di mamma e papà", m => m.View_Genitori, (m, v) => m.View_Genitori = v),
    new("Email del genitore", "Indirizzo email per le comunicazioni", m => m.View_MailGenitore, (m, v) => m.View_MailGenitore = v),
    new("Classe frequentata", "Anno scolastico del bambino (vedi gradi scolastici ammessi)", m => m.View_AnnoScolastico, (m, v) => m.View_AnnoScolastico = v),
    new("Settimane", "Scelta delle settimane di partecipazione", m => m.View_Settimane, (m, v) => m.View_Settimane = v),
    new("Scheda sanitaria", "Allergie, intolleranze, patologie e farmaci", m => m.View_SchedaSanitaria, (m, v) => m.View_SchedaSanitaria = v),
    new("Consensi", "Privacy, foto e altri consensi dell'evento", m => m.View_Consensi, (m, v) => m.View_Consensi = v),
    new("Formato e pagamento", "Formato di iscrizione e modalità di pagamento", m => m.View_FormatoIscrPagamento, (m, v) => m.View_FormatoIscrPagamento = v),
    new("Caparra", "Indica se la caparra è stata pagata", m => m.View_Caparra, (m, v) => m.View_Caparra = v),
    new("Iscrizione al NOI", "Tesseramento all'associazione NOI", m => m.View_IscrizioneNoi, (m, v) => m.View_IscrizioneNoi = v),
    new("Sconto fratelli", "Riduzione per fratelli iscritti", m => m.View_ScontoFratelli, (m, v) => m.View_ScontoFratelli = v),
    new("Taglia maglietta", "Scelta della taglia (vedi elenco taglie)", m => m.View_Taglia, (m, v) => m.View_Taglia = v),
    new("Uscita autonoma", "Autorizzazione a tornare a casa da solo", m => m.View_UscitaAutonoma, (m, v) => m.View_UscitaAutonoma = v),
    new("Priorità", "Segnala l'iscrizione come prioritaria", m => m.View_Priority, (m, v) => m.View_Priority = v),
  };

  /// <summary>Configurazione di partenza per un nuovo evento senza modello da copiare.</summary>
  public static EventoMetadata MetadataPredefinito(int eventId, IEnumerable<int> gradi) => new()
  {
    event_id = eventId,
    View_Genitori = true,
    View_MailGenitore = true,
    View_AnnoScolastico = true,
    View_Settimane = true,
    View_SchedaSanitaria = true,
    View_Consensi = true,
    View_FormatoIscrPagamento = true,
    View_UscitaAutonoma = true,
    View_Caparra = false,
    View_IscrizioneNoi = false,
    View_ScontoFratelli = false,
    View_Taglia = false,
    View_Priority = false,
    GradiScuolaAllowed = SerializzaGradi(gradi),
  };

  public static EventoMetadata CopiaMetadata(EventoMetadata origine, int eventId)
  {
    var copia = new EventoMetadata
    {
      event_id = eventId,
      GradiScuolaAllowed = SerializzaGradi(ParseGradi(origine.GradiScuolaAllowed)),
    };
    foreach (var campo in CampiModulo)
      campo.Set(copia, campo.Get(origine));
    return copia;
  }

  /// <summary>Legge gradi_scuola_allowed ("[1,2]"); valori non validi = nessun grado.</summary>
  public static int[] ParseGradi(string? json)
  {
    if (string.IsNullOrWhiteSpace(json))
      return Array.Empty<int>();
    try
    {
      return (JsonConvert.DeserializeObject<int[]>(json) ?? Array.Empty<int>()).Distinct().OrderBy(g => g).ToArray();
    }
    catch (JsonException)
    {
      return Array.Empty<int>();
    }
  }

  public static string SerializzaGradi(IEnumerable<int> gradi) =>
    JsonConvert.SerializeObject(gradi.Distinct().OrderBy(g => g).ToArray());

  // ---------------------------------------------------------------- errori

  /// <summary>Messaggio leggibile per errori di Postgrest e della Edge Function.</summary>
  public static string MessaggioErrore(Exception ex) => ex switch
  {
    FunctionsException fe => MessaggioDaJson(fe.Content) ?? fe.StatusCode switch
    {
      401 => "Sessione scaduta: esci e rientra.",
      403 => "Operazione riservata ai super amministratori.",
      404 => "Funzione admin-accounts non trovata: va pubblicata su Supabase (vedi supabase/functions/admin-accounts).",
      _ => fe.Message,
    },
    PostgrestException pe => MessaggioDaJson(pe.Content) ?? MessaggioDaJson(pe.Message) ?? pe.Message,
    _ => ex.Message,
  };

  /// <summary>Interpreta il corpo JSON di un errore. Null se non è JSON riconoscibile.</summary>
  public static string? MessaggioDaJson(string? json)
  {
    if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{"))
      return null;

    JObject obj;
    try { obj = JObject.Parse(json); }
    catch (JsonException) { return null; }

    // Edge Function: { "error": "..." }
    if (obj["error"] is JValue { Type: JTokenType.String } errore)
      return (string?)errore;

    var codice = (string?)obj["code"];
    var messaggio = (string?)obj["message"] ?? (string?)obj["msg"];
    return codice switch
    {
      "23505" => "Esiste già un elemento con lo stesso nome.",
      "23503" => "Operazione non possibile: l'elemento è collegato ad altri dati (ad esempio iscrizioni o accessi).",
      "42501" => "Permesso negato.",
      _ => messaggio,
    };
  }
}
