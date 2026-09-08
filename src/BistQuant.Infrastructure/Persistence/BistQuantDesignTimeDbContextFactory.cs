using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BistQuant.Infrastructure.Persistence;

public class BistQuantDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BistQuantDbContext>
{
    public BistQuantDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("EF_PROVIDER")?.ToLowerInvariant();
        var optionsBuilder = new DbContextOptionsBuilder<BistQuantDbContext>();

        if (provider == "sqlserver")
        {
            optionsBuilder.UseSqlServer("Server=localhost;Database=BistQuant_Dev;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;",
                b => b.MigrationsAssembly(typeof(BistQuantDbContext).Assembly.FullName));
        }
        else
        {
            optionsBuilder.UseSqlite("Data Source=bistquant_dev.db",
                b => b.MigrationsAssembly(typeof(BistQuantDbContext).Assembly.FullName));
        }

        optionsBuilder.ReplaceService<IMigrationsAssembly, ProviderSpecificMigrationsAssembly>();
        return new BistQuantDbContext(optionsBuilder.Options);
    }
}
