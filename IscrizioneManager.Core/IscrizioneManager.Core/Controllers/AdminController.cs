using IscrizioneManager.Core.Logic;
using IscrizioneManager.Core.Models;
using IscrizioneManager.Core.Models.Admin;
using IscrizioneManager.Core.Repositories;
using IscrizioniManager.Models;

namespace IscrizioniManager.Controllers
{
  /// <summary>Evento appena creato; <see cref="Avviso"/> se la configurazione iniziale è incompleta.</summary>
  public record EventoCreato(Evento Evento, string? Avviso);

  /// <summary>Operazioni delle pagine di amministrazione (solo super amministratori).</summary>
  public static class AdminController
  {
    internal static IAdminRepository _repo = new SupabaseAdminRepository();

    // ---------------------------------------------------------------- account

    public static async Task<List<AppAccount>> GetAccountsAsync() =>
      AdminLogica.Ordina(await _repo.GetAccountsAsync());

    public static async Task<NuovoAccountRisultato> CreaAccountAsync(string nome, string password, bool superadmin, IReadOnlyCollection<AppAccount> esistenti)
    {
      nome = ValidaNomeUnivoco(nome, esistenti.Select(a => a.Nome), "un account");
      Valida(AdminLogica.ValidaPassword(password));
      return await _repo.CreaAccountAsync(nome, password, superadmin, AdminLogica.ProssimoOrdine(esistenti));
    }

    public static async Task CambiaPasswordAsync(Guid accountId, string password)
    {
      Valida(AdminLogica.ValidaPassword(password));
      await _repo.ImpostaPasswordAsync(accountId, password);
    }

    public static async Task RinominaAccountAsync(AppAccount account, string nome, IReadOnlyCollection<AppAccount> esistenti)
    {
      nome = ValidaNomeUnivoco(nome, esistenti.Where(a => a.Id != account.Id).Select(a => a.Nome), "un account");
      await _repo.RinominaAccountAsync(account.Id, nome);
      account.Nome = nome;
    }

    public static async Task EliminaAccountAsync(Guid accountId, Guid accountCorrenteId)
    {
      if (accountId == accountCorrenteId)
        throw new InvalidOperationException("Non puoi eliminare l'account con cui sei collegato.");
      await _repo.EliminaAccountAsync(accountId);
    }

    public static async Task ImpostaAttivoAsync(AppAccount account, bool attivo, Guid accountCorrenteId)
    {
      if (!attivo && account.Id == accountCorrenteId)
        throw new InvalidOperationException("Non puoi disattivare l'account con cui sei collegato.");
      await _repo.ImpostaAccountAttivoAsync(account.Id, attivo);
      account.Attivo = attivo;
    }

    public static async Task ImpostaSuperadminAsync(AppAccount account, bool superadmin, Guid accountCorrenteId)
    {
      if (!superadmin && account.Id == accountCorrenteId)
        throw new InvalidOperationException("Non puoi togliere a te stesso il ruolo di super amministratore: fallo da un altro account.");
      await _repo.ImpostaSuperadminAsync(account.Id, superadmin);
      account.IsSuperadmin = superadmin;
    }

    /// <summary>Sposta un account nella tendina di login e rinumera l'ordine.</summary>
    public static async Task SpostaAccountAsync(IReadOnlyList<AppAccount> ordinati, int indice, int delta)
    {
      foreach (var (id, ordine) in AdminLogica.Sposta(ordinati, indice, delta))
      {
        await _repo.ImpostaOrdineAsync(id, ordine);
        ordinati.First(a => a.Id == id).Ordine = ordine;
      }
    }

    // ---------------------------------------------------------------- accessi

    public static Task<List<AccountEvento>> GetAccessiAsync() => _repo.GetAccessiAsync();

    /// <summary>Salva i ruoli per evento di un account. Restituisce il numero di modifiche.</summary>
    public static async Task<int> SalvaAccessiAsync(Guid accountId, IReadOnlyDictionary<int, int> attuali, IReadOnlyDictionary<int, int?> desiderati)
    {
      var modifiche = AdminLogica.CalcolaModificheAccessi(attuali, desiderati);
      foreach (var (eventId, ruoloId) in modifiche.DaSalvare)
        await _repo.SalvaAccessoAsync(accountId, eventId, ruoloId);
      foreach (var eventId in modifiche.DaRimuovere)
        await _repo.RimuoviAccessoAsync(accountId, eventId);
      return modifiche.DaSalvare.Count + modifiche.DaRimuovere.Count;
    }

    // ---------------------------------------------------------------- ruoli

    public static Task<List<Ruolo>> GetRuoliAsync() => _repo.GetRuoliAsync();

