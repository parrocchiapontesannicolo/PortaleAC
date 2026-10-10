using IscrizioneManager.Core.Models;
using IscrizioneManager.Core.Services;
using IscrizioniManager.Models;

namespace IscrizioniManager.Core.Services
{
  /// <summary>
  /// Link Google (foglio / cartella Drive) configurati per l'evento corrente.
  /// I link si configurano lato amministrazione: la RLS fa leggere ciascun tipo
  /// solo a chi ha il permesso google.sheet / google.drive.
  /// </summary>
  public class GoogleSheetService
  {
    /// <summary>Link configurato per l'evento corrente, o null se assente.</summary>
    public static async Task<string?> GetUrlAsync(UrlTypes urlType)
    {
      var type = (int)urlType;
      var sheet = (await ClientHolder.Client
          .GetAll<GoogleSheet>()
          .Select("id,url,url_type,event_id")
          .Where(x => x.UrlType == type)
          .Limit(1)
          .Get())
        .Model;

      return string.IsNullOrWhiteSpace(sheet?.Url) ? null : sheet.Url.Trim();
    }

    /// <summary>
    /// Compatibilità con il codice precedente: <paramref name="askUserFunc"/> è
    /// ignorato, il link non viene più creato dall'app.
    /// </summary>
    public static Task<string?> GetOrCreateUrlAsync(Func<Task<string?>>? askUserFunc, UrlTypes urlType) =>
      GetUrlAsync(urlType);
  }
}
