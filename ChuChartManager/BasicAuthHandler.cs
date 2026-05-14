using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ChuChartManager;

public class BasicAuthOptions : AuthenticationSchemeOptions { }

public class BasicAuthHandler(
    IOptionsMonitor<BasicAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<BasicAuthOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return Task.FromResult(AuthenticateResult.NoResult());

        try
        {
            var authHeader = AuthenticationHeaderValue.Parse(Request.Headers.Authorization!);
            if (authHeader.Scheme != "Basic" || authHeader.Parameter == null)
                return Task.FromResult(AuthenticateResult.Fail("Invalid scheme"));

            var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);
            var username = credentials[0];
            var password = credentials.Length > 1 ? credentials[1] : "";

            if (username == StaticSettings.Config.AuthUsername &&
                password == StaticSettings.Config.AuthPassword)
            {
                var claims = new[] { new Claim(ClaimTypes.Name, username) };
                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }

            return Task.FromResult(AuthenticateResult.Fail("Invalid credentials"));
        }
        catch
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid authorization header"));
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Basic realm=\"ChuChartManager\"";
        Response.StatusCode = 401;
        return Task.CompletedTask;
    }
}
