using System.Text;
using System.Text.Json;
using IscrizioneManager.Core.Security;
using IscrizioniManager.Data;
using Microsoft.JSInterop;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using static Supabase.Gotrue.Constants;

namespace IscrizioneManager.Core.Services
{
  /// <summary>
  /// Login con account condiviso + password, ripristino della sessione salvata,
  /// rinnovo dei token e logout. L'evento si sceglie dopo il login (AppState).
  /// <para>
  /// Il rinnovo lo fa questa classe e non il timer di gotrue: così prima di
  /// rinnovare si rilegge la coppia di token salvata, che un'altra scheda può
  /// aver già ruotato (riusare un refresh token vecchio fa revocare la sessione),
  /// e un token scaduto durante lo standby si rinnova comunque al risveglio.
  /// </para>
  /// </summary>
  public class AuthService
  {
    internal const string AccessTokenKey = "sb.accessToken";
    internal const string RefreshTokenKey = "sb.refreshToken";

    internal const string MessaggioServerNonRaggiungibile =
      "Impossibile contattare il server. Controlla la connessione e riprova.";

    private static readonly TimeSpan RitentaDopo = TimeSpan.FromSeconds(30);

    // Chiavi della versione precedente, rimosse al primo avvio
    private static readonly string[] LegacyKeys = { "jwtToken", "refreshToken", "eventId", "eventDesc", "roleId" };

    private readonly LocalStorageService _storage;
    private readonly AppState _appState;
    private readonly IJSRuntime _js;

    // Client gotrue della sessione attiva (null se non loggato)
    private IGotrueClient<User, Session>? _auth;
    private Timer? _timerRinnovo;
    private bool _rinnovoInCorso;
    private DotNetObjectReference<AuthService>? _rifJs;

    public AuthService(LocalStorageService storage, AppState appState, IJSRuntime js)
    {
      _storage = storage;
      _appState = appState;
      _js = js;
    }

    /// <summary>
    /// True se l'ultimo ripristino è fallito per un problema temporaneo (rete,
    /// server): i token sono ancora salvati e si può riprovare senza password.
    /// </summary>
    public bool RipristinoDaRiprovare { get; private set; }

    /// <summary>Account selezionabili nella pagina di login (non richiede login).</summary>
    public async Task<List<LoginAccount>> GetLoginAccountsAsync()
    {
      var anon = new Supabase.Client(SupabaseConfig.Url, SupabaseConfig.AnonKey, new SupabaseOptions { AutoRefreshToken = false });
      return await anon.Rpc<List<LoginAccount>>("login_accounts", new Dictionary<string, object>()) ?? new();
    }

    public async Task SignInAsync(LoginAccount account, string password)
    {
      if (account == null || string.IsNullOrWhiteSpace(password))
        throw new ArgumentException("Seleziona l'account e inserisci la password.");

      var supabase = CreateClient();
      try
      {
        await supabase.Auth.SignIn(account.Email, password);
      }
      catch (GotrueException ex) when (ex.StatusCode == 400)
      {
        throw new UnauthorizedAccessException("Account o password errati.");
      }
      catch (Exception ex)
      {
        // Niente JSON grezzo all'utente: il dettaglio va in console
        Console.WriteLine($"Login: {ex}");
        throw new InvalidOperationException(AuthLogica.MessaggioErroreLogin(ex), ex);
      }

      try
      {
        await StartSessionAsync(supabase);
        RipristinoDaRiprovare = false;
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Avvio sessione fallito: {ex}");
        await ClearSessionAsync(rimuoviToken: true);
        if (ex is AccountNonAttivoException)
          throw;
        throw new InvalidOperationException(MessaggioServerNonRaggiungibile, ex);
      }
    }

