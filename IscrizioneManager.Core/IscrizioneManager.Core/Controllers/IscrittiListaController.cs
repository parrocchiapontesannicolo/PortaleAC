using System.Globalization;
using IscrizioneManager.Core.Dtos;
using IscrizioneManager.Core.Models;
using IscrizioneManager.Core.Repositories;
using IscrizioniManager.Models;

namespace IscrizioniManager.Controllers
{
    public class IscrittiListaController
    {
        public IscrittiListaController() { }

        internal static IIscrittiListaRepository _repo = new SupabaseIscrittiListaRepository();

        /// <summary>Configurazione evento, anni scolastici e iscritti in un colpo solo.</summary>
        public static async Task<IscrittiListaDati> CaricaListaAsync()
        {
            var metadataTask = IscrizioneCompletaController.GetEventMetadataAsync();
            var bambiniTask = _repo.GetBambiniAsync();
            var iscrizioniTask = _repo.GetIscrizioniAsync();

            var metadata = await metadataTask;
            var anni = await IscrizioneCompletaController.GetAnniScolasticiAsync(
                IscrizioneCompletaController.GradiAmmessi(metadata));

            return new IscrittiListaDati
            {
                Metadata = metadata,
                AnniScolastici = anni,
                Iscritti = Componi(await bambiniTask, await iscrizioniTask, anni),
            };
        }

        public static async Task<List<IscrittoListaItem>> LoadIscrittiAsync()
            => (await CaricaListaAsync()).Iscritti;

        public static async Task AggiornaPagatoAsync(int idIscrizione, bool pagato)
        {
            if (idIscrizione <= 0) throw new ArgumentException("Iscrizione non valida", nameof(idIscrizione));
            await _repo.SetPagatoAsync(idIscrizione, pagato);
        }

        /// <summary>Una riga per bambino, con i dati della sua iscrizione (se c'è).</summary>
        public static List<IscrittoListaItem> Componi(IEnumerable<Bambino> bambini, IEnumerable<Iscrizione> iscrizioni,
            IEnumerable<AnnoScolastico> anniScolastici)
        {
            var iscrizioniPerBambino = iscrizioni
                .GroupBy(i => i.IdBambino)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.Id).First());
            var anni = anniScolastici
                .Where(a => a.Id != null)
                .GroupBy(a => a.Id!.Value)
                .ToDictionary(g => g.Key, g => g.First().Desc ?? string.Empty);

            return bambini
                .GroupBy(b => b.Id)
                .Select(g => g.First())
                .Select(b =>
                {
                    iscrizioniPerBambino.TryGetValue(b.Id, out var i);
                    var anno = i?.Anno;
                    return new IscrittoListaItem
                    {
                        IdBambino = b.Id,
                        IdIscrizione = i?.Id,
                        Cognome = b.Cognome ?? string.Empty,
                        Nome = b.Nome ?? string.Empty,
                        DataNascita = ParseData(b.DataNascita),
                        Genere = b.Genere,
                        Anno = anno,
                        AnnoDesc = anno != null && anni.TryGetValue(anno.Value, out var desc) ? desc : string.Empty,
                        Pagato = i?.Pagato ?? false,
                        CaparraPagata = i?.CaparraPagata ?? false,
                        Priority = i?.Priority ?? false,
                        Note = i?.Note,
                    };
                })
                .OrderBy(x => x.Cognome, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.Nome, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static List<IscrittoListaItem> Filtra(IEnumerable<IscrittoListaItem> iscritti, FiltroIscritti filtro)
        {
            var query = iscritti;

            var parole = (filtro.Testo ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parole.Length > 0)
                query = query.Where(x => parole.All(p => Corrisponde(x, p)));

            if (filtro.Anno == FiltroIscritti.AnnoNonIndicato)
                query = query.Where(x => x.Anno == null);
            else if (filtro.Anno != null)
                query = query.Where(x => x.Anno == filtro.Anno);

            if (filtro.Pagato != null)
                query = query.Where(x => x.Pagato == filtro.Pagato);
            if (filtro.Caparra != null)
                query = query.Where(x => x.CaparraPagata == filtro.Caparra);
            if (filtro.Priority != null)
                query = query.Where(x => x.Priority == filtro.Priority);

            var perNome = StringComparer.CurrentCultureIgnoreCase;
            query = filtro.Ordinamento switch
            {
                OrdinamentoIscritti.Recenti => query.OrderByDescending(x => x.IdIscrizione ?? 0).ThenByDescending(x => x.IdBambino),
                OrdinamentoIscritti.Anno => query.OrderBy(x => x.Anno == null).ThenBy(x => x.Anno)
                    .ThenBy(x => x.Cognome, perNome).ThenBy(x => x.Nome, perNome),
                OrdinamentoIscritti.DataNascita => query.OrderBy(x => x.DataNascita == null).ThenBy(x => x.DataNascita)
                    .ThenBy(x => x.Cognome, perNome),
                _ => query.OrderBy(x => x.Cognome, perNome).ThenBy(x => x.Nome, perNome),
            };

            return query.ToList();
        }

        public static RiepilogoIscritti Riepiloga(IReadOnlyCollection<IscrittoListaItem> iscritti, IEnumerable<AnnoScolastico> anniScolastici)
        {
            var ordineAnni = anniScolastici
                .Where(a => a.Id != null)
                .Select((a, indice) => (a.Id!.Value, indice))
                .GroupBy(x => x.Value)
                .ToDictionary(g => g.Key, g => g.First().indice);

            var perAnno = iscritti
                .GroupBy(x => x.Anno)
                .Select(g => new ConteggioAnno(
                    g.Key,
                    g.Key == null ? "Non indicato" : (g.Select(x => x.AnnoDesc).FirstOrDefault(d => d != "") ?? $"Anno {g.Key}"),
                    g.Count()))
                .OrderBy(c => c.Anno == null)
                .ThenBy(c => c.Anno != null && ordineAnni.TryGetValue(c.Anno.Value, out var i) ? i : int.MaxValue)
                .ThenBy(c => c.Anno)
                .ToList();

            return new RiepilogoIscritti
            {
                Totale = iscritti.Count,
                Pagati = iscritti.Count(x => x.Pagato),
                DaPagare = iscritti.Count(x => !x.Pagato),
                CaparraPagata = iscritti.Count(x => x.CaparraPagata),
                Priority = iscritti.Count(x => x.Priority),
                PerAnno = perAnno,
            };
        }

        private static bool Corrisponde(IscrittoListaItem x, string parola) =>
            Contiene(x.Cognome, parola) ||
            Contiene(x.Nome, parola) ||
            Contiene(x.AnnoDesc, parola) ||
            Contiene(x.DataNascitaDesc, parola) ||
            Contiene(x.Note, parola);

        /// <summary>Confronto senza maiuscole e accenti ("nicolo" trova "Nicolò").</summary>
        internal static bool Contiene(string? testo, string parola)
        {
            if (string.IsNullOrEmpty(testo))
                return false;
            try
            {
                return CultureInfo.InvariantCulture.CompareInfo.IndexOf(testo, parola,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
            }
            catch (PlatformNotSupportedException)
            {
                return testo.Contains(parola, StringComparison.OrdinalIgnoreCase);
            }
        }

        internal static DateTime? ParseData(string? data) =>
            DateTime.TryParse(data, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.Date : null;
    }
}
