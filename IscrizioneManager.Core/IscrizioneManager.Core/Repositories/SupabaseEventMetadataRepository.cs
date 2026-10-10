using IscrizioneManager.Core.Models;
using IscrizioneManager.Core.Services;

namespace IscrizioneManager.Core.Repositories;

public class SupabaseEventMetadataRepository : IEventMetadataRepository
{
    public async Task<EventoMetadata> GetEventMetadataAsync()
    {
        var result = await ClientHolder.Client
            .GetAll<EventoMetadata>()
            .Select("*")
            .Get();

        // Evento senza configurazione: tutte le sezioni opzionali spente
        return result.Model ?? new EventoMetadata { event_id = ClientHolder.Client._eventId };
    }

    public async Task<List<AnnoScolastico>> GetAnniScolasticiAsync(int[] gradiAmmessi)
    {
        var result = await ClientHolder.Client
            .BaseFrom<AnnoScolastico>()
            .Select("*")
            .Get();
        return result.Models
            .Where(x => gradiAmmessi.Length == 0 || gradiAmmessi.Contains(x.GradoScuola))
            .OrderBy(x => x.Id)
            .ToList();
    }
}
