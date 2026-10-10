using IscrizioneManager.Core.Services;
using IscrizioniManager.Controllers;
using IscrizioniManager.Models;
using static Supabase.Postgrest.Constants;

namespace IscrizioneManager.Core.Repositories;

public class SupabaseIscrizioneCompletaRepository : IIscrizioneCompletaRepository
{
    public async Task<bool> CreateAsync(ModuloIscrizioneDto dto)
    {
        // Evento letto una sola volta: se l'utente cambia evento a metà salvataggio
        // le richieste successive restano sull'evento di partenza.
        var eventId = EventoCorrente();
        var payload = new
        {
            p_dto = new
            {
                nome = dto.Nome,
                cognome = dto.Cognome,
                data_nascita = dto.DataNascita?.ToString("yyyy-MM-dd"),
                genere = dto.Genere,
                luogo_nascita = dto.LuogoNascita,
                indirizzo_residenza = dto.IndirizzoResidenza,
                comune_residenza = dto.ComuneResidenza,
                anno_scolastico = dto.AnnoScolastico,
                note = dto.Note,
                da_iscrivere_al_noi = dto.DaIscrivereAlNoi,
                altra_parrocchia = !dto.DaIscrivereAlNoi ? dto.AltraParrocchia : false,
                formato_iscrizione = dto.FormatoIscrizioneSelezionato,
                modalita_pagamento = dto.ModalitaPagamentoSelezionata,
                sconto_fratelli = dto.ScontoFratelli,
                ricevuta = dto.Ricevuta,
                desc_ricevuta = dto.Ricevuta ? dto.DescRicevuta : null,
                esce_solo = dto.EsceSolo,
                caparra_pagata = dto.CaparraPagata,
                email_genitore = dto.EmailGenitore,
                priority = dto.Priority,
                settimane = dto.Settimane?.Where(x => x.IsSelected).Select(s => new {
                    id_settimana = Math.Abs(s.Id),
                    intero = s.CostoIntero != null
                }).ToArray(),
                genitori = dto.Genitori?.Select(g => new {
                    id_genitore = g.IdGenitore,
                    nome = g.Nome,
                    cognome = g.Cognome,
                    telefono = g.Telefono,
                    sesso = g.Genere
                }).ToArray(),
                consensi = dto.ConsensiDisponibili?.Select(c => new {
                    id_tipo_consenso = c.IdTipoConsenso,
                    valore = c.IsSelected
                }).ToArray(),
                Taglia = dto.Taglia,
                allergie_intolleranze = dto.AllergieIntolleranze,
                patologie_terapie = dto.PatologieTerapie,
                event_id = eventId
            }
        };

        var bambinoId = await ClientHolder.Client.Rpc<int>("create_iscrizione", payload);
        if (bambinoId == default)
            return false;

        if (dto.Pagato)
            await AllineaPagatoAsync(eventId, bambinoId, true);
        return true;
    }

    public async Task<bool> UpdateAsync(ModuloIscrizioneDto dto)
    {
        var eventId = EventoCorrente();
        var payload = new
        {
            p_dto = new
            {
                nome = dto.Nome,
                cognome = dto.Cognome,
                data_nascita = dto.DataNascita?.ToString("yyyy-MM-dd"),
                genere = dto.Genere,
                luogo_nascita = dto.LuogoNascita,
                indirizzo_residenza = dto.IndirizzoResidenza,
                comune_residenza = dto.ComuneResidenza,
                anno_scolastico = dto.AnnoScolastico,
                note = dto.Note,
                da_iscrivere_al_noi = dto.DaIscrivereAlNoi,
                altra_parrocchia = !dto.DaIscrivereAlNoi ? dto.AltraParrocchia : false,
                formato_iscrizione = dto.FormatoIscrizioneSelezionato,
                modalita_pagamento = dto.ModalitaPagamentoSelezionata,
                sconto_fratelli = dto.ScontoFratelli,
                ricevuta = dto.Ricevuta,
                desc_ricevuta = dto.Ricevuta ? dto.DescRicevuta : null,
                esce_solo = dto.EsceSolo,
                caparra_pagata = dto.CaparraPagata,
                email_genitore = dto.EmailGenitore,
                priority = dto.Priority,
                settimane = dto.Settimane?.Where(x => x.IsSelected).Select(s => new {
                    id_settimana = Math.Abs(s.Id),
                    intero = s.CostoIntero != null
                }).ToArray(),
                genitori = dto.Genitori?.Select(g => new {
                    id_genitore = g.IdGenitore,
                    nome = g.Nome,
                    cognome = g.Cognome,
                    telefono = g.Telefono,
                    sesso = g.Genere
                }).ToArray(),
                consensi = dto.ConsensiDisponibili?.Select(c => new {
                    id_tipo_consenso = c.IdTipoConsenso,
                    valore = c.IsSelected
                }).ToArray(),
                taglia = dto.Taglia,
                allergie_intolleranze = dto.AllergieIntolleranze,
                patologie_terapie = dto.PatologieTerapie,
                event_id = eventId
            },
            p_id_bambino = dto.IdBambino
        };

        var bambinoId = await ClientHolder.Client.Rpc<int>("update_iscrizione", payload);
        if (bambinoId == default)
            return false;

        // Solo se l'utente l'ha cambiato nel modulo: il valore letto all'apertura
        // annullerebbe un pagamento segnato nel frattempo dalla lista.
        if (dto.PagatoModificato)
            await AllineaPagatoAsync(eventId, dto.IdBambino!.Value, dto.Pagato);
        return true;
    }

