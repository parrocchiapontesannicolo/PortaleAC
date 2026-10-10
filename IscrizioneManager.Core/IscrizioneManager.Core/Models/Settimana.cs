using IscrizioniManager.Data;
using Supabase.Postgrest.Attributes;
using System.ComponentModel.DataAnnotations.Schema;
using Column = Supabase.Postgrest.Attributes.ColumnAttribute;

[Supabase.Postgrest.Attributes.Table("settimana")]
public class Settimana : EventModel
{
  [PrimaryKey("id_settimana")]
  public int Id { get; set; }

  [Column("data_inizio")]
  public DateTime DataInizio { get; set; }

  [Column("data_fine")]
  public DateTime DataFine { get; set; }

  [Column("costo_intero")]
  public decimal? CostoIntero { get; set; }

  [Column("costo_base")]
  public decimal? CostoBase { get; set; }

  [Column("desc")]
  public string? Desc { get; set; }

  [NotMapped]
  public bool IsSelected { get; set; }

  /// <summary>Descrizione della settimana senza "(Con/Senza pranzo)", per raggruppare le varianti.</summary>
  [NotMapped]
  [Newtonsoft.Json.JsonIgnore]
  public string? DescBase { get; set; }
}