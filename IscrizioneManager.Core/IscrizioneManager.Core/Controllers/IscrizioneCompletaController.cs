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
        // update_iscrizione riscrive i collegamenti: senza tutti i genitori li perderebbe
        if (dto.GenitoriNonLeggibili > 0) throw new InvalidOperationException(MessaggioGenitoriNonLeggibili(dto.GenitoriNonLeggibili));
        return await _repo.UpdateAsync(dto);
    }

    public static string MessaggioGenitoriNonLeggibili(int quanti) =>
        (quanti == 1
            ? "Un genitore collegato a questo iscritto non è visibile con i tuoi permessi"
            : $"{quanti} genitori collegati a questo iscritto non sono visibili con i tuoi permessi")
        + ": salvando si perderebbe il collegamento. Chiedi a un amministratore di sistemare l'iscrizione.";

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
