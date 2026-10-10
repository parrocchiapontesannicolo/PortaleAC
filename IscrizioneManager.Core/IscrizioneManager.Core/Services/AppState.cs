using IscrizioneManager.Core.Security;

namespace IscrizioneManager.Core.Services
{
  /// <summary>
  /// Stato della sessione: account, eventi accessibili, evento corrente e
  /// relativi permessi. I componenti si iscrivono a <see cref="Changed"/>.
  /// </summary>
  public class AppState
  {
    internal const string EventoCorrenteKey = "currentEventId";

    private readonly LocalStorageService _storage;
    private List<MioEvento> _eventi = new();

    public AppState(LocalStorageService storage)
    {
      _storage = storage;
    }

    public event Action? Changed;

    /// <summary>
    /// Impostata dalle pagine con modifiche non salvate: il layout la chiama
    /// prima di cambiare evento o uscire. Restituisce false per annullare.
    /// </summary>
    public Func<Task<bool>>? ConfermaUscita { get; set; }

    /// <summary>Sessione scaduta o revocata: il layout porta al login.</summary>
    public event Action? SessioneTerminata;

    internal void NotificaSessioneTerminata() => SessioneTerminata?.Invoke();

    public AccountCorrente? Account { get; private set; }
    public IReadOnlyList<MioEvento> Eventi => _eventi;
    public MioEvento? EventoCorrente { get; private set; }

    public bool IsAuthenticated => Account != null && ClientHolder.IsInitialized;
    public bool HasEvento => EventoCorrente != null;
    public bool IsSuperadmin => Account?.IsSuperadmin == true;

    /// <summary>Permesso sull'evento corrente.</summary>
    public bool Can(string permesso) => EventoCorrente?.Can(permesso) == true;

    public bool CanAny(params string[] permessi) => permessi.Any(Can);

    /// <summary>Carica account ed eventi dal server e ripristina l'ultimo evento usato.</summary>
    public async Task LoadAsync()
    {
      var client = ClientHolder.Client;

      var account = await client.Rpc<List<AccountCorrente>>("my_account", new Dictionary<string, object>());
      Account = account?.FirstOrDefault()
        ?? throw new AccountNonAttivoException();

      _eventi = await client.Rpc<List<MioEvento>>("my_events", new Dictionary<string, object>()) ?? new();

      MioEvento? evento = null;
      if (int.TryParse(await _storage.GetItem(EventoCorrenteKey), out var lastId))
        evento = _eventi.FirstOrDefault(e => e.EventId == lastId);

      var attivi = _eventi.Where(e => e.Attivo).ToList();
      if (evento == null && attivi.Count == 1)
        evento = attivi[0];

      SetEvento(evento);
      Changed?.Invoke();
    }

    /// <summary>Ricarica account e permessi (es. dopo modifiche dalla pagina admin).</summary>
    public async Task RefreshAsync()
    {
      var correnteId = EventoCorrente?.EventId;
      var account = await ClientHolder.Client.Rpc<List<AccountCorrente>>("my_account", new Dictionary<string, object>());
      Account = account?.FirstOrDefault() ?? Account;
      _eventi = await ClientHolder.Client.Rpc<List<MioEvento>>("my_events", new Dictionary<string, object>()) ?? new();
      SetEvento(_eventi.FirstOrDefault(e => e.EventId == correnteId));
      Changed?.Invoke();
    }

    /// <summary>Cambia evento senza rifare il login.</summary>
    public async Task SelezionaEventoAsync(int eventId)
    {
      var evento = _eventi.FirstOrDefault(e => e.EventId == eventId)
        ?? throw new InvalidOperationException("Evento non accessibile.");

      SetEvento(evento);
      await _storage.SetItem(EventoCorrenteKey, eventId.ToString());
      Changed?.Invoke();
    }

    internal void Clear()
    {
      Account = null;
      _eventi = new();
      EventoCorrente = null;
      Changed?.Invoke();
    }

    private void SetEvento(MioEvento? evento)
    {
      EventoCorrente = evento;
      if (ClientHolder.IsInitialized)
        ClientHolder.Client.EnsureEventId(evento?.EventId ?? 0);
    }
  }

  /// <summary>L'utente autenticato non ha un account attivo (my_account vuoto).</summary>
  public sealed class AccountNonAttivoException : InvalidOperationException
  {
    public AccountNonAttivoException()
      : base("Account disattivato o non configurato. Contatta l'amministratore.") { }
  }
}
