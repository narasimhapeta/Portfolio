using AutoInsurance.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var connectionString = context.Configuration["ConnectionStrings:DefaultConnection"]
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required");

        services.AddDbContext<MasterDbContext>(options =>
            options.UseSqlServer(connectionString));
    })
    .Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("Running database migrations...");
    using var scope = host.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    await context.Database.MigrateAsync();
    logger.LogInformation("Migrations applied successfully.");
}
catch (Exception ex)
{
    logger.LogError(ex, "Migration failed.");
    Environment.Exit(1);
}
