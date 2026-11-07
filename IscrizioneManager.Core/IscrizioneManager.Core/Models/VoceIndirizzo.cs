using Newtonsoft.Json;

namespace IscrizioneManager.Core.Models;

/// <summary>Riga della RPC elenco_indirizzi: un comune con una sua via (senza via se non ne ha).</summary>
public class VoceIndirizzo
{
  [JsonProperty("id_comune")]
  public int IdComune { get; set; }

  [JsonProperty("comune")]
  public string? Comune { get; set; }

  [JsonProperty("id_via")]
  public int? IdVia { get; set; }

  [JsonProperty("via")]
  public string? Via { get; set; }
}
