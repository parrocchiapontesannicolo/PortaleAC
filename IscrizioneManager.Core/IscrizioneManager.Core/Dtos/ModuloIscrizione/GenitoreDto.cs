public class GenitoreDto
{
  public int? IdGenitore { get; set; }
  public string? Cognome { get; set; }
  public string? Nome { get; set; }
  public string? Telefono { get; set; }
  public int? Genere { get; set; }
  public bool Existing { get; set; }
  public bool NotExisting => !Existing;
  public string NomeCompleto => $"{Cognome} {Nome}".Trim();
}