    public async Task<bool> DeleteAsync(int idBambino)
    {
        var payload = new { p_id_bambino = idBambino };
        var deletedId = await ClientHolder.Client.Rpc<int>("delete_iscrizione", payload);
        return deletedId != default;
    }

    public async Task<ModuloIscrizioneDto> GetAsync(int idBambino)
    {
        var client = ClientHolder.Client;
        // Tutte le letture sullo stesso evento, anche se nel frattempo si cambia evento
        var eventId = EventoCorrente();

        var bambinoTask = client.BaseFrom<Bambino>().Select("*").Where(x => x.event_id == eventId).Where(x => x.Id == idBambino).Get();
        var iscrizioneTask = client.BaseFrom<Iscrizione>().Select("*").Where(x => x.event_id == eventId).Where(x => x.IdBambino == idBambino).Get();
        var relGenitoriTask = client.BaseFrom<GenitoreBambino>().Select("*").Where(x => x.event_id == eventId).Where(x => x.IdBambino == idBambino).Get();
        var tutteSettimaneTask = client.BaseFrom<Settimana>().Select("*").Where(x => x.event_id == eventId).Get();
        var tipiConsensoTask = client.BaseFrom<TipoConsenso>().Select("*").Where(x => x.event_id == eventId).Get();

        var bambino = (await bambinoTask).Models.FirstOrDefault()
            ?? throw new InvalidOperationException("Iscritto non trovato: potrebbe essere stato eliminato o appartenere a un altro evento.");
        var iscrizione = (await iscrizioneTask).Models.OrderByDescending(x => x.Id).FirstOrDefault()
            ?? throw new InvalidOperationException($"Il bambino {bambino.Cognome} {bambino.Nome} non ha un'iscrizione in questo evento.");

        var idIscrizione = iscrizione.Id;
        var settimaneSceltaTask = client.BaseFrom<IscrizioneSettimana>().Select("*").Where(x => x.event_id == eventId).Where(x => x.IdIscrizione == idIscrizione).Get();
        var tagliaTask = client.BaseFrom<IscrizioneTaglia>().Select("*").Where(x => x.event_id == eventId).Where(x => x.IdIscrizione == idIscrizione).Get();
        var schedaTask = client.BaseFrom<SchedaSanitaria>().Select("*").Where(x => x.event_id == eventId).Where(x => x.IdIscrizione == idIscrizione).Get();
        var consensiTask = client.BaseFrom<Consenso>().Select("*").Where(x => x.event_id == eventId).Where(x => x.IdIscrizione == idIscrizione).Get();

        // Genitori: una sola query per id. Niente filtro sull'evento: in passato si potevano
        // collegare genitori di altri eventi e filtrarli li farebbe perdere al salvataggio.
        // Se qualcuno non è leggibile il DTO lo segnala e il controller rifiuta il salvataggio.
        var idGenitori = (await relGenitoriTask).Models.Select(r => r.IdGenitore).Distinct().ToList();
        var genitori = new List<Genitore>();
        if (idGenitori.Count > 0)
        {
            genitori = (await client.BaseFrom<Genitore>()
                .Select("*")
                .Filter("id_genitore", Operator.In, idGenitori.Cast<object>().ToList())
                .Get()).Models;
        }

        var genitoriDto = idGenitori
            .Select(id => genitori.FirstOrDefault(g => g.Id == id))
            .Where(g => g != null)
            .Select(g => new GenitoreDto
            {
                IdGenitore = g!.Id,
                Nome = g.Nome,
                Cognome = g.Cognome,
                Telefono = g.Telefono,
                Genere = g.Genere,
                Existing = true
            })
            .ToList();

        var scheda = (await schedaTask).Models.FirstOrDefault();
        var idTaglia = (await tagliaTask).Models.FirstOrDefault()?.IdTaglia;

        // Tutti i tipi di consenso dell'evento, con il valore registrato (se c'è)
        var consensi = (await consensiTask).Models;
        var tipiConsenso = (await tipiConsensoTask).Models;
        var consensiDto = tipiConsenso
            .OrderBy(t => t.Id)
            .Select(t => new ConsensoDto
            {
                IdTipoConsenso = t.Id,
                Descrizione = t.Descrizione,
                IsSelected = consensi.Any(c => c.IdTipoConsenso == t.Id && c.Valore)
            })
            .Concat(consensi
                .Where(c => tipiConsenso.All(t => t.Id != c.IdTipoConsenso))
                .Select(c => new ConsensoDto { IdTipoConsenso = c.IdTipoConsenso, IsSelected = c.Valore }))
            .ToList();

        // Tutte le settimane dell'evento (non solo quelle scelte), così in modifica se ne possono aggiungere
        var settimane = SettimaneModulo.Espandi((await tutteSettimaneTask).Models, (await settimaneSceltaTask).Models);

        return new ModuloIscrizioneDto
        {
            IdBambino = bambino.Id,
            Nome = bambino.Nome,
            Cognome = bambino.Cognome,
            DataNascita = IscrittiListaController.ParseData(bambino.DataNascita),
            Genere = bambino.Genere,
            LuogoNascita = bambino.LuogoNascita,
            IndirizzoResidenza = bambino.IndirizzoResidenza,
            ComuneResidenza = bambino.ComuneResidenza,
            IdIscrizione = iscrizione.Id,
            AnnoScolastico = iscrizione.Anno,
            Note = iscrizione.Note,
            Genitori = genitoriDto,
            GenitoriNonLeggibili = idGenitori.Count - genitoriDto.Count,
            Settimane = settimane,
            Taglia = idTaglia,
            AllergieIntolleranze = scheda?.AllergieIntolleranze,
            PatologieTerapie = scheda?.PatologieTerapie,
            ConsensiDisponibili = consensiDto,
            DaIscrivereAlNoi = iscrizione.DaIscrivereAlNoi,
            AltraParrocchia = iscrizione.AltraParrocchia,
            ModalitaPagamentoSelezionata = iscrizione.ModalitaPagamento,
            FormatoIscrizioneSelezionato = iscrizione.FormatoIscrizione,
            Pagato = iscrizione.Pagato,
            PagatoIniziale = iscrizione.Pagato,
            ScontoFratelli = iscrizione.ScontoFratelli,
            Ricevuta = iscrizione.Ricevuta,
            DescRicevuta = iscrizione.DescRicevuta,
            EsceSolo = iscrizione.EsceSolo,
            CaparraPagata = iscrizione.CaparraPagata,
            EmailGenitore = iscrizione.EmailGenitore,
            Priority = iscrizione.Priority ?? false
        };
    }

    private static int EventoCorrente()
    {
        var eventId = ClientHolder.Client._eventId;
        if (eventId <= 0)
            throw new InvalidOperationException("Nessun evento selezionato.");
        return eventId;
    }

    /// <summary>
    /// Il flag "pagato" non passa da create/update_iscrizione: lo allinea con
    /// set_pagato solo se il valore sul database è diverso.
    /// </summary>
    private static async Task AllineaPagatoAsync(int eventId, int idBambino, bool pagato)
    {
        try
        {
            var iscrizione = (await ClientHolder.Client
                .BaseFrom<Iscrizione>()
                .Select("*")
                .Where(x => x.event_id == eventId).Where(x => x.IdBambino == idBambino)
                .Get()).Models.OrderByDescending(x => x.Id).FirstOrDefault();

            if (iscrizione == null)
                throw new InvalidOperationException("iscrizione non trovata dopo il salvataggio.");
            if (iscrizione.Pagato != pagato)
                await IscrittiListaController.AggiornaPagatoAsync(iscrizione.Id, pagato);
        }
        catch (Exception ex)
        {
            throw new PagatoNonAggiornatoException(ex);
        }
    }
}