    public static async Task<Ruolo> SalvaRuoloAsync(Ruolo ruolo, IReadOnlyCollection<Ruolo> esistenti)
    {
      var daSalvare = ruolo.Copia();
      daSalvare.Nome = ValidaNomeUnivoco(ruolo.Nome, esistenti.Where(r => r.Id != ruolo.Id).Select(r => r.Nome), "un ruolo");
      daSalvare.Descrizione = string.IsNullOrWhiteSpace(ruolo.Descrizione) ? null : ruolo.Descrizione.Trim();
      daSalvare.Permessi = AdminLogica.NormalizzaPermessi(ruolo.Permessi);

      if (daSalvare.Id == 0)
        return await _repo.CreaRuoloAsync(daSalvare);

      await _repo.AggiornaRuoloAsync(daSalvare);
      return daSalvare;
    }

    public static async Task EliminaRuoloAsync(int ruoloId)
    {
      var usi = (await _repo.GetAccessiAsync()).Count(a => a.RuoloId == ruoloId);
      if (usi > 0)
        throw new InvalidOperationException(
          $"Il ruolo è assegnato a {usi} {(usi == 1 ? "accesso" : "accessi")} account/evento: cambia o rimuovi quegli accessi prima di eliminarlo.");
      await _repo.EliminaRuoloAsync(ruoloId);
    }

    // ---------------------------------------------------------------- eventi

    /// <summary>Eventi attivi prima, poi i più recenti.</summary>
    public static async Task<List<Evento>> GetEventiAsync() =>
      (await _repo.GetEventiAsync()).OrderByDescending(e => e.IsActive).ThenByDescending(e => e.Id).ToList();

    /// <summary>
    /// Crea l'evento e la sua configurazione. Con <paramref name="copiaDaEventId"/> copia
    /// impostazioni del modulo, taglie e tipi di consenso da un altro evento e, con
    /// <paramref name="copiaAccessi"/>, anche gli account abilitati con il loro ruolo.
    /// </summary>
    public static async Task<EventoCreato> CreaEventoAsync(string nome, int? copiaDaEventId, IReadOnlyCollection<Evento> esistenti,
      bool copiaAccessi = false)
    {
      nome = ValidaNomeUnivoco(nome, esistenti.Select(e => e.Nome), "un evento");
      var evento = await _repo.CreaEventoAsync(nome);

      try
      {
        EventoMetadata? origine = copiaDaEventId is int id ? await _repo.GetMetadataAsync(id) : null;
        EventoMetadata metadata;
        if (origine != null)
        {
          metadata = AdminLogica.CopiaMetadata(origine, evento.Id);
        }
        else
        {
          var anni = await _repo.GetAnniScolasticiAsync();
          metadata = AdminLogica.MetadataPredefinito(evento.Id, anni.Select(a => a.GradoScuola));
        }
        await _repo.CreaMetadataAsync(metadata);

        if (copiaDaEventId is int sorgente)
        {
          foreach (var t in await _repo.GetTaglieAsync(sorgente))
            await _repo.SalvaTagliaAsync(new TagliaAdmin { EventId = evento.Id, Codice = t.Codice, Descrizione = t.Descrizione });
          foreach (var c in await _repo.GetTipiConsensoAsync(sorgente))
            await _repo.SalvaTipoConsensoAsync(new TipoConsensoAdmin { EventId = evento.Id, Descrizione = c.Descrizione });
          if (copiaAccessi)
          {
            foreach (var a in (await _repo.GetAccessiAsync()).Where(a => a.EventId == sorgente))
              await _repo.SalvaAccessoAsync(a.AccountId, evento.Id, a.RuoloId);
          }
        }
      }
      catch (Exception ex)
      {
        // L'evento esiste già: non si annulla, si segnala cosa manca
        return new EventoCreato(evento,
          $"L'evento è stato creato ma la configurazione iniziale non è completa ({AdminLogica.MessaggioErrore(ex)}). Completala da \"Configura\".");
      }

      return new EventoCreato(evento, null);
    }

    public static async Task RinominaEventoAsync(Evento evento, string nome, IReadOnlyCollection<Evento> esistenti)
    {
      nome = ValidaNomeUnivoco(nome, esistenti.Where(e => e.Id != evento.Id).Select(e => e.Nome), "un evento");
      await _repo.RinominaEventoAsync(evento.Id, nome);
      evento.Nome = nome;
    }

    public static async Task ImpostaEventoAttivoAsync(Evento evento, bool attivo)
    {
      await _repo.ImpostaEventoAttivoAsync(evento.Id, attivo);
      evento.IsActive = attivo ? 1 : 0;
    }

    // ---------------------------------------------------------------- configurazione evento

    public static Task<EventoMetadata?> GetMetadataAsync(int eventId) => _repo.GetMetadataAsync(eventId);

    public static Task<List<AnnoScolastico>> GetAnniScolasticiAsync() => _repo.GetAnniScolasticiAsync();

