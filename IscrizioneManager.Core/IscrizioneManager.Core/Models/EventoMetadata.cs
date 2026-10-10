using IscrizioniManager.Data;
using Supabase.Postgrest.Attributes;

namespace IscrizioneManager.Core.Models
{
  [Table("evento_metadata")]
  public class EventoMetadata : EventModel
  {
    [PrimaryKey("id")]
    public int Id { get; set; }
    [Column("view_genitori")]
    public bool View_Genitori { get; set; }
    [Column("view_mail_genitore")]
    public bool View_MailGenitore { get; set; }
    [Column("view_anno_scolastico")]
    public bool View_AnnoScolastico { get; set; }
    [Column("view_settimane")]
    public bool View_Settimane { get; set; }
    [Column("view_scheda_sanitaria")]
    public bool View_SchedaSanitaria { get; set; }
    [Column("view_consensi")]
    public bool View_Consensi { get; set; }
    [Column("view_formato_iscr_pagamento")]
    public bool View_FormatoIscrPagamento { get; set; }
    [Column("view_iscrizione_noi")]
    public bool View_IscrizioneNoi { get; set; }
    [Column("view_sconto_fratelli")]
    public bool View_ScontoFratelli { get; set; }
    [Column("view_taglia")]
    public bool View_Taglia { get; set; }
    [Column("view_uscita_autonoma")]
    public bool View_UscitaAutonoma { get; set; }

    [Column("gradi_scuola_allowed")]
    public string? GradiScuolaAllowed { get; set; }
    [Column("view_caparra")]
    public bool View_Caparra { get; set; }
    [Column("view_priority")]
    public bool View_Priority { get; set; }
  }
}
