namespace IscrizioniManager.Items
{
  public class SettimanaItem
  {
    public int Id { get; set; }

    public bool IsIntero { get; set; }

    public bool IsSelected { get; set; }

    public string Descrizione { get; set; } = string.Empty;

    public override string ToString() => Descrizione; // per il Picker
  }
}
