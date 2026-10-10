using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace IscrizioneManager.Core.Models
{
  [Table("anno_scolastico")]
  public class AnnoScolastico : BaseModel
  {
    [PrimaryKey("id")]
    public int? Id { get; set; }

    [Column("desc")]
    public string Desc { get; set; } = string.Empty;

    [Column("grado_scuola")]
    public int GradoScuola { get; set; }

    public AnnoScolastico() { }

    public AnnoScolastico(int? id, string desc)
    {
      Id = id;
      Desc = desc;
    }
  }
}
