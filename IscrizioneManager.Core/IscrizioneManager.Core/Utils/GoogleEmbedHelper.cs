using System.Text;
using System.Text.RegularExpressions;
using IscrizioneManager.Core.Models;

namespace IscrizioneManager.Core.Utils
{
  public enum GoogleLinkTipo
  {
    CartellaDrive,
    FileDrive,
    Foglio,
    Documento,
    Presentazione,
  }

  /// <summary>Link Google riconosciuto: tipo di risorsa, id e parametri da conservare.</summary>
  public sealed record GoogleLink(
    GoogleLinkTipo Tipo,
    string Id,
    string? ResourceKey = null,
    string? Gid = null,
    bool Pubblicato = false)
  {
    public bool IsCartella => Tipo == GoogleLinkTipo.CartellaDrive;

    /// <summary>Fogli, documenti e presentazioni (non pubblicati) si possono modificare nell'iframe.</summary>
    public bool SupportaModifica =>
      !Pubblicato && Tipo is GoogleLinkTipo.Foglio or GoogleLinkTipo.Documento or GoogleLinkTipo.Presentazione;
  }

  /// <summary>
  /// Converte i link di Google Drive / Fogli / Documenti / Presentazioni nella
  /// versione che Google permette di mostrare dentro un iframe.
  /// </summary>
  public static class GoogleEmbedHelper
  {
    private const string Drive = "https://drive.google.com";
    private const string Docs = "https://docs.google.com";

    private const string IdPattern = "[A-Za-z0-9_-]{10,}";

