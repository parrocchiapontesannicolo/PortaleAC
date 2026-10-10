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
    var bambiniTask = client.GetAll<Bambino>().Select("*").Get();
    var iscrizioniTask = client.GetAll<Iscrizione>().Select("*").Get();
    var squadraBambinoTask = client.GetAll<SquadraBambino>().Select("*").Get();
    var squadraTask = client.GetAll<Squadra>().Select("*").Order("id_squadra", Ordering.Ascending).Get();
    var anniTask = client.BaseFrom<IscrizioneManager.Core.Models.AnnoScolastico>().Select("*").Get();
    await Task.WhenAll(bambiniTask, iscrizioniTask, squadraBambinoTask, squadraTask, anniTask);

    var bambini = bambiniTask.Result.Models;
    var annoPerBambino = iscrizioniTask.Result.Models
      .GroupBy(i => i.IdBambino)
      .ToDictionary(g => g.Key, g => (int?)g.First().Anno);
    var descAnno = anniTask.Result.Models
      .Where(a => a.Id.HasValue)
      .ToDictionary(a => a.Id!.Value, a => a.Desc ?? "");
    var squadraPerBambino = squadraBambinoTask.Result.Models
      .GroupBy(sb => sb.IdBambino)
      .ToDictionary(g => g.Key, g => g.First().IdSquadra);

    Bambino MakeBambino(Bambino x)
    {
      var anno = annoPerBambino.GetValueOrDefault(x.Id);
      return new Bambino
      {
        Id = x.Id,
        Nome = x.Nome,
        Cognome = x.Cognome,
        Anno = anno,
        AnnoDesc = anno.HasValue ? descAnno.GetValueOrDefault(anno.Value, "") : ""
      };
    }

    var squadre = squadraTask.Result.Models;
    var idSquadre = squadre.Select(s => s.Id).ToHashSet();

    foreach (var s in squadre)
    {
      s.Bambini = bambini
        .Where(b => squadraPerBambino.TryGetValue(b.Id, out var id) && id == s.Id)
        .Select(MakeBambino)
        .ToList();
      s.OrdinaBambini();
    }

    // Senza squadra: anche chi punta a una squadra non più esistente
    var senzaSquadra = new Squadra
    {
      Id = Squadra.IdSenzaSquadra,
      Nome = Squadra.NomeSenzaSquadra,
      Color = Squadra.ColoreSenzaSquadra,
      Bambini = bambini
        .Where(b => !squadraPerBambino.TryGetValue(b.Id, out var id) || !idSquadre.Contains(id))
        .Select(MakeBambino)
        .ToList()
    };
    senzaSquadra.OrdinaBambini();

    var risultato = squadre.ToList();
    risultato.Add(senzaSquadra);
    return risultato;
  }

  public async Task SetSquadraAsync(int bambinoId, int newSquadraId)
  {
    await ClientHolder.Client
      .GetAll<SquadraBambino>()
      .Where(x => x.IdBambino == bambinoId)
      .Delete(Minimal);

    if (newSquadraId != Squadra.IdSenzaSquadra)
      await ClientHolder.Client
        .GetAll<SquadraBambino>()
        .Insert(new SquadraBambino
        {
          IdSquadra = newSquadraId,
          IdBambino = bambinoId,
          event_id = ClientHolder.Client._eventId
        }, Minimal);
  }

  public async Task SaveAllAssignmentsAsync(List<Squadra> squadre)
  {
    var client = ClientHolder.Client;
    var nuove = SquadraDistributor.Assegnazioni(squadre, client._eventId);

    // Copia delle assegnazioni attuali, per ripristinarle se l'inserimento fallisce
    var precedenti = (await client.GetAll<SquadraBambino>().Select("*").Get()).Models;

    // GetAll<T>() filtra già sull'evento corrente: si cancella solo questo evento
    await client.GetAll<SquadraBambino>().Delete(Minimal);

    if (nuove.Count == 0)
      return;

    try
    {
      await client.GetAll<SquadraBambino>().Insert(nuove, Minimal);
    }
    catch (Exception ex)
    {
      if (precedenti.Count > 0)
      {
        try
        {
          await client.GetAll<SquadraBambino>().Insert(precedenti
            .Select(p => new SquadraBambino { IdSquadra = p.IdSquadra, IdBambino = p.IdBambino, event_id = p.event_id })
            .ToList(), Minimal);
        }
        catch (Exception restoreEx)
        {
          throw new InvalidOperationException(
            $"Salvataggio delle squadre non riuscito ({ex.Message}) e ripristino fallito ({restoreEx.Message}).", ex);
        }
      }
      throw new InvalidOperationException(
        $"Salvataggio delle squadre non riuscito: {ex.Message}. Le assegnazioni precedenti sono state ripristinate.", ex);
    }
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
