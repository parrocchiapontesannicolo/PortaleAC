using IscrizioneManager.Core.Services;
using Microsoft.JSInterop;
using MudBlazor;

namespace IscrizioneManager.Web.Services
{
  public enum PreferenzaTema { Automatico, Chiaro, Scuro }

  /// <summary>
  /// Tema chiaro/scuro condiviso da login e layout principale. Senza una scelta
  /// esplicita segue l'impostazione del dispositivo (anche se cambia mentre l'app è aperta).
  /// </summary>
  public sealed class TemaService
  {
    // Stessa chiave della v2 iniziale: "1" scuro, "0" chiaro, assente = automatico
    private const string Chiave = "darkMode";

    private readonly LocalStorageService _storage;
    private readonly IJSRuntime _js;
    private bool _sistemaScuro;

    public TemaService(LocalStorageService storage, IJSRuntime js)
    {
      _storage = storage;
      _js = js;
    }

    public PreferenzaTema Preferenza { get; private set; }

    public bool IsDarkMode => Preferenza switch
    {
      PreferenzaTema.Scuro => true,
      PreferenzaTema.Chiaro => false,
      _ => _sistemaScuro,
    };

    public event Action? Changed;

    /// <summary>Letto prima del primo render, così la pagina non lampeggia in chiaro.</summary>
    public async Task CaricaAsync()
    {
      try
      {
        Preferenza = await _storage.GetItem(Chiave) switch
        {
          "1" => PreferenzaTema.Scuro,
          "0" => PreferenzaTema.Chiaro,
          _ => PreferenzaTema.Automatico,
        };
        _sistemaScuro = await _js.InvokeAsync<bool>("temaSistemaScuro");
      }
      catch (JSException)
      {
        // Storage non disponibile (es. navigazione privata restrittiva): tema chiaro
      }
    }

    /// <summary>Segue i cambi di tema del dispositivo tramite il MudThemeProvider.</summary>
    public Task OsservaSistemaAsync(MudThemeProvider provider)
        => provider.WatchSystemDarkModeAsync(scuro =>
        {
          _sistemaScuro = scuro;
          Changed?.Invoke();
          return Task.CompletedTask;
        });

    public async Task ImpostaAsync(PreferenzaTema preferenza)
    {
      Preferenza = preferenza;
      Changed?.Invoke();
      try
      {
        if (preferenza == PreferenzaTema.Automatico)
          await _storage.RemoveItem(Chiave);
        else
          await _storage.SetItem(Chiave, preferenza == PreferenzaTema.Scuro ? "1" : "0");
      }
      catch (JSException)
      {
        // La scelta vale comunque per questa sessione
      }
    }
  }
}
