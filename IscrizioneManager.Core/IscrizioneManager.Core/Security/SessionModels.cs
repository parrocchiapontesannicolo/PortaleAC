using Newtonsoft.Json;

namespace IscrizioneManager.Core.Security
{
  /// <summary>Account mostrato nella tendina di login (RPC login_accounts).</summary>
  public class LoginAccount
  {
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;
  }

  /// <summary>Account con cui si è entrati (RPC my_account).</summary>
  public class AccountCorrente
  {
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonProperty("is_superadmin")]
    public bool IsSuperadmin { get; set; }
  }

  /// <summary>Evento accessibile all'account corrente, con i permessi (RPC my_events).</summary>
  public class MioEvento
  {
    [JsonProperty("event_id")]
    public int EventId { get; set; }

    [JsonProperty("nome")]
    public string Nome { get; set; } = string.Empty;

    [JsonProperty("isactive")]
    public int IsActive { get; set; }

    [JsonProperty("ruolo")]
    public string? Ruolo { get; set; }

    [JsonProperty("permessi")]
    public List<string> Permessi { get; set; } = new();

    [JsonIgnore]
    public bool Attivo => IsActive == 1;

    public bool Can(string permesso) => Permessi.Contains(permesso);
  }
}