    private static readonly Regex CartellaRegex = new(
      $@"^/drive/(?:u/\d+/)?(?:mobile/)?folders/(?<id>{IdPattern})",
      RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex FileRegex = new(
      $@"^/file/(?:u/\d+/)?d/(?<id>{IdPattern})",
      RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex DocsRegex = new(
      $@"^/(?<app>spreadsheets|document|presentation)/(?:u/\d+/)?d/(?<pub>e/)?(?<id>{IdPattern})",
      RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex GidRegex = new(
      @"(?:^|[?#&])gid=(?<gid>\d+)",
      RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Riconosce un link Google. <paramref name="type"/> serve solo a decidere
    /// i link ambigui (open?id=...): cartella per Drive, foglio per Google Sheet.
    /// </summary>
    public static GoogleLink? Parse(string? url, UrlTypes type)
    {
      if (string.IsNullOrWhiteSpace(url))
        return null;

      var testo = url.Trim();
      if (!testo.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
          !testo.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        testo = "https://" + testo;

      if (!Uri.TryCreate(testo, UriKind.Absolute, out var uri))
        return null;

      var query = LeggiQuery(uri.Query);
      query.TryGetValue("resourcekey", out var resourceKey);
      var gidMatch = GidRegex.Match(uri.Query + uri.Fragment);
      var gid = gidMatch.Success ? gidMatch.Groups["gid"].Value : null;
      var path = uri.AbsolutePath;

      string? IdDaQuery() =>
        query.TryGetValue("id", out var id) && Regex.IsMatch(id, $"^{IdPattern}$") ? id : null;

      switch (uri.Host.ToLowerInvariant())
      {
        case "drive.google.com":
        {
          var m = CartellaRegex.Match(path);
          if (m.Success)
            return new GoogleLink(GoogleLinkTipo.CartellaDrive, m.Groups["id"].Value, resourceKey);

          m = FileRegex.Match(path);
          if (m.Success)
            return new GoogleLink(GoogleLinkTipo.FileDrive, m.Groups["id"].Value, resourceKey);

          var idQuery = IdDaQuery();
          if (idQuery == null)
            return null;

          return path.TrimEnd('/').ToLowerInvariant() switch
          {
            "/embeddedfolderview" or "/folderview" => new GoogleLink(GoogleLinkTipo.CartellaDrive, idQuery, resourceKey),
            "/uc" => new GoogleLink(GoogleLinkTipo.FileDrive, idQuery, resourceKey),
            "/open" => type == UrlTypes.GoogleSheet
              ? new GoogleLink(GoogleLinkTipo.Foglio, idQuery, resourceKey, gid)
              : new GoogleLink(GoogleLinkTipo.CartellaDrive, idQuery, resourceKey),
            _ => null,
          };
        }

        case "docs.google.com":
        {
          var m = DocsRegex.Match(path);
          if (m.Success)
          {
            var tipo = m.Groups["app"].Value.ToLowerInvariant() switch
            {
              "spreadsheets" => GoogleLinkTipo.Foglio,
              "document" => GoogleLinkTipo.Documento,
              _ => GoogleLinkTipo.Presentazione,
            };
            return new GoogleLink(tipo, m.Groups["id"].Value, resourceKey,
              tipo == GoogleLinkTipo.Foglio ? gid : null, m.Groups["pub"].Success);
          }

          var idQuery = IdDaQuery();
          if (idQuery != null && path.TrimEnd('/').Equals("/open", StringComparison.OrdinalIgnoreCase))
            return type == UrlTypes.GoogleSheet
              ? new GoogleLink(GoogleLinkTipo.Foglio, idQuery, resourceKey, gid)
              : new GoogleLink(GoogleLinkTipo.FileDrive, idQuery, resourceKey);

          return null;
        }

        default:
          return null;
      }
    }

    /// <summary>Id della risorsa Google (cartella, file, foglio...), o null se il link non è riconosciuto.</summary>
    public static string? ExtractId(string? url, UrlTypes type = UrlTypes.GoogleDrive) =>
      Parse(url, type)?.Id;

    /// <summary>
    /// URL da usare nell'iframe, o null se il link non è riconosciuto.
    /// <paramref name="soloLettura"/>: anteprima invece dell'editor (fogli, documenti, presentazioni).
    /// <paramref name="vistaLista"/>: cartelle Drive mostrate come lista invece che a griglia.
    /// </summary>
    public static string? ToEmbedUrl(string? url, UrlTypes type, bool soloLettura = false, bool vistaLista = false)
    {
      var link = Parse(url, type);
      return link == null ? null : ToEmbedUrl(link, soloLettura, vistaLista);
    }

    public static string ToEmbedUrl(GoogleLink link, bool soloLettura = false, bool vistaLista = false)
    {
      var id = Uri.EscapeDataString(link.Id);
      var rk = string.IsNullOrEmpty(link.ResourceKey) ? null : Uri.EscapeDataString(link.ResourceKey);

      if (link.Tipo == GoogleLinkTipo.CartellaDrive)
      {
        var sb = new StringBuilder($"{Drive}/embeddedfolderview?id={id}");
        if (rk != null)
          sb.Append("&resourcekey=").Append(rk);
        sb.Append(vistaLista ? "#list" : "#grid");
        return sb.ToString();
      }

      if (link.Tipo == GoogleLinkTipo.FileDrive)
        return $"{Drive}/file/d/{id}/preview" + (rk != null ? $"?resourcekey={rk}" : "");

      var app = link.Tipo switch
      {
        GoogleLinkTipo.Foglio => "spreadsheets",
        GoogleLinkTipo.Documento => "document",
        _ => "presentation",
      };

      if (link.Pubblicato)
      {
        return link.Tipo switch
        {
          GoogleLinkTipo.Foglio => $"{Docs}/spreadsheets/d/e/{id}/pubhtml?widget=true&headers=false"
            + (link.Gid != null ? $"&gid={link.Gid}" : ""),
          GoogleLinkTipo.Documento => $"{Docs}/document/d/e/{id}/pub?embedded=true",
          _ => $"{Docs}/presentation/d/e/{id}/embed",
        };
      }

      var parametri = new List<string>();
      if (!soloLettura)
        parametri.Add("rm=minimal");
      if (rk != null)
        parametri.Add("resourcekey=" + rk);

      var risultato = new StringBuilder($"{Docs}/{app}/d/{id}/{(soloLettura ? "preview" : "edit")}");
      if (parametri.Count > 0)
        risultato.Append('?').Append(string.Join("&", parametri));
      if (link.Gid != null)
        risultato.Append("#gid=").Append(link.Gid);
      return risultato.ToString();
    }

    private static Dictionary<string, string> LeggiQuery(string query)
    {
      var risultato = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      foreach (var parte in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
      {
        var i = parte.IndexOf('=');
        var chiave = Uri.UnescapeDataString(i < 0 ? parte : parte[..i]);
        var valore = i < 0 ? "" : Uri.UnescapeDataString(parte[(i + 1)..].Replace('+', ' '));
        risultato.TryAdd(chiave, valore);
      }
      return risultato;
    }
  }
}
