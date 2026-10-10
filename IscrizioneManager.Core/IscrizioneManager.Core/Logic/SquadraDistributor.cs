using IscrizioniManager.Models;

namespace IscrizioneManager.Core.Logic;

public static class SquadraDistributor
{
  /// <summary>
  /// Ridistribuisce TUTTI i bambini (anche quelli già assegnati) tra le squadre
  /// reali, bilanciando per anno scolastico. "Senza squadra" viene svuotata.
  /// Senza squadre reali non cambia nulla.
  /// </summary>
  public static List<Squadra> Distribuisci(
    List<Squadra> squadre,
    Func<IEnumerable<Bambino>, IEnumerable<Bambino>>? shuffler = null)
  {
    shuffler ??= b => b.OrderBy(_ => Guid.NewGuid());

    var targetSquadre = squadre.Where(s => !s.IsSenzaSquadra).ToList();
    if (targetSquadre.Count == 0)
      return squadre;

    var tutti = squadre
      .SelectMany(s => s.Bambini)
      .GroupBy(b => b.Id)
      .Select(g => g.First())
      .ToList();

    foreach (var s in squadre)
      s.Bambini.Clear();

    // Round robin che prosegue tra un anno e l'altro: le squadre restano
    // bilanciate sia per anno sia nel totale.
    var gruppiPerAnno = tutti.GroupBy(b => b.Anno).OrderBy(g => g.Key ?? int.MaxValue);
    int index = 0;

    foreach (var gruppo in gruppiPerAnno)
    {
      foreach (var bambino in shuffler(gruppo))
      {
        targetSquadre[index % targetSquadre.Count].Bambini.Add(bambino);
        index++;
      }
    }

    foreach (var s in targetSquadre)
      s.OrdinaBambini();

    return squadre;
  }

  /// <summary>
  /// Sposta un bambino da una squadra all'altra nel modello locale.
  /// Restituisce il bambino spostato, o null se non trovato o se origine = destinazione.
  /// </summary>
  public static Bambino? Sposta(List<Squadra> squadre, int bambinoId, int daSquadraId, int aSquadraId)
  {
    if (daSquadraId == aSquadraId)
      return null;

    var da = squadre.SingleOrDefault(s => s.Id == daSquadraId);
    var a = squadre.SingleOrDefault(s => s.Id == aSquadraId);
    var bambino = da?.Bambini.FirstOrDefault(b => b.Id == bambinoId);
    if (da == null || a == null || bambino == null)
      return null;

    da.Bambini.Remove(bambino);
    a.Bambini.Add(bambino);
    a.OrdinaBambini();
    return bambino;
  }

  /// <summary>
  /// Righe squadra_bambino corrispondenti allo stato delle squadre (esclusa
  /// "Senza squadra"). Un bambino presente in più squadre viene assegnato alla prima.
  /// </summary>
  public static List<SquadraBambino> Assegnazioni(IEnumerable<Squadra> squadre, int eventId)
  {
    var visti = new HashSet<int>();
    var righe = new List<SquadraBambino>();

    foreach (var s in squadre.Where(s => !s.IsSenzaSquadra))
      foreach (var b in s.Bambini)
        if (visti.Add(b.Id))
          righe.Add(new SquadraBambino { IdSquadra = s.Id, IdBambino = b.Id, event_id = eventId });

    return righe;
  }
}
