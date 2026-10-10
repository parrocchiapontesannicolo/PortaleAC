using IscrizioneManager.Core.Models;
using IscrizioneManager.Core.Utils;

namespace IscrizioneManager.Tests.Tests;

public class GoogleEmbedHelperTests
{
    private const string FolderId = "1AbCdEfGhIjKlMnOpQrStUvWxYz012345";
    private const string FileId = "1ZyXwVuTsRqPoNmLkJiHgFeDcBa98765_";
    private const string SheetId = "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms";
    private const string DocId = "1hVjK-Lmn0pQrStUvWxYzAbCdEfGhIjKlMnOp";

    // Cartelle Drive

    [Theory]
    [InlineData("https://drive.google.com/drive/folders/" + FolderId)]
    [InlineData("https://drive.google.com/drive/folders/" + FolderId + "?usp=sharing")]
    [InlineData("https://drive.google.com/drive/folders/" + FolderId + "?usp=drive_link")]
    [InlineData("https://drive.google.com/drive/u/0/folders/" + FolderId)]
    [InlineData("https://drive.google.com/drive/u/1/folders/" + FolderId + "?usp=sharing")]
    [InlineData("https://drive.google.com/drive/mobile/folders/" + FolderId)]
    [InlineData("https://drive.google.com/open?id=" + FolderId)]
    [InlineData("https://drive.google.com/open?id=" + FolderId + "&usp=sharing")]
    [InlineData("https://drive.google.com/embeddedfolderview?id=" + FolderId + "#list")]
    [InlineData("  https://drive.google.com/drive/folders/" + FolderId + "  ")]
    [InlineData("drive.google.com/drive/folders/" + FolderId)]
    [InlineData("http://drive.google.com/drive/folders/" + FolderId)]
    public void ToEmbedUrl_CartellaDrive_UsaEmbeddedFolderViewAGriglia(string url)
    {
        GoogleEmbedHelper.ToEmbedUrl(url, UrlTypes.GoogleDrive)
            .Should().Be($"https://drive.google.com/embeddedfolderview?id={FolderId}#grid");
    }

    [Fact]
    public void ToEmbedUrl_CartellaDrive_VistaLista()
    {
        GoogleEmbedHelper.ToEmbedUrl($"https://drive.google.com/drive/folders/{FolderId}?usp=sharing", UrlTypes.GoogleDrive, vistaLista: true)
            .Should().Be($"https://drive.google.com/embeddedfolderview?id={FolderId}#list");
    }

    [Fact]
    public void ToEmbedUrl_CartellaDrive_ConservaResourceKey()
    {
        var url = $"https://drive.google.com/drive/folders/{FolderId}?resourcekey=0-AbC_dEf-GhI&usp=sharing";

        GoogleEmbedHelper.ToEmbedUrl(url, UrlTypes.GoogleDrive)
            .Should().Be($"https://drive.google.com/embeddedfolderview?id={FolderId}&resourcekey=0-AbC_dEf-GhI#grid");
    }

    [Fact]
    public void Parse_CartellaDrive_RiconosceTipo()
    {
        var link = GoogleEmbedHelper.Parse($"https://drive.google.com/drive/u/0/folders/{FolderId}", UrlTypes.GoogleDrive);

        link.Should().NotBeNull();
        link!.Tipo.Should().Be(GoogleLinkTipo.CartellaDrive);
        link.IsCartella.Should().BeTrue();
        link.SupportaModifica.Should().BeFalse();
        link.Id.Should().Be(FolderId);
    }

    // File Drive

    [Theory]
    [InlineData("https://drive.google.com/file/d/" + FileId + "/view")]
    [InlineData("https://drive.google.com/file/d/" + FileId + "/view?usp=sharing")]
    [InlineData("https://drive.google.com/file/d/" + FileId + "/view?usp=drive_link")]
    [InlineData("https://drive.google.com/file/d/" + FileId + "/edit")]
    [InlineData("https://drive.google.com/file/d/" + FileId + "/preview")]
    [InlineData("https://drive.google.com/file/d/" + FileId)]
    [InlineData("https://drive.google.com/file/u/1/d/" + FileId + "/view")]
    [InlineData("https://drive.google.com/uc?id=" + FileId + "&export=download")]
    public void ToEmbedUrl_FileDrive_UsaPreview(string url)
    {
        GoogleEmbedHelper.ToEmbedUrl(url, UrlTypes.GoogleDrive)
            .Should().Be($"https://drive.google.com/file/d/{FileId}/preview");
    }

