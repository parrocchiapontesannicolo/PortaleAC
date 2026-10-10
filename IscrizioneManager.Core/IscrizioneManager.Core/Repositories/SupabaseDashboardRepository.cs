using IscrizioneManager.Core.Models;
using IscrizioneManager.Core.Services;
using IscrizioniManager.Models;

namespace IscrizioneManager.Core.Repositories;

public class SupabaseDashboardRepository : IDashboardRepository
{
    public async Task<List<Iscrizione>> GetIscrizioniAsync()
    {
        var result = await ClientHolder.Client
            .GetAll<Iscrizione>()
            .Select("id_iscrizione,id_bambino,anno,pagato,caparra_pagata,event_id")
            .Get();
        return result.Models;
    }

    public async Task<List<AnnoScolastico>> GetAnniScolasticiAsync()
    {
        var result = await ClientHolder.Client
            .BaseFrom<AnnoScolastico>()
            .Select("id,desc,grado_scuola")
            .Get();
        return result.Models;
    }

    public async Task<List<int>> GetIdBambiniAsync()
    {
        var result = await ClientHolder.Client
            .GetAll<Bambino>()
            .Select("id_bambino,event_id")
            .Get();
        return result.Models.Select(b => b.Id).ToList();
    }

    public async Task<List<Squadra>> GetSquadreAsync()
    {
        var result = await ClientHolder.Client
            .GetAll<Squadra>()
            .Select("id_squadra,nome,color,event_id")
            .Get();
        return result.Models;
    }

    public async Task<List<SquadraBambino>> GetSquadraBambiniAsync()
    {
        var result = await ClientHolder.Client
            .GetAll<SquadraBambino>()
            .Select("id_squadra,id_bambino,event_id")
            .Get();
        return result.Models;
    }
}
