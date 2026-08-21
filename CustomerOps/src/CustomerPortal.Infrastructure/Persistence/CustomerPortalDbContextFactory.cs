using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CustomerPortal.Infrastructure.Persistence;

public class CustomerPortalDbContextFactory : IDesignTimeDbContextFactory<CustomerPortalDbContext>
{
    public CustomerPortalDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CustomerPortalDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=CustomerPortal;User Id=sa;Password=LocalDev!2026;TrustServerCertificate=True;")
            .Options;

        return new CustomerPortalDbContext(options);
    }
}