    [Fact]
    public void ToEmbedUrl_FileDrive_ConservaResourceKey()
    {
        var url = $"https://drive.google.com/file/d/{FileId}/view?usp=sharing&resourcekey=0-XyZ123";

        GoogleEmbedHelper.ToEmbedUrl(url, UrlTypes.GoogleDrive)
            .Should().Be($"https://drive.google.com/file/d/{FileId}/preview?resourcekey=0-XyZ123");
    }

    // Fogli Google

    [Theory]
    [InlineData("https://docs.google.com/spreadsheets/d/" + SheetId + "/edit")]
    [InlineData("https://docs.google.com/spreadsheets/d/" + SheetId + "/edit?usp=sharing")]
    [InlineData("https://docs.google.com/spreadsheets/d/" + SheetId)]
    [InlineData("https://docs.google.com/spreadsheets/d/" + SheetId + "/")]
    [InlineData("https://docs.google.com/spreadsheets/u/1/d/" + SheetId + "/edit")]
    [InlineData("https://docs.google.com/spreadsheets/d/" + SheetId + "/htmlview")]
    public void ToEmbedUrl_Foglio_ModificaMinimale(string url)
    {
        GoogleEmbedHelper.ToEmbedUrl(url, UrlTypes.GoogleSheet)
            .Should().Be($"https://docs.google.com/spreadsheets/d/{SheetId}/edit?rm=minimal");
    }

    [Theory]
    [InlineData("https://docs.google.com/spreadsheets/d/" + SheetId + "/edit#gid=123456789")]
    [InlineData("https://docs.google.com/spreadsheets/d/" + SheetId + "/edit?usp=sharing#gid=123456789")]
    [InlineData("https://docs.google.com/spreadsheets/u/0/d/" + SheetId + "/edit?gid=123456789#gid=123456789")]
    [InlineData("https://docs.google.com/spreadsheets/d/" + SheetId + "/edit#gid=123456789&range=A1:C10")]
    public void ToEmbedUrl_Foglio_ConservaGid(string url)
    {
        GoogleEmbedHelper.ToEmbedUrl(url, UrlTypes.GoogleSheet)
            .Should().Be($"https://docs.google.com/spreadsheets/d/{SheetId}/edit?rm=minimal#gid=123456789");
    }

    [Fact]
    public void ToEmbedUrl_Foglio_SoloLettura_UsaPreview()
    {
        GoogleEmbedHelper.ToEmbedUrl($"https://docs.google.com/spreadsheets/d/{SheetId}/edit#gid=0", UrlTypes.GoogleSheet, soloLettura: true)
            .Should().Be($"https://docs.google.com/spreadsheets/d/{SheetId}/preview#gid=0");
    }

    [Fact]
    public void ToEmbedUrl_Foglio_ConservaResourceKey()
    {
        var url = $"https://docs.google.com/spreadsheets/d/{SheetId}/edit?resourcekey=0-Key_1#gid=7";

        GoogleEmbedHelper.ToEmbedUrl(url, UrlTypes.GoogleSheet)
            .Should().Be($"https://docs.google.com/spreadsheets/d/{SheetId}/edit?rm=minimal&resourcekey=0-Key_1#gid=7");
        GoogleEmbedHelper.ToEmbedUrl(url, UrlTypes.GoogleSheet, soloLettura: true)
            .Should().Be($"https://docs.google.com/spreadsheets/d/{SheetId}/preview?resourcekey=0-Key_1#gid=7");
    }

    [Fact]
    public void ToEmbedUrl_OpenId_ConTipoFoglio_DiventaFoglio()
    {
        GoogleEmbedHelper.ToEmbedUrl($"https://drive.google.com/open?id={SheetId}", UrlTypes.GoogleSheet)
            .Should().Be($"https://docs.google.com/spreadsheets/d/{SheetId}/edit?rm=minimal");
    }