    /// <summary>
    /// Ripristina la sessione salvata. Restituisce false se non c'è, se non è
    /// più valida (token rimossi) o se il server non risponde: in quest'ultimo
    /// caso i token restano salvati e <see cref="RipristinoDaRiprovare"/> è true.
    /// </summary>
    public async Task<bool> TryRestoreSessionAsync()
    {
      RipristinoDaRiprovare = false;
      try
      {
        foreach (var key in LegacyKeys)
          await _storage.RemoveItem(key);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Pulizia chiavi vecchie: {ex.Message}");
      }

      var (access, refresh) = await LeggiTokenAsync();
      if (string.IsNullOrEmpty(access) || string.IsNullOrEmpty(refresh))
        return false;

      try
      {
        var supabase = CreateClient();
        try
        {
          // Con access token ancora valido non ruota il refresh token (le altre schede restano valide)
          await supabase.Auth.SetSession(access, refresh);
        }
        catch (GotrueException ex) when (ex.StatusCode is 401 or 403)
        {
          // Access token rifiutato (es. orologio del dispositivo sfasato): si prova col refresh token
          Console.WriteLine($"Access token rifiutato, rinnovo: {ex.Message}");
          await supabase.Auth.SetSession(access, refresh, forceAccessTokenRefresh: true);
        }
        await StartSessionAsync(supabase);
        return true;
      }
      catch (Exception ex)
      {
        var definitivo = AuthLogica.IsErroreDefinitivo(ex);
        Console.WriteLine($"Ripristino sessione fallito ({(definitivo ? "sessione non valida" : "errore temporaneo, token conservati")}): {ex.Message}");
        await ClearSessionAsync(rimuoviToken: definitivo);
        RipristinoDaRiprovare = !definitivo;
        return false;
      }
    }

    public async Task SignOutAsync()
    {
      var auth = _auth;
      FermaSessioneCorrente();
      if (auth != null)
      {
        // Solo questo dispositivo: gli account sono condivisi, lo scope globale
        // disconnetterebbe tutti quelli che usano lo stesso account.
        try { await auth.SignOut(SignOutScope.Local); }
        catch (Exception ex) { Console.WriteLine($"Logout: {ex.Message}"); }
      }
      await ClearSessionAsync(rimuoviToken: true);
    }

    /// <summary>Chiamato da sessione.js quando un'altra scheda cambia i token salvati.</summary>
    [JSInvokable]
    public async Task TokenSalvatiCambiati()
    {
      var auth = _auth;
      if (auth == null || _rinnovoInCorso)
        return;

      try
      {
        if (await SincronizzaAsync(auth) == EsitoSincronizzazione.Adottata && auth == _auth)
          PianificaRinnovo();
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Sincronizzazione token: {ex.Message}");
      }
    }

    /// <summary>
    /// Chiamato da sessione.js quando la pagina torna visibile o la rete torna
    /// disponibile: i timer sono sospesi in standby, quindi si verifica subito.
    /// </summary>
    [JSInvokable]
    public Task PaginaRiattivata() => RinnovaSeNecessarioAsync();

    private static Supabase.Client CreateClient() =>
      new(SupabaseConfig.Url, SupabaseConfig.AnonKey, new SupabaseOptions { AutoRefreshToken = false, AutoConnectRealtime = false });

    private async Task StartSessionAsync(Supabase.Client supabase)
    {
      FermaSessioneCorrente();

      var session = supabase.Auth.CurrentSession
        ?? throw new GotrueException("Sessione non creata.", FailureHint.Reason.NoSessionFound);
      // SetSession con token ancora valido mette in ExpiresIn la data assoluta di scadenza
      AuthLogica.AllineaScadenza(session, DateTime.UtcNow);
      // Prima si salvano i token, poi si attiva la sessione: la sincronizzazione
      // tra schede non deve vedere i token della sessione precedente.
      await SaveTokensAsync(session);

      var eventClient = new EventSupabaseClient(
        auth: supabase.Auth,
        realtime: supabase.Realtime,
        functions: supabase.Functions,
        postgrest: supabase.Postgrest,
        storage: supabase.Storage,
        options: new SupabaseOptions(),
        eventId: 0);

      ClientHolder.Initialize(eventClient);
      _auth = supabase.Auth;

      await _appState.LoadAsync();
      PianificaRinnovo();
      await RegistraAscoltoJsAsync();
    }

