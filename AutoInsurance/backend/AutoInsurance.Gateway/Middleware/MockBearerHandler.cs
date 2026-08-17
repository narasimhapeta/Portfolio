using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

namespace AutoInsurance.Gateway.Middleware;

public class MockBearerHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public MockBearerHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var ticket = new AuthenticationTicket(Context.User, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
