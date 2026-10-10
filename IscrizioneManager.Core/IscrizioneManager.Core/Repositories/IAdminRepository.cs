using IscrizioneManager.Core.Models;
using IscrizioneManager.Core.Models.Admin;
using IscrizioniManager.Models;

namespace IscrizioneManager.Core.Repositories;

/// <summary>
/// Accesso ai dati delle pagine di amministrazione. Lavora su tutti gli eventi
/// (non usa il filtro sull'evento corrente): la RLS lo consente solo al superadmin.
/// </summary>
public interface IAdminRepository
{
  // Account (creazione, password ed eliminazione passano dalla Edge Function)
  Task<List<AppAccount>> GetAccountsAsync();
  Task<NuovoAccountRisultato> CreaAccountAsync(string nome, string password, bool isSuperadmin, int ordine);
  Task ImpostaPasswordAsync(Guid accountId, string password);
  Task EliminaAccountAsync(Guid accountId);
  Task RinominaAccountAsync(Guid accountId, string nome);
  Task ImpostaAccountAttivoAsync(Guid accountId, bool attivo);
  Task ImpostaSuperadminAsync(Guid accountId, bool superadmin);
  Task ImpostaOrdineAsync(Guid accountId, int ordine);

  // Accessi account ↔ evento
  Task<List<AccountEvento>> GetAccessiAsync();
  Task SalvaAccessoAsync(Guid accountId, int eventId, int ruoloId);
  Task RimuoviAccessoAsync(Guid accountId, int eventId);

  // Ruoli
  Task<List<Ruolo>> GetRuoliAsync();
  Task<Ruolo> CreaRuoloAsync(Ruolo ruolo);
  Task AggiornaRuoloAsync(Ruolo ruolo);
  Task EliminaRuoloAsync(int ruoloId);

  // Eventi
  Task<List<Evento>> GetEventiAsync();
  Task<Evento> CreaEventoAsync(string nome);
  Task RinominaEventoAsync(int eventId, string nome);
  Task ImpostaEventoAttivoAsync(int eventId, bool attivo);

  // Configurazione evento
  Task<EventoMetadata?> GetMetadataAsync(int eventId);
  Task CreaMetadataAsync(EventoMetadata metadata);
  Task AggiornaMetadataAsync(EventoMetadata metadata);
  Task<List<AnnoScolastico>> GetAnniScolasticiAsync();

  Task<List<LinkGoogleAdmin>> GetLinkAsync(int eventId);
  Task CreaLinkAsync(LinkGoogleAdmin link);
  Task AggiornaLinkAsync(int linkId, string url);
  Task EliminaLinkAsync(int linkId);

  Task<List<SettimanaAdmin>> GetSettimaneAsync(int eventId);
  Task SalvaSettimanaAsync(SettimanaAdmin settimana);
  Task EliminaSettimanaAsync(int settimanaId);

  Task<List<TagliaAdmin>> GetTaglieAsync(int eventId);
  Task SalvaTagliaAsync(TagliaAdmin taglia);
  Task EliminaTagliaAsync(int tagliaId);

  Task<List<TipoConsensoAdmin>> GetTipiConsensoAsync(int eventId);
  Task SalvaTipoConsensoAsync(TipoConsensoAdmin tipo);
  Task EliminaTipoConsensoAsync(int tipoId);
}