    [Fact]
    public void ToEmbedUrl_FoglioPubblicato_UsaPubHtml()
    {
        var url = "https://docs.google.com/spreadsheets/d/e/2PACX-1vQabcdefghijklmnopqrstuvwxyz0123456789/pubhtml";

        var link = GoogleEmbedHelper.Parse(url, UrlTypes.GoogleSheet);
        link!.Pubblicato.Should().BeTrue();
        link.SupportaModifica.Should().BeFalse();
        GoogleEmbedHelper.ToEmbedUrl(url, UrlTypes.GoogleSheet)
            .Should().Be("https://docs.google.com/spreadsheets/d/e/2PACX-1vQabcdefghijklmnopqrstuvwxyz0123456789/pubhtml?widget=true&headers=false");
    }

    [Fact]
    public void Parse_Foglio_SupportaModifica()
    {
        var link = GoogleEmbedHelper.Parse($"https://docs.google.com/spreadsheets/d/{SheetId}/edit#gid=5", UrlTypes.GoogleSheet);

        link.Should().Be(new GoogleLink(GoogleLinkTipo.Foglio, SheetId, null, "5"));
        link!.SupportaModifica.Should().BeTrue();
    }

    // Documenti e presentazioni

    [Fact]
    public void ToEmbedUrl_Documento()
    {
        var url = $"https://docs.google.com/document/d/{DocId}/edit?usp=sharing";

        GoogleEmbedHelper.ToEmbedUrl(url, UrlTypes.GoogleDrive)
            .Should().Be($"https://docs.google.com/document/d/{DocId}/edit?rm=minimal");
        GoogleEmbedHelper.ToEmbedUrl(url, UrlTypes.GoogleDrive, soloLettura: true)
            .Should().Be($"https://docs.google.com/document/d/{DocId}/preview");
    }

    [Fact]
    public void ToEmbedUrl_Documento_IgnoraFrammentoHeading()
    {
        GoogleEmbedHelper.ToEmbedUrl($"https://docs.google.com/document/u/2/d/{DocId}/edit#heading=h.abc123", UrlTypes.GoogleDrive)
            .Should().Be($"https://docs.google.com/document/d/{DocId}/edit?rm=minimal");
    }

    [Fact]
    public void ToEmbedUrl_Presentazione()
    {
        var url = $"https://docs.google.com/presentation/d/{DocId}/edit#slide=id.p1";

        GoogleEmbedHelper.ToEmbedUrl(url, UrlTypes.GoogleDrive)
            .Should().Be($"https://docs.google.com/presentation/d/{DocId}/edit?rm=minimal");
        GoogleEmbedHelper.ToEmbedUrl(url, UrlTypes.GoogleDrive, soloLettura: true)
            .Should().Be($"https://docs.google.com/presentation/d/{DocId}/preview");
    }

    // Link non riconosciuti

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("non è un link")]
    [InlineData("https://www.google.com/search?q=drive")]
    [InlineData("https://example.com/drive/folders/" + FolderId)]
    [InlineData("https://drive.google.com/drive/my-drive")]
    [InlineData("https://drive.google.com/drive/folders/")]
    [InlineData("https://drive.google.com/open")]
    [InlineData("https://drive.google.com/open?id=abc")]
    [InlineData("https://docs.google.com/forms/d/" + DocId + "/viewform")]
    [InlineData("ftp://drive.google.com/drive/folders/" + FolderId)]
    public void ToEmbedUrl_LinkNonRiconosciuto_RestituisceNull(string? url)
    {
        GoogleEmbedHelper.ToEmbedUrl(url, UrlTypes.GoogleDrive).Should().BeNull();
        GoogleEmbedHelper.ToEmbedUrl(url, UrlTypes.GoogleSheet).Should().BeNull();
    }

    // ExtractId

    [Theory]
    [InlineData("https://drive.google.com/drive/folders/" + FolderId + "?usp=sharing", FolderId)]
    [InlineData("https://drive.google.com/open?id=" + FolderId, FolderId)]
    [InlineData("https://drive.google.com/file/d/" + FileId + "/view", FileId)]
    [InlineData("https://docs.google.com/spreadsheets/u/1/d/" + SheetId + "/edit#gid=0", SheetId)]
    [InlineData("https://docs.google.com/document/d/" + DocId + "/edit", DocId)]
    [InlineData("https://example.com/x", null)]
    [InlineData(null, null)]
    public void ExtractId_RestituisceIdRisorsa(string? url, string? atteso)
    {
        GoogleEmbedHelper.ExtractId(url).Should().Be(atteso);
    }
}
