using IscrizioneManager.Core.Security;
using IscrizioniManager.Data;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using Supabase.Gotrue.Interfaces;
using static Supabase.Gotrue.Constants;

namespace IscrizioneManager.Core.Services
{
  /// <summary>
  /// Login con account condiviso + password, ripristino della sessione salvata
  /// e logout. L'evento si sceglie dopo il login (AppState).
  /// </summary>
  public class AuthService
  {
    private const string AccessTokenKey = "sb.accessToken";
    private const string RefreshTokenKey = "sb.refreshToken";

    // Chiavi della versione precedente, rimosse al primo avvio
    private static readonly string[] LegacyKeys = { "jwtToken", "refreshToken", "eventId", "eventDesc", "roleId" };

    private readonly LocalStorageService _storage;
    private readonly AppState _appState;

    public AuthService(LocalStorageService storage, AppState appState)
    {
      _storage = storage;
      _appState = appState;
    }

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

      await StartSessionAsync(supabase);
    }

    /// <summary>Ripristina la sessione salvata. Restituisce false se non c'è o è scaduta.</summary>
    public async Task<bool> TryRestoreSessionAsync()
    {
      foreach (var key in LegacyKeys)
        await _storage.RemoveItem(key);

      var access = await _storage.GetItem(AccessTokenKey);
      var refresh = await _storage.GetItem(RefreshTokenKey);
      if (string.IsNullOrEmpty(access) || string.IsNullOrEmpty(refresh))
        return false;

      try
      {
        var supabase = CreateClient();
        await supabase.Auth.SetSession(access, refresh);
        await StartSessionAsync(supabase);
        return true;
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Ripristino sessione fallito: {ex.Message}");
        await ClearSessionAsync();
        return false;
      }
    }

    public async Task SignOutAsync()
    {
      if (ClientHolder.IsInitialized)
      {
        try { await ClientHolder.Client.Auth.SignOut(); }
        catch (Exception ex) { Console.WriteLine($"Logout: {ex.Message}"); }
      }
      await ClearSessionAsync();
    }

    private static Supabase.Client CreateClient() =>
      new(SupabaseConfig.Url, SupabaseConfig.AnonKey, new SupabaseOptions { AutoRefreshToken = true, AutoConnectRealtime = false });

    private async Task StartSessionAsync(Supabase.Client supabase)
    {
      var eventClient = new EventSupabaseClient(
        auth: supabase.Auth,
        realtime: supabase.Realtime,
        functions: supabase.Functions,
        postgrest: supabase.Postgrest,
        storage: supabase.Storage,
        options: new SupabaseOptions(),
        eventId: 0);

      ClientHolder.Initialize(eventClient);
      await SaveTokensAsync(supabase.Auth.CurrentSession);
      supabase.Auth.AddStateChangedListener(OnAuthStateChanged);

      try
      {
        await _appState.LoadAsync();
      }
      catch
      {
        await ClearSessionAsync();
        throw;
      }
    }

    private async void OnAuthStateChanged(IGotrueClient<User, Session> sender, AuthState state)
    {
      try
      {
        if (state == AuthState.TokenRefreshed || state == AuthState.SignedIn)
          await SaveTokensAsync(sender.CurrentSession);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Salvataggio token fallito: {ex.Message}");
      }
    }

    private async Task SaveTokensAsync(Session? session)
    {
      if (session?.AccessToken == null || session.RefreshToken == null)
        return;
      await _storage.SetItem(AccessTokenKey, session.AccessToken);
      await _storage.SetItem(RefreshTokenKey, session.RefreshToken);
    }

    private async Task ClearSessionAsync()
    {
      if (ClientHolder.IsInitialized)
        ClientHolder.Client.Auth.ClearStateChangedListeners();

      await _storage.RemoveItem(AccessTokenKey);
      await _storage.RemoveItem(RefreshTokenKey);
      ClientHolder.Reset();
      _appState.Clear();
    }
  }
}
