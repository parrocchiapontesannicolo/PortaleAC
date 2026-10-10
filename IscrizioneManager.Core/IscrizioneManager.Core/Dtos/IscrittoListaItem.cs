namespace IscrizioneManager.Core.Dtos
{
  /// <summary>Riga della lista iscritti: bambino + dati principali dell'iscrizione.</summary>
  public class IscrittoListaItem
  {
    public int IdBambino { get; set; }

    /// <summary>Null se il bambino non ha (ancora) un'iscrizione nell'evento.</summary>
    public int? IdIscrizione { get; set; }

    public string Cognome { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public DateTime? DataNascita { get; set; }
    public int? Genere { get; set; }
    public int? Anno { get; set; }
    public string AnnoDesc { get; set; } = string.Empty;
    public bool Pagato { get; set; }
    public bool CaparraPagata { get; set; }
    public bool Priority { get; set; }
    public string? Note { get; set; }

    public string NomeCompleto => $"{Cognome} {Nome}".Trim();
    public string DataNascitaDesc => DataNascita?.ToString("dd/MM/yyyy") ?? string.Empty;
  }
}
