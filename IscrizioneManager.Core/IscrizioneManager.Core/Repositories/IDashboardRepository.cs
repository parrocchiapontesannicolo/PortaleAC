using IscrizioneManager.Core.Models;

namespace IscrizioneManager.Core.Repositories;

/// <summary>Letture leggere (solo le colonne necessarie) per i numeri della home.</summary>
public interface IDashboardRepository
{
    Task<List<Iscrizione>> GetIscrizioniAsync();
    Task<List<AnnoScolastico>> GetAnniScolasticiAsync();
    /// <summary>Id dei bambini dalla tabella: richiede i permessi sugli iscritti.</summary>
    Task<List<int>> GetIdBambiniAsync();

    /// <summary>Id dei bambini tramite la RPC squadre_bambini: basta il permesso sulle squadre.</summary>
    Task<List<int>> GetIdBambiniSquadreAsync();

    Task<List<Squadra>> GetSquadreAsync();
    Task<List<SquadraBambino>> GetSquadraBambiniAsync();
}
