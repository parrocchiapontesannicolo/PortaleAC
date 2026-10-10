using IscrizioniManager.Models;

namespace IscrizioneManager.Core.Repositories;

public interface IIscrittiListaRepository
{
    Task<List<Bambino>> GetBambiniAsync();
    Task<List<Iscrizione>> GetIscrizioniAsync();
    Task SetPagatoAsync(int idIscrizione, bool pagato);
}
