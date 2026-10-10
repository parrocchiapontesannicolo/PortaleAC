using IscrizioneManager.Core.Logic;
using IscrizioneManager.Core.Models;
using IscrizioneManager.Core.Repositories;

namespace IscrizioneManager.Core.Controllers;

public class DashboardController
{
    internal static IDashboardRepository _repo = new SupabaseDashboardRepository();

    /// <summary>
    /// Carica solo le sezioni consentite: gli iscritti (totali, pagamenti, anni)
    /// e/o le squadre. Le letture partono in parallelo.
    /// </summary>
    public static async Task<DashboardStats> CaricaAsync(bool iscritti, bool squadre)
    {
        if (!iscritti && !squadre)
            return DashboardStats.Vuote;

        var iscrizioniTask = iscritti ? _repo.GetIscrizioniAsync() : Task.FromResult<List<Iscrizione>>(null!);
        var anniTask = iscritti ? _repo.GetAnniScolasticiAsync() : Task.FromResult<List<AnnoScolastico>>(null!);
        var bambiniTask = squadre ? _repo.GetIdBambiniAsync() : Task.FromResult<List<int>>(null!);
        var squadreTask = squadre ? _repo.GetSquadreAsync() : Task.FromResult<List<Squadra>>(null!);
        var assegnazioniTask = squadre ? _repo.GetSquadraBambiniAsync() : Task.FromResult<List<SquadraBambino>>(null!);

        await Task.WhenAll(iscrizioniTask, anniTask, bambiniTask, squadreTask, assegnazioniTask);

        return DashboardStats.Calcola(
            iscritti ? iscrizioniTask.Result : null,
            iscritti ? anniTask.Result : null,
            squadre ? bambiniTask.Result : null,
            squadre ? squadreTask.Result : null,
            squadre ? assegnazioniTask.Result : null);
    }
}
