using IscrizioneManager.Core.Logic;
using IscrizioneManager.Core.Services;
using IscrizioniManager.Models;
using IscrizioniManager.Utils;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;

namespace IscrizioneManager.Core.Repositories;

public class SupabaseSquadraBambinoRepository : ISquadraBambinoRepository
{
  private static readonly QueryOptions Minimal = new() { Returning = QueryOptions.ReturnType.Minimal };

  public async Task<List<Squadra>> GetSquadreAsync()
  {
    var client = ClientHolder.Client;
    var bambiniTask = LeggiBambiniAsync(client._eventId);
    var squadraBambinoTask = client.GetAll<SquadraBambino>().Select("*").Get();
    var squadraTask = client.GetAll<Squadra>().Select("*").Order("id_squadra", Ordering.Ascending).Get();
    var anniTask = client.BaseFrom<IscrizioneManager.Core.Models.AnnoScolastico>().Select("*").Get();
    await Task.WhenAll(bambiniTask, squadraBambinoTask, squadraTask, anniTask);

    var descAnno = anniTask.Result.Models
      .Where(a => a.Id.HasValue)
      .GroupBy(a => a.Id!.Value)
      .ToDictionary(g => g.Key, g => g.First().Desc ?? "");

    return SquadraDistributor.Componi(
      squadraTask.Result.Models, bambiniTask.Result, squadraBambinoTask.Result.Models, descAnno);
  }

  /// <summary>
  /// Bambini dell'evento con nome, cognome e anno tramite la RPC squadre_bambini:
  /// funziona anche per chi ha solo i permessi sulle squadre.
  /// </summary>
  internal static async Task<List<BambinoSquadre>> LeggiBambiniAsync(int eventId) =>
    await ClientHolder.Client.Rpc<List<BambinoSquadre>>("squadre_bambini", new Dictionary<string, object>
    {
      ["p_event_id"] = eventId,
    }) ?? new List<BambinoSquadre>();

  public async Task SetSquadraAsync(int bambinoId, int newSquadraId)
  {
    var client = ClientHolder.Client;
    // Evento letto una volta sola: un cambio evento a metà non deve mescolare i dati.
    // La RPC cancella e inserisce in un'unica transazione e controlla l'evento.
    var eventId = client._eventId;
    await client.Rpc("sposta_bambino", new Dictionary<string, object>
    {
      ["p_event_id"] = eventId,
      ["p_id_bambino"] = bambinoId,
      ["p_id_squadra"] = newSquadraId,
    });
  }

  public async Task SaveAllAssignmentsAsync(List<Squadra> squadre)
  {
    var client = ClientHolder.Client;
    var eventId = client._eventId;
    var assegnazioni = SquadraDistributor.Assegnazioni(squadre, eventId)
      .Select(a => new { id_squadra = a.IdSquadra, id_bambino = a.IdBambino })
      .ToList();

    // Cancellazione e inserimento in un'unica transazione lato database
    await client.Rpc("salva_squadre", new Dictionary<string, object>
    {
      ["p_event_id"] = eventId,
      ["p_assegnazioni"] = assegnazioni,
    });
  }

  public async Task<Squadra> CreaSquadraAsync(string nome, string colore)
  {
    var risposta = await ClientHolder.Client
      .GetAll<Squadra>()
      .InsertAsync(new Squadra { Nome = nome, Color = colore });

    return risposta.Model
      ?? throw new InvalidOperationException("Squadra non creata: controlla di avere i permessi per gestire le squadre.");
  }

  public async Task ModificaSquadraAsync(int idSquadra, string nome, string colore)
  {
    var risposta = await ClientHolder.Client
      .GetAll<Squadra>()
      .Filter("id_squadra", Operator.Equals, idSquadra)
      .Set(x => x.Nome, nome)
      .Set(x => x.Color, colore)
      .Update();

    if (risposta.Models.Count == 0)
      throw new InvalidOperationException("Squadra non trovata o permessi insufficienti.");
  }

  public async Task EliminaSquadraAsync(int idSquadra)
  {
    await ClientHolder.Client
      .GetAll<SquadraBambino>()
      .Filter("id_squadra", Operator.Equals, idSquadra)
      .Delete(Minimal);

    await ClientHolder.Client
      .GetAll<Squadra>()
      .Filter("id_squadra", Operator.Equals, idSquadra)
      .Delete(Minimal);
  }
}
