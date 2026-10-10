namespace IscrizioneManager.Core.Dtos
{
  public enum OrdinamentoIscritti
  {
    Cognome,
    Recenti,
    Anno,
    DataNascita,
  }

  /// <summary>Filtri della lista iscritti (null = nessun filtro su quel campo).</summary>
  public class FiltroIscritti
  {
    /// <summary>Valore di <see cref="Anno"/> per gli iscritti senza anno scolastico.</summary>
    public const int AnnoNonIndicato = -1;

    public string? Testo { get; set; }
    public int? Anno { get; set; }
    public bool? Pagato { get; set; }
    public bool? Caparra { get; set; }
    public bool? Priority { get; set; }
    public OrdinamentoIscritti Ordinamento { get; set; } = OrdinamentoIscritti.Cognome;

    public int FiltriAttivi =>
      (string.IsNullOrWhiteSpace(Testo) ? 0 : 1) +
      (Anno.HasValue ? 1 : 0) +
      (Pagato.HasValue ? 1 : 0) +
      (Caparra.HasValue ? 1 : 0) +
      (Priority.HasValue ? 1 : 0);

    public void Azzera()
    {
      Testo = null;
      Anno = null;
      Pagato = null;
      Caparra = null;
      Priority = null;
    }

    public FiltroIscritti Clona() => (FiltroIscritti)MemberwiseClone();
  }

  public record ConteggioAnno(int? Anno, string Desc, int Conteggio);

  /// <summary>Totali mostrati in cima alla lista iscritti.</summary>
  public class RiepilogoIscritti
  {
    public int Totale { get; init; }
    public int Pagati { get; init; }
    public int DaPagare { get; init; }
    public int CaparraPagata { get; init; }
    public int Priority { get; init; }
    public List<ConteggioAnno> PerAnno { get; init; } = new();
  }

  /// <summary>Dati caricati dalla pagina lista iscritti.</summary>
  public class IscrittiListaDati
  {
    public IscrizioneManager.Core.Models.EventoMetadata Metadata { get; init; } = new();
    public List<IscrizioneManager.Core.Models.AnnoScolastico> AnniScolastici { get; init; } = new();
    public List<IscrittoListaItem> Iscritti { get; init; } = new();
  }
}
