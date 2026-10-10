using IscrizioneManager.Core.Models;
using IscrizioneManager.Core.Repositories;
using Newtonsoft.Json;

public class IscrizioneCompletaController
{
    public IscrizioneCompletaController() { }

    internal static IIscrizioneCompletaRepository _repo = new SupabaseIscrizioneCompletaRepository();
    internal static IEventMetadataRepository _metaRepo = new SupabaseEventMetadataRepository();

    public static async Task<EventoMetadata> GetEventMetadataAsync()
        => await _metaRepo.GetEventMetadataAsync();

    /// <summary>Anni scolastici dei gradi ammessi; con l'elenco vuoto restituisce tutti gli anni.</summary>
    public static async Task<List<AnnoScolastico>> GetAnniScolasticiAsync(int[] gradiAmmessi)
        => await _metaRepo.GetAnniScolasticiAsync(gradiAmmessi ?? Array.Empty<int>());

    /// <summary>Gradi scolastici ammessi dall'evento (campo JSON, es. "[1,2]").</summary>
    public static int[] GradiAmmessi(EventoMetadata? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata?.GradiScuolaAllowed))
            return Array.Empty<int>();
        try
        {
            return JsonConvert.DeserializeObject<int[]>(metadata.GradiScuolaAllowed) ?? Array.Empty<int>();
        }
        catch (JsonException)
        {
            return Array.Empty<int>();
        }
    }

    public static async Task<bool> CreateAsync(ModuloIscrizioneDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        return await _repo.CreateAsync(dto);
    }

    public static async Task<bool> UpdateAsync(ModuloIscrizioneDto dto)
    {
        if (dto == null) throw new ArgumentNullException(nameof(dto));
        if (dto.IdBambino == null || dto.IdBambino <= 0) throw new ArgumentException("Iscrizione da modificare non valida", nameof(dto));
        return await _repo.UpdateAsync(dto);
    }

    public static async Task<bool> DeleteAsync(int idBambino)
    {
        if (idBambino <= 0) throw new ArgumentException("Id non valido", nameof(idBambino));
        return await _repo.DeleteAsync(idBambino);
    }

    public static async Task<ModuloIscrizioneDto> GetAsync(int idBambino)
    {
        if (idBambino <= 0) throw new ArgumentException("Id non valido", nameof(idBambino));
        return await _repo.GetAsync(idBambino);
    }
}