    private async Task RegistraAscoltoJsAsync()
    {
      try
      {
        _rifJs ??= DotNetObjectReference.Create(this);
        var modulo = await _js.InvokeAsync<IJSObjectReference>("import", "./js/sessione.js");
        await modulo.InvokeVoidAsync("registra", _rifJs, RefreshTokenKey);
      }
      catch (Exception ex)
      {
        // Senza ascolto si perde solo la sincronizzazione immediata tra schede
        Console.WriteLine($"Ascolto sessione non disponibile: {ex.Message}");
      }
    }

    // ------------------------------------------------------------ rinnovo

    private void PianificaRinnovo(TimeSpan? attesa = null)
    {
      _timerRinnovo?.Dispose();
      _timerRinnovo = null;

      var access = _auth?.CurrentSession?.AccessToken;
      if (access == null)
        return;

      var ritardo = attesa ?? AuthLogica.CalcolaAttesaRinnovo(
        AuthLogica.LeggiScadenzaJwt(access), DateTimeOffset.UtcNow, TimeSpan.FromSeconds(Random.Shared.Next(0, 30)));
      _timerRinnovo = new Timer(_ => _ = RinnovaSeNecessarioAsync(), null, ritardo, Timeout.InfiniteTimeSpan);
    }

    private async Task RinnovaSeNecessarioAsync()
    {
      var auth = _auth;
      if (auth?.CurrentSession == null || _rinnovoInCorso)
        return;

      _rinnovoInCorso = true;
      TimeSpan? riprova = null;
      try
      {
        // Un'altra scheda può aver già ruotato i token
        if (await SincronizzaAsync(auth) == EsitoSincronizzazione.Terminata || auth != _auth)
          return;

        var corrente = auth.CurrentSession;
        if (corrente?.AccessToken == null || corrente.RefreshToken == null
            || !AuthLogica.DaRinnovare(AuthLogica.LeggiScadenzaJwt(corrente.AccessToken), DateTimeOffset.UtcNow))
          return;

        // La versione con i token espliciti non scarta la sessione già scaduta
        if (auth is Supabase.Gotrue.Client gotrue)
          await gotrue.RefreshToken(corrente.AccessToken, corrente.RefreshToken);
        else
          await auth.RefreshToken();
        if (auth != _auth)
          return;

        AuthLogica.AllineaScadenza(auth.CurrentSession!, DateTime.UtcNow);
        await SaveTokensAsync(auth.CurrentSession);
      }
      catch (Exception ex)
      {
        if (auth != _auth)
          return;

        if (AuthLogica.IsErroreDefinitivo(ex))
        {
          Console.WriteLine($"Rinnovo token rifiutato: {ex.Message}");
          // Se nel frattempo un'altra scheda ha salvato token nuovi si usano quelli
          var esito = EsitoSincronizzazione.Invariata;
          try { esito = await SincronizzaAsync(auth); }
          catch (Exception exSync) { Console.WriteLine($"Sincronizzazione token: {exSync.Message}"); }
          if (esito == EsitoSincronizzazione.Invariata && auth == _auth)
            await TerminaSessioneAsync(rimuoviToken: true);
        }
        else
        {
          Console.WriteLine($"Rinnovo token rimandato: {ex.Message}");
          riprova = RitentaDopo;
        }
      }
      finally
      {
        _rinnovoInCorso = false;
        if (auth == _auth)
          PianificaRinnovo(riprova);
      }
    }

    private enum EsitoSincronizzazione { Invariata, Adottata, Terminata }

    /// <summary>Allinea la sessione in memoria ai token salvati (scritti da un'altra scheda).</summary>
    private async Task<EsitoSincronizzazione> SincronizzaAsync(IGotrueClient<User, Session> auth)
    {
      var (access, refresh) = await LeggiTokenAsync();
      var corrente = auth.CurrentSession;
      if (auth != _auth || corrente == null)
        return EsitoSincronizzazione.Invariata;

      switch (AuthLogica.DecidiSincronizzazione(corrente.AccessToken, corrente.RefreshToken, access, refresh))
      {
        case AzioneSincronizzazione.Adotta:
          // Nessuna chiamata al server: si prende la coppia già ruotata
          corrente.AccessToken = access;
          corrente.RefreshToken = refresh;
          AuthLogica.AllineaScadenza(corrente, DateTime.UtcNow);
          return EsitoSincronizzazione.Adottata;

        case AzioneSincronizzazione.Termina:
          // Logout o accesso con un altro account da un'altra scheda
          await TerminaSessioneAsync(rimuoviToken: false);
          return EsitoSincronizzazione.Terminata;

        default:
          return EsitoSincronizzazione.Invariata;
      }
    }

