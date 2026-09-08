using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;

namespace BistQuant.Infrastructure.Persistence;

#pragma warning disable EF1001 // Internal EF Core API usage
public class ProviderSpecificMigrationsAssembly : MigrationsAssembly
{
    private readonly ICurrentDbContext _currentContext;

    public ProviderSpecificMigrationsAssembly(
        ICurrentDbContext currentContext,
        IDbContextOptions options,
        IMigrationsIdGenerator idGenerator,
        IDiagnosticsLogger<DbLoggerCategory.Migrations> logger)
        : base(currentContext, options, idGenerator, logger)
    {
        _currentContext = currentContext;
    }

    private bool IsSqlite => _currentContext.Context.Database.IsSqlite();

    private string TargetNamespace => IsSqlite
        ? "BistQuant.Infrastructure.Migrations.Sqlite"
        : "BistQuant.Infrastructure.Migrations.SqlServer";

    public override IReadOnlyDictionary<string, TypeInfo> Migrations
    {
        get
        {
            var all = base.Migrations;
            var filtered = all
                .Where(pair => pair.Value.Namespace == TargetNamespace)
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            return filtered;
        }
    }

    public override ModelSnapshot? ModelSnapshot
    {
        get
        {
            var targetNs = TargetNamespace;
            var snapshotType = Assembly.GetTypes()
                .FirstOrDefault(t => typeof(ModelSnapshot).IsAssignableFrom(t) && t.Namespace == targetNs);

            if (snapshotType != null)
            {
                return (ModelSnapshot?)Activator.CreateInstance(snapshotType);
            }

            return base.ModelSnapshot;
        }
    }
}
#pragma warning restore EF1001
