namespace IscrizioneManager.Web.Shared
{
  /// <summary>
  /// Rotte dell'app. Sempre SENZA "/" iniziale: l'app è pubblicata sotto
  /// /PortaleAC/ su GitHub Pages e i percorsi sono relativi al &lt;base href&gt;.
  /// </summary>
  public static class Rotte
  {
    public const string Login = "login";
    public const string Eventi = "eventi";
    public const string Home = "home";
    public const string Iscritti = "iscritti";
    public const string NuovaIscrizione = "iscritti/nuovo";
    public const string Squadre = "squadre";
    public const string FoglioGoogle = "documenti/foglio";
    public const string CartellaDrive = "documenti/drive";
    public const string AdminAccount = "admin/account";
    public const string AdminRuoli = "admin/ruoli";
    public const string AdminEventi = "admin/eventi";

    public static string DettaglioIscritto(int idBambino) => $"{Iscritti}/{idBambino}";
    public static string AdminEvento(int eventId) => $"{AdminEventi}/{eventId}";
  }
}
