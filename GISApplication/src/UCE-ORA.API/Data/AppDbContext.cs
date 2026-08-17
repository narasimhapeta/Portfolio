using Microsoft.EntityFrameworkCore;
using UCE_ORA.API.Models;

namespace UCE_ORA.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TransmissionLine> TransmissionLines { get; set; }
}
