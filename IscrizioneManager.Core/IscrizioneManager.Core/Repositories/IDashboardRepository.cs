using IscrizioneManager.Core.Models;

namespace IscrizioneManager.Core.Repositories;

/// <summary>Letture leggere (solo le colonne necessarie) per i numeri della home.</summary>
public interface IDashboardRepository
{
    Task<List<Iscrizione>> GetIscrizioniAsync();
    Task<List<AnnoScolastico>> GetAnniScolasticiAsync();
    Task<List<int>> GetIdBambiniAsync();
    Task<List<Squadra>> GetSquadreAsync();
    Task<List<SquadraBambino>> GetSquadraBambiniAsync();
}