    private async Task TerminaSessioneAsync(bool rimuoviToken)
    {
      await ClearSessionAsync(rimuoviToken);
      _appState.NotificaSessioneTerminata();
    }

    // ------------------------------------------------------------ token salvati

    private async Task<(string? Access, string? Refresh)> LeggiTokenAsync()
    {
      // Prima il refresh token: chi salva scrive prima l'access token, così
      // non si legge mai un refresh token nuovo con un access token vecchio.
      var refresh = await _storage.GetItem(RefreshTokenKey);
      var access = await _storage.GetItem(AccessTokenKey);
      return (access, refresh);
    }

    private async Task SaveTokensAsync(Session? session)
    {
      if (session?.AccessToken == null || session.RefreshToken == null)
        return;
      await _storage.SetItem(AccessTokenKey, session.AccessToken);
      await _storage.SetItem(RefreshTokenKey, session.RefreshToken);
    }

    /// <summary>Ferma il rinnovo e scollega il client corrente (nessuna attività in background).</summary>
    private void FermaSessioneCorrente()
    {
      _timerRinnovo?.Dispose();
      _timerRinnovo = null;

      var auth = _auth;
      _auth = null;
      if (auth == null)
        return;

      try
      {
        auth.Shutdown();
        auth.ClearStateChangedListeners();
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Chiusura client: {ex.Message}");
      }
    }

    private async Task ClearSessionAsync(bool rimuoviToken)
    {
      FermaSessioneCorrente();

      if (rimuoviToken)
      {
        try
        {
          await _storage.RemoveItem(AccessTokenKey);
          await _storage.RemoveItem(RefreshTokenKey);
        }
        catch (Exception ex)
        {
          Console.WriteLine($"Rimozione token: {ex.Message}");
        }
      }

      ClientHolder.Reset();
      _appState.Clear();
    }
  }

  internal enum AzioneSincronizzazione { Nessuna, Adotta, Termina }

  /// <summary>Logica pura della sessione (scadenze JWT, errori, sincronizzazione), testabile.</summary>
  internal static class AuthLogica
  {
    /// <summary>Il rinnovo si pianifica questo tempo prima della scadenza dell'access token.</summary>
    internal static readonly TimeSpan AnticipoRinnovo = TimeSpan.FromMinutes(5);

    /// <summary>Sotto questa durata residua il token va rinnovato (anticipo + tolleranza).</summary>
    internal static readonly TimeSpan SogliaRinnovo = AnticipoRinnovo + TimeSpan.FromMinutes(1);

    internal static readonly TimeSpan AttesaMassima = TimeSpan.FromHours(4);

    /// <summary>Scadenza (claim exp) di un JWT, senza verificarne la firma.</summary>
    internal static DateTimeOffset? LeggiScadenzaJwt(string? jwt)
    {
      var payload = LeggiPayload(jwt);
      if (payload is { } p && p.TryGetProperty("exp", out var exp) && exp.ValueKind == JsonValueKind.Number
          && exp.TryGetInt64(out var secondi))
        return DateTimeOffset.FromUnixTimeSeconds(secondi);
      return null;
    }

    /// <summary>Utente (claim sub) di un JWT.</summary>
    internal static string? LeggiUtenteJwt(string? jwt)
    {
      var payload = LeggiPayload(jwt);
      if (payload is { } p && p.TryGetProperty("sub", out var sub) && sub.ValueKind == JsonValueKind.String)
        return sub.GetString();
      return null;
    }

    /// <summary>Messaggio per l'utente quando il login (dopo la password) non riesce.</summary>
    internal static string MessaggioErroreLogin(Exception ex) => ex switch
    {
      GotrueException { StatusCode: 429 } => "Troppi tentativi di accesso. Attendi qualche minuto e riprova.",
      _ when IsErroreDefinitivo(ex) => "Accesso non riuscito. Riprova.",
      _ => AuthService.MessaggioServerNonRaggiungibile
    };

