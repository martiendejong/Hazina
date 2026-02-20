using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hazina.Data.Contexts
{
    /// <summary>
    /// Design-time factory for GeometricReasoningDbContext.
    /// Enables EF Core migrations and tools to create context instances.
    /// </summary>
    public class GeometricReasoningDbContextFactory : IDesignTimeDbContextFactory<GeometricReasoningDbContext>
    {
        public GeometricReasoningDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<GeometricReasoningDbContext>();

            // Use SQL Server LocalDB for development
            optionsBuilder.UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=HazinaGeometric;Trusted_Connection=True;MultipleActiveResultSets=true");

            return new GeometricReasoningDbContext(optionsBuilder.Options);
        }
    }
}
