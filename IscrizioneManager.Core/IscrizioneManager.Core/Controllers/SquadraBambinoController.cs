using IscrizioneManager.Core.Repositories;

public class SquadraBambinoController
{
  public const int LunghezzaMassimaNome = 40;

  internal static ISquadraBambinoRepository _repo = new SupabaseSquadraBambinoRepository();

  public static async Task<List<Squadra>> GetSquadreAsync()
    => await _repo.GetSquadreAsync();

  public static async Task SetSquadraAsync(int bambinoId, int newSquadraId)
    => await _repo.SetSquadraAsync(bambinoId, newSquadraId);

  public static async Task SaveAllAssignmentsAsync(List<Squadra> squadre)
    => await _repo.SaveAllAssignmentsAsync(squadre);

  public static async Task<Squadra> CreaSquadraAsync(string? nome, string? colore)
  {
    var (n, c) = Valida(nome, colore);
    return await _repo.CreaSquadraAsync(n, c);
  }

  public static async Task ModificaSquadraAsync(int idSquadra, string? nome, string? colore)
  {
    if (idSquadra == Squadra.IdSenzaSquadra)
      throw new ArgumentException("\"Senza squadra\" non è una squadra modificabile.");

    var (n, c) = Valida(nome, colore);
    await _repo.ModificaSquadraAsync(idSquadra, n, c);
  }

  public static async Task EliminaSquadraAsync(int idSquadra)
  {
    if (idSquadra == Squadra.IdSenzaSquadra)
      throw new ArgumentException("\"Senza squadra\" non si può eliminare.");

    await _repo.EliminaSquadraAsync(idSquadra);
  }

  /// <summary>Nome obbligatorio (spazi rimossi), colore normalizzato in #RRGGBB.</summary>
  internal static (string Nome, string Colore) Valida(string? nome, string? colore)
  {
    var n = (nome ?? string.Empty).Trim();
    if (n.Length == 0)
      throw new ArgumentException("Il nome della squadra è obbligatorio.");
    if (n.Length > LunghezzaMassimaNome)
      throw new ArgumentException($"Il nome della squadra può avere al massimo {LunghezzaMassimaNome} caratteri.");
    if (string.Equals(n, Squadra.NomeSenzaSquadra, StringComparison.OrdinalIgnoreCase))
      throw new ArgumentException($"\"{Squadra.NomeSenzaSquadra}\" è un nome riservato.");

    var c = Squadra.NormalizzaColore(colore)
      ?? throw new ArgumentException("Colore non valido: usa il formato #RRGGBB.");

    return (n, c);
  }
}
