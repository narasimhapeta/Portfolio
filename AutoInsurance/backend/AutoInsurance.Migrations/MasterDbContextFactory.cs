using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AutoInsurance.Migrations;

public class MasterDbContextFactory : IDesignTimeDbContextFactory<MasterDbContext>
{
    public MasterDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=localhost,1433;Database=AutoInsurance;User Id=sa;Password=AutoIns@2026!;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new MasterDbContext(options);
    }
}