    internal static bool DaRinnovare(DateTimeOffset? scadenza, DateTimeOffset adesso) =>
      scadenza == null || scadenza.Value - adesso <= SogliaRinnovo;

    /// <summary>
    /// Attesa prima del prossimo rinnovo. <paramref name="casuale"/> (pochi secondi)
    /// sfasa le schede aperte, così di solito una sola rinnova e le altre adottano.
    /// </summary>
    internal static TimeSpan CalcolaAttesaRinnovo(DateTimeOffset? scadenza, DateTimeOffset adesso, TimeSpan casuale)
    {
      if (scadenza == null)
        return TimeSpan.FromMinutes(1);

      var attesa = scadenza.Value - adesso - AnticipoRinnovo - casuale;
      if (attesa < TimeSpan.Zero)
        return TimeSpan.Zero;
      return attesa > AttesaMassima ? AttesaMassima : attesa;
    }

    /// <summary>
    /// Corregge CreatedAt/ExpiresIn in base al claim exp dell'access token
    /// (gotrue, ripristinando un token ancora valido, mette in ExpiresIn la data assoluta).
    /// </summary>
    internal static void AllineaScadenza(Session session, DateTime adessoUtc)
    {
      var scadenza = LeggiScadenzaJwt(session.AccessToken);
      if (scadenza == null)
        return;

      var adesso = DateTime.SpecifyKind(adessoUtc, DateTimeKind.Utc);
      session.CreatedAt = adesso;
      session.ExpiresIn = Math.Max(0, (long)(scadenza.Value.UtcDateTime - adesso).TotalSeconds);
    }

    /// <summary>Cosa fare quando i token salvati differiscono da quelli in memoria.</summary>
    internal static AzioneSincronizzazione DecidiSincronizzazione(
      string? accessMemoria, string? refreshMemoria, string? accessSalvato, string? refreshSalvato)
    {
      // Token rimossi: logout (o sessione non più valida) in un'altra scheda
      if (string.IsNullOrEmpty(accessSalvato) || string.IsNullOrEmpty(refreshSalvato))
        return AzioneSincronizzazione.Termina;

      if (refreshSalvato == refreshMemoria)
        return AzioneSincronizzazione.Nessuna;

      // Un'altra scheda ha fatto login con un altro account: vale l'ultimo accesso
      var utenteSalvato = LeggiUtenteJwt(accessSalvato);
      if (utenteSalvato == null)
        return AzioneSincronizzazione.Nessuna;
      if (utenteSalvato != LeggiUtenteJwt(accessMemoria))
        return AzioneSincronizzazione.Termina;

      // Si adotta solo una coppia non più vecchia di quella in memoria
      var scadenzaSalvata = LeggiScadenzaJwt(accessSalvato);
      var scadenzaMemoria = LeggiScadenzaJwt(accessMemoria);
      if (scadenzaSalvata == null || (scadenzaMemoria != null && scadenzaSalvata < scadenzaMemoria))
        return AzioneSincronizzazione.Nessuna;

      return AzioneSincronizzazione.Adotta;
    }

    /// <summary>
    /// True se l'errore dice che la sessione non è più valida (token rifiutati,
    /// account disattivato): solo allora si cancellano i token salvati. Rete
    /// assente, timeout ed errori 5xx sono temporanei.
    /// </summary>
    internal static bool IsErroreDefinitivo(Exception ex) => ex switch
    {
      GotrueException g when g.Reason == FailureHint.Reason.Offline => false,
      GotrueException g when g.StatusCode >= 500 || g.StatusCode is 408 or 429 => false,
      GotrueException => true,
      AccountNonAttivoException => true,
      // JWT salvato illeggibile
      ArgumentException => true,
      _ => false
    };

    private static JsonElement? LeggiPayload(string? jwt)
    {
      if (string.IsNullOrEmpty(jwt))
        return null;

      var parti = jwt.Split('.');
      if (parti.Length < 2)
        return null;

      try
      {
        var base64 = parti[1].Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement.Clone() : null;
      }
      catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
      {
        return null;
      }
    }
  }
}
