namespace IscrizioneManager.Core.Repositories;

public interface IIscrizioneCompletaRepository
{
    Task<bool> CreateAsync(ModuloIscrizioneDto dto);
    Task<bool> UpdateAsync(ModuloIscrizioneDto dto);
    Task<bool> DeleteAsync(int idBambino);
    Task<ModuloIscrizioneDto> GetAsync(int idBambino);
}

/// <summary>
/// L'iscrizione è stata salvata ma il flag "pagato" no: va trattata come un
/// salvataggio riuscito (non ripetere l'inserimento) con un avviso.
/// </summary>
public class PagatoNonAggiornatoException : Exception
{
    public PagatoNonAggiornatoException(Exception inner)
        : base($"Iscrizione salvata, ma lo stato del pagamento non è stato aggiornato: {inner.Message}", inner) { }
}
