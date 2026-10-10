namespace IscrizioniManager.Items
{
  public class TagliaItem
  {
    public int Id { get; set; }

    public string Descrizione { get; set; } = string.Empty;

    public override string ToString() => Descrizione; // per il Picker
  }
}
