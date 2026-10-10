using IscrizioniManager.Data;
using Supabase.Postgrest.Attributes;
using System.ComponentModel.DataAnnotations.Schema;

namespace IscrizioniManager.Models;

[Supabase.Postgrest.Attributes.Table("bambino")]
public class Bambino : EventModel
{
  [PrimaryKey("id_bambino")]
  public int Id { get; set; }

  [Supabase.Postgrest.Attributes.Column("nome")]
  public string Nome { get; set; } = string.Empty;

  [Supabase.Postgrest.Attributes.Column("cognome")]
  public string Cognome { get; set; } = string.Empty;

  [Supabase.Postgrest.Attributes.Column("data_nascita")]
  public string DataNascita { get; set; } = string.Empty;
  [Supabase.Postgrest.Attributes.Column("genere")]
  public int? Genere { get; set; }

  [Supabase.Postgrest.Attributes.Column("luogo_nascita")]
  public string? LuogoNascita { get; set; }

  [Supabase.Postgrest.Attributes.Column("indirizzo_residenza")]
  public string? IndirizzoResidenza { get; set; }
  [Supabase.Postgrest.Attributes.Column("comune_residenza")]
  public string? ComuneResidenza { get; set; }
  [NotMapped]
  public int? Anno { get; set; }
  [NotMapped]
  public string? AnnoDesc { get; set; }
}