using IscrizioneManager.Core.Services;
using IscrizioniManager.Models;

namespace IscrizioneManager.Core.Repositories;

/// <summary>
/// Legge direttamente bambino e iscrizione (non la vista v_iscrizione_completa,
/// che unisce tabelle sensibili e con la RLS può perdere righe).
/// </summary>
public class SupabaseIscrittiListaRepository : IIscrittiListaRepository
{
    public async Task<List<Bambino>> GetBambiniAsync()
    {
        return (await ClientHolder.Client
            .GetAll<Bambino>()
            .Select("*")
            .Get()).Models;
    }

    public async Task<List<Iscrizione>> GetIscrizioniAsync()
    {
        return (await ClientHolder.Client
            .GetAll<Iscrizione>()
            .Select("*")
            .Get()).Models;
    }

    public async Task SetPagatoAsync(int idIscrizione, bool pagato)
    {
        // RPC dedicata: basta il permesso iscritti.pagamenti (l'update diretto richiede iscritti.edit)
        await ClientHolder.Client.Rpc("set_pagato", new Dictionary<string, object>
        {
            ["p_id_iscrizione"] = idIscrizione,
            ["p_pagato"] = pagato,
        });
    }
}
