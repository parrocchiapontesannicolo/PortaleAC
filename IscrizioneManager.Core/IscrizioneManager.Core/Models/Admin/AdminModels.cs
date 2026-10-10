using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace IscrizioneManager.Core.Models.Admin
{
  /// <summary>Account condiviso (tabella app_account). L'utente auth lo crea la Edge Function admin-accounts.</summary>
  [Table("app_account")]
  public class AppAccount : BaseModel
  {
    [PrimaryKey("id", true)]
    public Guid Id { get; set; }

    [Column("nome")]
    public string Nome { get; set; } = string.Empty;

    [Column("is_superadmin")]
    public bool IsSuperadmin { get; set; }

    [Column("attivo")]
    public bool Attivo { get; set; } = true;

    [Column("ordine")]
    public int Ordine { get; set; }

    [Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTime? CreatedAt { get; set; }
  }

  /// <summary>Insieme di permessi con un nome (tabella ruolo).</summary>
  [Table("ruolo")]
  public class Ruolo : BaseModel
  {
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("nome")]
    public string Nome { get; set; } = string.Empty;

    [Column("descrizione")]
    public string? Descrizione { get; set; }

    [Column("permessi")]
    public List<string> Permessi { get; set; } = new();

    public Ruolo Copia() => new()
    {
      Id = Id,
      Nome = Nome,
      Descrizione = Descrizione,
      Permessi = new List<string>(Permessi),
    };
  }

  /// <summary>Accesso di un account a un evento con un ruolo (tabella account_evento).</summary>
  [Table("account_evento")]
  public class AccountEvento : BaseModel
  {
    [PrimaryKey("account_id", true)]
    public Guid AccountId { get; set; }

    [PrimaryKey("event_id", true)]
    public int EventId { get; set; }

    [Column("ruolo_id")]
    public int RuoloId { get; set; }
  }

  /// <summary>Link Google dell'evento (tabella google_sheet): 1 = foglio, 2 = cartella Drive.</summary>
  [Table("google_sheet")]
  public class LinkGoogleAdmin : BaseModel
  {
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("event_id")]
    public int EventId { get; set; }

    [Column("url")]
    public string Url { get; set; } = string.Empty;

    [Column("url_type")]
    public int UrlType { get; set; }
  }

  [Table("settimana")]
  public class SettimanaAdmin : BaseModel
  {
    [PrimaryKey("id_settimana")]
    public int Id { get; set; }

    [Column("event_id")]
    public int EventId { get; set; }

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

    public SettimanaAdmin Copia() => new()
    {
      Id = Id,
      EventId = EventId,
      DataInizio = DataInizio,
      DataFine = DataFine,
      CostoIntero = CostoIntero,
      CostoBase = CostoBase,
      Desc = Desc,
    };
  }

  [Table("taglia")]
  public class TagliaAdmin : BaseModel
  {
    [PrimaryKey("id_taglia")]
    public int Id { get; set; }

    [Column("event_id")]
    public int EventId { get; set; }

    [Column("codice")]
    public string Codice { get; set; } = string.Empty;

    [Column("descrizione")]
    public string? Descrizione { get; set; }
  }

  [Table("tipo_consenso")]
  public class TipoConsensoAdmin : BaseModel
  {
    [PrimaryKey("id_tipo_consenso")]
    public int Id { get; set; }

    [Column("event_id")]
    public int EventId { get; set; }

    [Column("descrizione")]
    public string Descrizione { get; set; } = string.Empty;
  }

  /// <summary>Risposta della Edge Function alla creazione di un account.</summary>
  public class NuovoAccountRisultato
  {
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;
  }
}
