using System.Net.Http;
using System.Text;
using System.Text.Json;
using IscrizioneManager.Core.Services;
using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;

namespace IscrizioneManager.Tests.Tests;

public class AuthLogicaTests
{
    private static readonly DateTimeOffset Adesso = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static string Jwt(string sub, DateTimeOffset scadenza)
    {
        static string B64(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var payload = JsonSerializer.Serialize(new { sub, exp = scadenza.ToUnixTimeSeconds(), role = "authenticated" });
        return $"{B64("{\"alg\":\"HS256\",\"typ\":\"JWT\"}")}.{B64(payload)}.firma";
    }

    private static GotrueException ErroreGotrue(int status, FailureHint.Reason motivo = FailureHint.Reason.Unknown)
    {
        var ex = new GotrueException("errore", motivo);
        typeof(GotrueException).GetProperty(nameof(GotrueException.StatusCode))!.SetValue(ex, status);
        return ex;
    }

    // ------------------------------------------------------------ JWT

    [Fact]
    public void LeggiScadenzaJwt_TokenValido_RestituisceExp()
    {
        var scadenza = Adesso.AddMinutes(40);
        AuthLogica.LeggiScadenzaJwt(Jwt("u1", scadenza)).Should().Be(scadenza);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonunjwt")]
    [InlineData("a.%%%.c")]
    [InlineData("a.bm9uIGpzb24.c")]
    public void LeggiScadenzaJwt_TokenIllegibile_Null(string? jwt)
        => AuthLogica.LeggiScadenzaJwt(jwt).Should().BeNull();

    [Fact]
    public void LeggiUtenteJwt_RestituisceSub()
        => AuthLogica.LeggiUtenteJwt(Jwt("utente-123", Adesso)).Should().Be("utente-123");

    // ------------------------------------------------------------ scadenza della sessione (AUTH-2)

    [Fact]
    public void AllineaScadenza_ExpiresInAssoluto_DiventaDurataResidua()
    {
        // Come la crea gotrue in SetSession con un token ancora valido
        var scadenza = Adesso.AddMinutes(40);
        var session = new Session
        {
            AccessToken = Jwt("u1", scadenza),
            RefreshToken = "r1",
            ExpiresIn = scadenza.ToUnixTimeSeconds()
        };

        AuthLogica.AllineaScadenza(session, Adesso.UtcDateTime);

        session.ExpiresIn.Should().Be(40 * 60);
        session.ExpiresAt().Should().Be(scadenza.UtcDateTime);
    }

    [Fact]
    public void AllineaScadenza_TokenGiaScaduto_ExpiresInZero()
    {
        var session = new Session { AccessToken = Jwt("u1", Adesso.AddMinutes(-5)), ExpiresIn = 3600 };
        AuthLogica.AllineaScadenza(session, Adesso.UtcDateTime);
        session.ExpiresIn.Should().Be(0);
    }

    [Fact]
    public void AllineaScadenza_TokenIllegibile_NonTocca()
    {
        var session = new Session { AccessToken = "x", ExpiresIn = 3600 };
        AuthLogica.AllineaScadenza(session, Adesso.UtcDateTime);
        session.ExpiresIn.Should().Be(3600);
    }

    // ------------------------------------------------------------ pianificazione del rinnovo

    [Fact]
    public void CalcolaAttesaRinnovo_PrimaDellaScadenzaMenoAnticipo()
        => AuthLogica.CalcolaAttesaRinnovo(Adesso.AddMinutes(60), Adesso, TimeSpan.FromSeconds(10))
            .Should().Be(TimeSpan.FromMinutes(55) - TimeSpan.FromSeconds(10));

    [Fact]
    public void CalcolaAttesaRinnovo_TokenScadutoInStandby_Subito()
        => AuthLogica.CalcolaAttesaRinnovo(Adesso.AddHours(-2), Adesso, TimeSpan.Zero).Should().Be(TimeSpan.Zero);

    [Fact]
    public void CalcolaAttesaRinnovo_ScadenzaLontana_Limitata()
        => AuthLogica.CalcolaAttesaRinnovo(Adesso.AddDays(7), Adesso, TimeSpan.Zero).Should().Be(AuthLogica.AttesaMassima);

    [Fact]
    public void CalcolaAttesaRinnovo_ScadenzaSconosciuta_UnMinuto()
        => AuthLogica.CalcolaAttesaRinnovo(null, Adesso, TimeSpan.Zero).Should().Be(TimeSpan.FromMinutes(1));

    [Fact]
    public void DaRinnovare_QuandoScattaIlTimer_True()
    {
        // Il timer scatta a (anticipo + casuale) dalla scadenza: deve rinnovare anche con il massimo sfasamento
        var casuale = TimeSpan.FromSeconds(29);
        var scadenza = Adesso.AddHours(1);
        var scatto = Adesso + AuthLogica.CalcolaAttesaRinnovo(scadenza, Adesso, casuale);
        AuthLogica.DaRinnovare(scadenza, scatto).Should().BeTrue();
    }

    [Fact]
    public void DaRinnovare_TokenAppenaEmesso_False()
        => AuthLogica.DaRinnovare(Adesso.AddMinutes(60), Adesso).Should().BeFalse();

    [Fact]
    public void DaRinnovare_TokenScaduto_True()
        => AuthLogica.DaRinnovare(Adesso.AddMinutes(-30), Adesso).Should().BeTrue();

    // ------------------------------------------------------------ sincronizzazione tra schede (AUTH-4)

    [Fact]
    public void DecidiSincronizzazione_StessoRefreshToken_Nessuna()
    {
        var access = Jwt("u1", Adesso.AddMinutes(30));
        AuthLogica.DecidiSincronizzazione(access, "r1", access, "r1").Should().Be(AzioneSincronizzazione.Nessuna);
    }

    [Fact]
    public void DecidiSincronizzazione_RuotatoDaAltraScheda_Adotta()
    {
        var vecchio = Jwt("u1", Adesso.AddMinutes(10));
        var nuovo = Jwt("u1", Adesso.AddMinutes(60));
        AuthLogica.DecidiSincronizzazione(vecchio, "r1", nuovo, "r2").Should().Be(AzioneSincronizzazione.Adotta);
    }

    [Fact]
    public void DecidiSincronizzazione_SalvatoPiuVecchio_Nessuna()
    {
        var memoria = Jwt("u1", Adesso.AddMinutes(60));
        var salvato = Jwt("u1", Adesso.AddMinutes(10));
        AuthLogica.DecidiSincronizzazione(memoria, "r2", salvato, "r1").Should().Be(AzioneSincronizzazione.Nessuna);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("a", null)]
    public void DecidiSincronizzazione_TokenRimossi_Termina(string? access, string? refresh)
        => AuthLogica.DecidiSincronizzazione(Jwt("u1", Adesso), "r1", access, refresh)
            .Should().Be(AzioneSincronizzazione.Termina);

    [Fact]
    public void DecidiSincronizzazione_AltroAccount_Termina()
    {
        var memoria = Jwt("animatore", Adesso.AddMinutes(30));
        var salvato = Jwt("responsabile", Adesso.AddMinutes(60));
        AuthLogica.DecidiSincronizzazione(memoria, "r1", salvato, "r9").Should().Be(AzioneSincronizzazione.Termina);
    }

    [Fact]
    public void DecidiSincronizzazione_SalvatoIllegibile_Nessuna()
        => AuthLogica.DecidiSincronizzazione(Jwt("u1", Adesso), "r1", "rotto", "r2")
            .Should().Be(AzioneSincronizzazione.Nessuna);

    // ------------------------------------------------------------ errori (AUTH-5)

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    public void IsErroreDefinitivo_RispostaAuth4xx_True(int status)
        => AuthLogica.IsErroreDefinitivo(ErroreGotrue(status)).Should().BeTrue();

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(408)]
    [InlineData(429)]
    public void IsErroreDefinitivo_ServerOLimiti_False(int status)
        => AuthLogica.IsErroreDefinitivo(ErroreGotrue(status)).Should().BeFalse();

