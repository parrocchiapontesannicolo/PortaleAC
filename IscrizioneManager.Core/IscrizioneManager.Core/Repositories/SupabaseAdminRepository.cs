using IscrizioneManager.Core.Models;
using IscrizioneManager.Core.Models.Admin;
using IscrizioneManager.Core.Services;
using IscrizioniManager.Models;
using Newtonsoft.Json;
using Supabase.Postgrest.Models;
using static Supabase.Functions.Client;
using static Supabase.Postgrest.Constants;

namespace IscrizioneManager.Core.Repositories;

public class SupabaseAdminRepository : IAdminRepository
{
  private const string FunzioneAccount = "admin-accounts";

  private static Supabase.Interfaces.ISupabaseTable<T, Supabase.Realtime.RealtimeChannel> Tabella<T>() where T : BaseModel, new() =>
    ClientHolder.Client.BaseFrom<T>();

  // ---------------------------------------------------------------- account

  public async Task<List<AppAccount>> GetAccountsAsync() =>
    (await Tabella<AppAccount>().Get()).Models;

  public async Task<NuovoAccountRisultato> CreaAccountAsync(string nome, string password, bool isSuperadmin, int ordine)
  {
    var json = await InvocaFunzioneAsync(new Dictionary<string, object>
    {
      ["action"] = "create",
      ["nome"] = nome,
      ["password"] = password,
      ["is_superadmin"] = isSuperadmin,
      ["ordine"] = ordine,
    });
    return JsonConvert.DeserializeObject<NuovoAccountRisultato>(json)
      ?? throw new InvalidOperationException("Risposta non valida dalla funzione admin-accounts.");
  }

  public async Task ImpostaPasswordAsync(Guid accountId, string password) =>
    await InvocaFunzioneAsync(new Dictionary<string, object>
    {
      ["action"] = "set_password",
      ["account_id"] = accountId.ToString(),
      ["password"] = password,
    });

  public async Task EliminaAccountAsync(Guid accountId) =>
    await InvocaFunzioneAsync(new Dictionary<string, object>
    {
      ["action"] = "delete",
      ["account_id"] = accountId.ToString(),
    });

  public async Task RinominaAccountAsync(Guid accountId, string nome) =>
    await Tabella<AppAccount>().Filter("id", Operator.Equals, accountId.ToString()).Set(a => a.Nome, nome).Update();

  public async Task ImpostaAccountAttivoAsync(Guid accountId, bool attivo) =>
    await Tabella<AppAccount>().Filter("id", Operator.Equals, accountId.ToString()).Set(a => a.Attivo, attivo).Update();

  public async Task ImpostaSuperadminAsync(Guid accountId, bool superadmin) =>
    await Tabella<AppAccount>().Filter("id", Operator.Equals, accountId.ToString()).Set(a => a.IsSuperadmin, superadmin).Update();

  public async Task ImpostaOrdineAsync(Guid accountId, int ordine) =>
    await Tabella<AppAccount>().Filter("id", Operator.Equals, accountId.ToString()).Set(a => a.Ordine, ordine).Update();

  private static async Task<string> InvocaFunzioneAsync(Dictionary<string, object> body)
  {
    var client = ClientHolder.Client;
    var token = client.Auth.CurrentSession?.AccessToken
      ?? throw new InvalidOperationException("Sessione scaduta: esci e rientra.");

    // Il token dell'utente va passato esplicitamente: la funzione verifica che sia un superadmin
    var options = new InvokeFunctionOptions
    {
      Body = body,
      Headers = new Dictionary<string, string> { ["apikey"] = SupabaseConfig.AnonKey },
    };

    try
    {
      return await client.Functions.Invoke(FunzioneAccount, token, options);
    }
    catch (HttpRequestException ex)
    {
      throw new InvalidOperationException(
        "Impossibile contattare la funzione admin-accounts: verifica che sia pubblicata su Supabase. " + ex.Message, ex);
    }
  }

  // ---------------------------------------------------------------- accessi

  public async Task<List<AccountEvento>> GetAccessiAsync() =>
    (await Tabella<AccountEvento>().Get()).Models;

  public async Task SalvaAccessoAsync(Guid accountId, int eventId, int ruoloId) =>
    await Tabella<AccountEvento>().Upsert(new AccountEvento { AccountId = accountId, EventId = eventId, RuoloId = ruoloId });

  public async Task RimuoviAccessoAsync(Guid accountId, int eventId) =>
    await Tabella<AccountEvento>()
      .Filter("account_id", Operator.Equals, accountId.ToString())
      .Filter("event_id", Operator.Equals, eventId.ToString())
      .Delete();

  // ---------------------------------------------------------------- ruoli

  public async Task<List<Ruolo>> GetRuoliAsync() =>
    (await Tabella<Ruolo>().Order("nome", Ordering.Ascending).Get()).Models;

  public async Task<Ruolo> CreaRuoloAsync(Ruolo ruolo)
  {
    var res = await Tabella<Ruolo>().Insert(ruolo);
    return res.Model ?? throw new InvalidOperationException("Il ruolo non è stato creato.");
  }

