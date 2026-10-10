using IscrizioneManager.Core.Models;
using IscrizioneManager.Core.Services;

namespace IscrizioneManager.Web.Services
{
  /// <summary>Dati di riferimento del modulo iscrizione per l'evento corrente.</summary>
  public record DatiModulo(
    EventoMetadata Metadata,
    List<AnnoScolastico> AnniScolastici,
    List<Settimana> Settimane,
    List<Taglia> Taglie,
    List<ConsensoDto> Consensi);

  public class ModuloIscrizioneViewModel
  {
    /// <summary>Carica in parallelo tutto quello che serve al modulo.</summary>
    public async Task<DatiModulo> CaricaDatiAsync()
    {
      var metadataTask = IscrizioneCompletaController.GetEventMetadataAsync();
      var settimaneTask = GetSettimaneAsync();
      var taglieTask = GetTaglieAsync();
      var consensiTask = GetConsensiAsync();

      var metadata = await metadataTask;
      var anni = await IscrizioneCompletaController.GetAnniScolasticiAsync(IscrizioneCompletaController.GradiAmmessi(metadata));

      return new DatiModulo(metadata, anni, await settimaneTask, await taglieTask, await consensiTask);
    }

    public async Task<List<Settimana>> GetSettimaneAsync()
    {
      var settimane = await ClientHolder.Client
        .GetAll<Settimana>()
        .Select("*")
        .Get();

      return SettimaneModulo.Espandi(settimane.Models);
    }

    public async Task<List<Taglia>> GetTaglieAsync()
    {
      var taglie = await ClientHolder.Client
        .GetAll<Taglia>()
        .Select("*")
        .Get();
      return taglie.Models.OrderBy(x => x.Id).ToList();
    }

    public async Task<List<ConsensoDto>> GetConsensiAsync()
    {
      var tipiConsensi = await ClientHolder.Client
        .GetAll<TipoConsenso>()
        .Select("*")
        .Get();

      return tipiConsensi.Models
        .OrderBy(x => x.Id)
        .Select(x => new ConsensoDto() { IdTipoConsenso = x.Id, Descrizione = x.Descrizione })
        .ToList();
    }

    /// <summary>Genitori già registrati, solo dell'evento corrente.</summary>
    public async Task<List<GenitoreDto>> GetGenitoriListAsync()
    {
      var eventId = ClientHolder.Client._eventId;
      var genitori = await ClientHolder.Client
        .BaseFrom<Genitore>()
        .Select("*")
        .Where(x => x.event_id == eventId)
        .Get();

      return genitori.Models
        .GroupBy(x => x.Id)
        .Select(g => g.First())
        .OrderBy(x => x.Cognome, StringComparer.CurrentCultureIgnoreCase)
        .ThenBy(x => x.Nome, StringComparer.CurrentCultureIgnoreCase)
        .Select(x => new GenitoreDto
        {
          IdGenitore = x.Id,
          Nome = x.Nome,
          Cognome = x.Cognome,
          Telefono = x.Telefono,
          Genere = x.Genere,
          Existing = true
        })
        .ToList();
    }
  }
}
