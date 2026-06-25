using System.Security.Claims;

namespace AutoInsurance.Gateway.Middleware;

public class MockAuthMiddleware
{
    private readonly RequestDelegate _next;

    public MockAuthMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "dev-user-001"),
            new Claim(ClaimTypes.Email, "dev@autoinsurance.local"),
            new Claim("http://schemas.microsoft.com/identity/claims/objectidentifier", "dev-b2c-object-id-001"),
            new Claim(ClaimTypes.Role, "Policyholder")
        };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "MockAuth"));
        await _next(context);
    }
}