    /// <summary>Salva la configurazione del modulo; la crea se l'evento non l'ha ancora.</summary>
    public static async Task<EventoMetadata> SalvaMetadataAsync(int eventId, EventoMetadata metadata, IEnumerable<int> gradi)
    {
      metadata.event_id = eventId;
      metadata.GradiScuolaAllowed = AdminLogica.SerializzaGradi(gradi);

      if (metadata.Id != 0)
      {
        await _repo.AggiornaMetadataAsync(metadata);
        return metadata;
      }

      await _repo.CreaMetadataAsync(metadata);
      return await _repo.GetMetadataAsync(eventId) ?? metadata;
    }

    public static Task<List<LinkGoogleAdmin>> GetLinkAsync(int eventId) => _repo.GetLinkAsync(eventId);

    /// <summary>Imposta (o toglie, se vuoto) il link del tipo indicato. Elimina eventuali doppioni.</summary>
    public static async Task SalvaLinkAsync(int eventId, UrlTypes tipo, string? url, IReadOnlyCollection<LinkGoogleAdmin> esistenti)
    {
      Valida(AdminLogica.ValidaUrl(url));
      var pulito = url?.Trim() ?? string.Empty;
      var stessoTipo = esistenti.Where(l => l.EventId == eventId && l.UrlType == (int)tipo).OrderBy(l => l.Id).ToList();

      if (pulito.Length == 0)
      {
        foreach (var l in stessoTipo)
          await _repo.EliminaLinkAsync(l.Id);
        return;
      }

      if (stessoTipo.Count == 0)
      {
        await _repo.CreaLinkAsync(new LinkGoogleAdmin { EventId = eventId, UrlType = (int)tipo, Url = pulito });
        return;
      }

      if (stessoTipo[0].Url != pulito)
        await _repo.AggiornaLinkAsync(stessoTipo[0].Id, pulito);
      foreach (var doppione in stessoTipo.Skip(1))
        await _repo.EliminaLinkAsync(doppione.Id);
    }

    public static Task<List<SettimanaAdmin>> GetSettimaneAsync(int eventId) => _repo.GetSettimaneAsync(eventId);

    public static async Task SalvaSettimanaAsync(SettimanaAdmin settimana)
    {
      if (settimana.DataFine.Date < settimana.DataInizio.Date)
        throw new ArgumentException("La data di fine deve essere uguale o successiva a quella di inizio.");
      if (settimana.CostoIntero < 0 || settimana.CostoBase < 0)
        throw new ArgumentException("I costi non possono essere negativi.");
      settimana.Desc = string.IsNullOrWhiteSpace(settimana.Desc) ? null : settimana.Desc.Trim();
      await _repo.SalvaSettimanaAsync(settimana);
    }

    public static Task EliminaSettimanaAsync(int settimanaId) => _repo.EliminaSettimanaAsync(settimanaId);

    public static Task<List<TagliaAdmin>> GetTaglieAsync(int eventId) => _repo.GetTaglieAsync(eventId);

    public static async Task SalvaTagliaAsync(TagliaAdmin taglia)
    {
      if (string.IsNullOrWhiteSpace(taglia.Codice))
        throw new ArgumentException("Il codice della taglia è obbligatorio (es. M, L, 7-8 anni).");
      taglia.Codice = taglia.Codice.Trim();
      taglia.Descrizione = string.IsNullOrWhiteSpace(taglia.Descrizione) ? null : taglia.Descrizione.Trim();
      await _repo.SalvaTagliaAsync(taglia);
    }

    public static Task EliminaTagliaAsync(int tagliaId) => _repo.EliminaTagliaAsync(tagliaId);

    public static Task<List<TipoConsensoAdmin>> GetTipiConsensoAsync(int eventId) => _repo.GetTipiConsensoAsync(eventId);

    public static async Task SalvaTipoConsensoAsync(TipoConsensoAdmin tipo)
    {
      if (string.IsNullOrWhiteSpace(tipo.Descrizione))
        throw new ArgumentException("Il testo del consenso è obbligatorio.");
      tipo.Descrizione = tipo.Descrizione.Trim();
      await _repo.SalvaTipoConsensoAsync(tipo);
    }

    public static Task EliminaTipoConsensoAsync(int tipoId) => _repo.EliminaTipoConsensoAsync(tipoId);

    // ---------------------------------------------------------------- supporto

    private static void Valida(string? errore)
    {
      if (errore != null)
        throw new ArgumentException(errore);
    }

    private static string ValidaNomeUnivoco(string? nome, IEnumerable<string> altriNomi, string cosa)
    {
      Valida(AdminLogica.ValidaNome(nome));
      var pulito = nome!.Trim();
      if (altriNomi.Any(n => string.Equals(n?.Trim(), pulito, StringComparison.CurrentCultureIgnoreCase)))
        throw new ArgumentException($"Esiste già {cosa} con questo nome.");
      return pulito;
    }
  }
}