  public async Task AggiornaRuoloAsync(Ruolo ruolo) =>
    await Tabella<Ruolo>().Update(ruolo);

  public async Task EliminaRuoloAsync(int ruoloId) =>
    await Tabella<Ruolo>().Filter("id", Operator.Equals, ruoloId.ToString()).Delete();

  // ---------------------------------------------------------------- eventi

  public async Task<List<Evento>> GetEventiAsync() =>
    (await Tabella<Evento>().Get()).Models;

  public async Task<Evento> CreaEventoAsync(string nome)
  {
    var res = await Tabella<Evento>().Insert(new Evento { Nome = nome, IsActive = 1 });
    return res.Model ?? throw new InvalidOperationException("L'evento non è stato creato.");
  }

  public async Task RinominaEventoAsync(int eventId, string nome) =>
    await Tabella<Evento>().Filter("id_evento", Operator.Equals, eventId.ToString()).Set(e => e.Nome, nome).Update();

  public async Task ImpostaEventoAttivoAsync(int eventId, bool attivo) =>
    await Tabella<Evento>().Filter("id_evento", Operator.Equals, eventId.ToString()).Set(e => e.IsActive, attivo ? 1 : 0).Update();

  // ---------------------------------------------------------------- configurazione evento

  public async Task<EventoMetadata?> GetMetadataAsync(int eventId) =>
    (await Tabella<EventoMetadata>().Filter("event_id", Operator.Equals, eventId.ToString()).Get()).Models.FirstOrDefault();

  public async Task CreaMetadataAsync(EventoMetadata metadata) =>
    await Tabella<EventoMetadata>().Insert(metadata);

  public async Task AggiornaMetadataAsync(EventoMetadata metadata) =>
    await Tabella<EventoMetadata>().Update(metadata);

  public async Task<List<AnnoScolastico>> GetAnniScolasticiAsync() =>
    (await Tabella<AnnoScolastico>().Get()).Models.OrderBy(a => a.GradoScuola).ThenBy(a => a.Id).ToList();

  public async Task<List<LinkGoogleAdmin>> GetLinkAsync(int eventId) =>
    (await Tabella<LinkGoogleAdmin>().Filter("event_id", Operator.Equals, eventId.ToString()).Get()).Models;

  public async Task CreaLinkAsync(LinkGoogleAdmin link) =>
    await Tabella<LinkGoogleAdmin>().Insert(link);

  public async Task AggiornaLinkAsync(int linkId, string url) =>
    await Tabella<LinkGoogleAdmin>().Filter("id", Operator.Equals, linkId.ToString()).Set(l => l.Url, url).Update();

  public async Task EliminaLinkAsync(int linkId) =>
    await Tabella<LinkGoogleAdmin>().Filter("id", Operator.Equals, linkId.ToString()).Delete();

  public async Task<List<SettimanaAdmin>> GetSettimaneAsync(int eventId) =>
    (await Tabella<SettimanaAdmin>().Filter("event_id", Operator.Equals, eventId.ToString()).Get()).Models
      .OrderBy(s => s.DataInizio).ToList();

  public async Task SalvaSettimanaAsync(SettimanaAdmin settimana)
  {
    if (settimana.Id == 0)
      await Tabella<SettimanaAdmin>().Insert(settimana);
    else
      await Tabella<SettimanaAdmin>().Update(settimana);
  }

  public async Task EliminaSettimanaAsync(int settimanaId) =>
    await Tabella<SettimanaAdmin>().Filter("id_settimana", Operator.Equals, settimanaId.ToString()).Delete();

  public async Task<List<TagliaAdmin>> GetTaglieAsync(int eventId) =>
    (await Tabella<TagliaAdmin>().Filter("event_id", Operator.Equals, eventId.ToString()).Get()).Models
      .OrderBy(t => t.Id).ToList();

  public async Task SalvaTagliaAsync(TagliaAdmin taglia)
  {
    if (taglia.Id == 0)
      await Tabella<TagliaAdmin>().Insert(taglia);
    else
      await Tabella<TagliaAdmin>().Update(taglia);
  }

  public async Task EliminaTagliaAsync(int tagliaId) =>
    await Tabella<TagliaAdmin>().Filter("id_taglia", Operator.Equals, tagliaId.ToString()).Delete();

  public async Task<List<TipoConsensoAdmin>> GetTipiConsensoAsync(int eventId) =>
    (await Tabella<TipoConsensoAdmin>().Filter("event_id", Operator.Equals, eventId.ToString()).Get()).Models
      .OrderBy(t => t.Id).ToList();

  public async Task SalvaTipoConsensoAsync(TipoConsensoAdmin tipo)
  {
    if (tipo.Id == 0)
      await Tabella<TipoConsensoAdmin>().Insert(tipo);
    else
      await Tabella<TipoConsensoAdmin>().Update(tipo);
  }

  public async Task EliminaTipoConsensoAsync(int tipoId) =>
    await Tabella<TipoConsensoAdmin>().Filter("id_tipo_consenso", Operator.Equals, tipoId.ToString()).Delete();
}
