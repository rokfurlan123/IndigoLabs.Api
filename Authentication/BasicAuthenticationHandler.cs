using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace IndigoLabs.Api.Authentication;

public sealed class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _configuration;

    public BasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var header)
            || !BasicAuthenticationDefaults.AuthenticationScheme.Equals(
                header.Scheme,
                StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(header.Parameter))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Authorization header."));
        }

        var credentials = DecodeCredentials(header.Parameter);
        if (credentials is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid Basic authentication payload."));
        }

        var configuredUsername = _configuration["Authentication:Basic:Username"];
        var passwordHash = _configuration["Authentication:Basic:PasswordHash"];
        var passwordSalt = _configuration["Authentication:Basic:PasswordSalt"];
        var iterationCount = _configuration.GetValue("Authentication:Basic:PasswordHashIterations", 210_000);

        if (string.IsNullOrEmpty(configuredUsername)
            || string.IsNullOrEmpty(passwordHash)
            || string.IsNullOrEmpty(passwordSalt)
            || !string.Equals(credentials.Value.Username, configuredUsername, StringComparison.Ordinal)
            || !PasswordHashVerifier.Verify(
                credentials.Value.Password,
                passwordSalt,
                passwordHash,
                iterationCount))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid username or password."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, credentials.Value.Username)
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Basic realm=\"IndigoLabs.Api\"";
        return base.HandleChallengeAsync(properties);
    }

    private static (string Username, string Password)? DecodeCredentials(string parameter)
    {
        try
        {
            var credentialBytes = Convert.FromBase64String(parameter);
            var credentials = Encoding.UTF8.GetString(credentialBytes);
            var separatorIndex = credentials.IndexOf(':');

            if (separatorIndex <= 0)
            {
                return null;
            }

            return (
                credentials[..separatorIndex],
                credentials[(separatorIndex + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
