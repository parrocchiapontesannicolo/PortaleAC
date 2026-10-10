namespace IscrizioneManager.Core.Security
{
  /// <summary>
  /// Chiavi dei permessi per evento. Devono restare allineate con la funzione
  /// SQL public.tutti_permessi() in supabase/migrations.
  /// </summary>
  public static class Permessi
  {
    public const string IscrittiView = "iscritti.view";
    public const string IscrittiEdit = "iscritti.edit";
    public const string IscrittiDelete = "iscritti.delete";
    public const string IscrittiPagamenti = "iscritti.pagamenti";
    public const string IscrittiSanitario = "iscritti.sanitario";
    public const string SquadreView = "squadre.view";
    public const string SquadreEdit = "squadre.edit";
    public const string GoogleSheet = "google.sheet";
    public const string GoogleDrive = "google.drive";

    public record Info(string Chiave, string Nome, string Descrizione, string Gruppo);

    public static readonly IReadOnlyList<Info> Tutti = new List<Info>
    {
      new(IscrittiView, "Vedere gli iscritti", "Lista iscritti e dettaglio in sola lettura", "Iscritti"),
      new(IscrittiEdit, "Inserire e modificare iscrizioni", "Nuova iscrizione e modifica (include la scheda sanitaria)", "Iscritti"),
      new(IscrittiDelete, "Eliminare iscrizioni", "Cancellazione definitiva di un iscritto", "Iscritti"),
      new(IscrittiPagamenti, "Gestire i pagamenti", "Vedere e segnare pagato / caparra", "Iscritti"),
      new(IscrittiSanitario, "Vedere la scheda sanitaria", "Allergie, intolleranze, patologie", "Iscritti"),
      new(SquadreView, "Vedere le squadre", "Composizione delle squadre", "Squadre"),
      new(SquadreEdit, "Gestire le squadre", "Spostare bambini, ridistribuire, creare squadre", "Squadre"),
      new(GoogleSheet, "Fogli Google", "Accesso al foglio Google dell'evento", "Documenti"),
      new(GoogleDrive, "Cartella Google Drive", "Accesso alla cartella Drive dell'evento", "Documenti"),
    };
  }
}
