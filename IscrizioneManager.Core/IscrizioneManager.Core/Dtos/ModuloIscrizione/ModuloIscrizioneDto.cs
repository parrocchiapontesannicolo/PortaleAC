public class ModuloIscrizioneDto
{
  public int? IdBambino { get; set; }
  public int? IdIscrizione { get; set; }
  public string? Cognome { get; set; }
  public string? Nome { get; set; }
  public DateTime? DataNascita { get; set; }
  public int? Genere { get; set; }
  public string GenereDesc => Genere == 1 ? "M" : Genere == 2 ? "F" : "";
  public string? LuogoNascita { get; set; }
  public string? IndirizzoResidenza { get; set; }
  public string? ComuneResidenza { get; set; }

  // Genitori
  public List<GenitoreDto> Genitori { get; set; } = new();

  // Iscrizione
  public int? AnnoScolastico { get; set; }
  public string? Note { get; set; }

  // Settimane e taglia
  public List<Settimana> Settimane { get; set; } = new();
  public int? Taglia { get; set; }

  // Scheda sanitaria
  public string? AllergieIntolleranze { get; set; }
  public string? PatologieTerapie { get; set; }

  // Consensi
  public List<ConsensoDto> ConsensiDisponibili { get; set; } = new();

  // Modalità pagamento
  public int? ModalitaPagamentoSelezionata { get; set; }
  public int? FormatoIscrizioneSelezionato { get; set; }

  /// <summary>Salvato a parte con la RPC set_pagato (non fa parte di create/update_iscrizione).</summary>
  public bool Pagato { get; set; }

  /// <summary>
  /// Valore di "pagato" letto dal database: al salvataggio set_pagato parte solo
  /// se l'utente l'ha cambiato, così non si annulla un pagamento segnato da altri.
  /// </summary>
  public bool PagatoIniziale { get; set; }

  public bool PagatoModificato => Pagato != PagatoIniziale;

  /// <summary>
  /// Genitori collegati che non è stato possibile leggere (permessi): salvare
  /// cancellerebbe i collegamenti, quindi il salvataggio viene rifiutato.
  /// </summary>
  public int GenitoriNonLeggibili { get; set; }

  // Altri flag
  public bool DaIscrivereAlNoi { get; set; }
  public bool AltraParrocchia { get; set; }
  public bool ScontoFratelli { get; set; }
  public bool Ricevuta { get; set; }
  public string? DescRicevuta { get; set; }
  public string? EmailGenitore { get; set; }
  public bool EsceSolo { get; set; }
  public bool CaparraPagata { get; set; }
  public bool Priority { get; set; }

  public string NomeCompleto => $"{Cognome} {Nome}".Trim();

  /// <summary>Somma indicativa delle settimane scelte (senza sconti).</summary>
  public decimal TotaleSettimane => SettimaneModulo.Totale(Settimane);

  public ModuloIscrizioneDto()
  {
    Settimane = new List<Settimana>();
    ConsensiDisponibili = new List<ConsensoDto>();
    Genitori = new List<GenitoreDto>();
  }
}
