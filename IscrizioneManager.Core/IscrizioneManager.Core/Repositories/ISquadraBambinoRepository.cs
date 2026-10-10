namespace IscrizioneManager.Core.Repositories;

public interface ISquadraBambinoRepository
{
  /// <summary>Squadre dell'evento corrente con i bambini; l'ultima è "Senza squadra" (Id 0).</summary>
  Task<List<Squadra>> GetSquadreAsync();
  /// <summary>Sposta un bambino (newSquadraId 0 = senza squadra) nell'evento corrente, in modo atomico.</summary>
  Task SetSquadraAsync(int bambinoId, int newSquadraId);
  Task SaveAllAssignmentsAsync(List<Squadra> squadre);

  Task<Squadra> CreaSquadraAsync(string nome, string colore);
  Task ModificaSquadraAsync(int idSquadra, string nome, string colore);
  /// <summary>Elimina la squadra; i suoi bambini tornano "Senza squadra".</summary>
  Task EliminaSquadraAsync(int idSquadra);
}
