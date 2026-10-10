using IscrizioniManager.Data;

namespace IscrizioneManager.Core.Services
{
  /// <summary>
  /// Client autenticato corrente. Viene impostato da AuthService al login (o al
  /// ripristino della sessione) e azzerato al logout.
  /// </summary>
  public static class ClientHolder
  {
    private static EventSupabaseClient? _client;

    public static EventSupabaseClient Client =>
      _client ?? throw new InvalidOperationException("Sessione non inizializzata: effettua il login.");

    public static bool IsInitialized => _client != null;

    public static void Initialize(EventSupabaseClient client)
    {
      _client = client;
    }

    public static void Reset()
    {
      _client = null;
    }
  }
}
