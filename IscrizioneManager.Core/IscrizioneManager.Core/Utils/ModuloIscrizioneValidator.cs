using System.Text.RegularExpressions;

namespace IscrizioneManager.Core.Utils
{
  /// <summary>
  /// Controlli del modulo iscrizione. È il riferimento per il salvataggio: la
  /// validazione inline della pagina è solo un aiuto visivo.
  /// </summary>
  public class ModuloIscrizioneValidator
  {
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public static string Validate(ModuloIscrizioneDto modulo)
    {
      var errors = "";
      if (string.IsNullOrWhiteSpace(modulo.Cognome))
        errors += "Il campo 'Cognome' è obbligatorio.\n";
      if (string.IsNullOrWhiteSpace(modulo.Nome))
        errors += "Il campo 'Nome' è obbligatorio.\n";
      if (!modulo.DataNascita.HasValue)
        errors += "Il campo 'Data di Nascita' è obbligatorio.\n";
      else if (modulo.DataNascita.Value.Date > DateTime.Today)
        errors += "La 'Data di Nascita' non può essere nel futuro.\n";
      if (!modulo.Genere.HasValue || (modulo.Genere != 1 && modulo.Genere != 2))
        errors += "Il campo 'Genere' è obbligatorio\n";

      if (modulo.Genitori != null)
      {
        foreach (var genitore in modulo.Genitori)
        {
          if (string.IsNullOrWhiteSpace(genitore.Cognome))
            errors += "Il campo 'Cognome' del genitore è obbligatorio.\n";
          if (string.IsNullOrWhiteSpace(genitore.Nome))
            errors += "Il campo 'Nome' del genitore è obbligatorio.\n";
          if (genitore.Genere == null)
            errors += "Il campo 'Genere' del genitore è obbligatorio\n";
        }
      }

      if (!IsEmailValida(modulo.EmailGenitore))
        errors += "L'indirizzo 'Email genitore' non è valido.\n";

      foreach (var settimana in SettimaneModulo.SceltaDoppia(modulo.Settimane))
        errors += $"Settimana '{settimana}': scegli solo 'con pranzo' oppure 'senza pranzo'.\n";

      return errors;
    }

    /// <summary>Vuota = valida (il campo non è obbligatorio).</summary>
    public static bool IsEmailValida(string? email) =>
      string.IsNullOrWhiteSpace(email) || EmailRegex.IsMatch(email.Trim());
  }
}