    [Fact]
    public void IsErroreDefinitivo_GotrueOffline_False()
        => AuthLogica.IsErroreDefinitivo(new GotrueException("Failed to fetch", FailureHint.Reason.Offline, new HttpRequestException()))
            .Should().BeFalse();

    [Fact]
    public void IsErroreDefinitivo_SessioneNonCreata_True()
        => AuthLogica.IsErroreDefinitivo(new GotrueException("vuoto", FailureHint.Reason.NoSessionFound)).Should().BeTrue();

    [Fact]
    public void IsErroreDefinitivo_AccountDisattivato_True()
        => AuthLogica.IsErroreDefinitivo(new AccountNonAttivoException()).Should().BeTrue();

    [Fact]
    public void IsErroreDefinitivo_JwtMalformato_True()
        => AuthLogica.IsErroreDefinitivo(new ArgumentException("IDX12709")).Should().BeTrue();

    [Fact]
    public void IsErroreDefinitivo_Rete_False()
    {
        AuthLogica.IsErroreDefinitivo(new HttpRequestException("Failed to fetch")).Should().BeFalse();
        AuthLogica.IsErroreDefinitivo(new TaskCanceledException()).Should().BeFalse();
        AuthLogica.IsErroreDefinitivo(new Supabase.Postgrest.Exceptions.PostgrestException("{\"code\":\"PGRST000\"}")).Should().BeFalse();
    }

    // ------------------------------------------------------------ messaggi di login (BLZ-7)

    [Fact]
    public void MessaggioErroreLogin_Rete_ServerNonRaggiungibile()
        => AuthLogica.MessaggioErroreLogin(new GotrueException("Failed to fetch", FailureHint.Reason.Offline))
            .Should().Be(AuthService.MessaggioServerNonRaggiungibile);

    [Fact]
    public void MessaggioErroreLogin_TroppiTentativi()
        => AuthLogica.MessaggioErroreLogin(ErroreGotrue(429)).Should().Contain("Troppi tentativi");

    [Fact]
    public void MessaggioErroreLogin_NonMostraJsonGrezzo()
        => AuthLogica.MessaggioErroreLogin(new GotrueException("{\"code\":422,\"msg\":\"x\"}") { })
            .Should().NotContain("{");
}
