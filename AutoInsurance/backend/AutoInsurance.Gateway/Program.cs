using AutoInsurance.Gateway.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

var authMode = builder.Configuration["Auth:Mode"] ?? "mock";

if (authMode == "mock")
{
    builder.Services
        .AddAuthentication()
        .AddScheme<AuthenticationSchemeOptions, MockBearerHandler>("Bearer", _ => { });
}
else if (authMode == "b2c")
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer("Bearer", options =>
        {
            options.Authority = builder.Configuration["Auth:B2C:Authority"]
                ?? throw new InvalidOperationException("Auth:B2C:Authority required in b2c mode");
            options.Audience = builder.Configuration["Auth:B2C:ClientId"]
                ?? throw new InvalidOperationException("Auth:B2C:ClientId required in b2c mode");
        });
}

builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

if (authMode == "mock")
{
    app.UseMiddleware<MockAuthMiddleware>();
}
else
{
    app.UseAuthentication();
    app.UseAuthorization();
}

await app.UseOcelot();
app.Run();
