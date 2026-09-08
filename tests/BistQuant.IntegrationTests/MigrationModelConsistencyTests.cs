using BistQuant.Domain.Entities;
using BistQuant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace BistQuant.IntegrationTests;

public class MigrationModelConsistencyTests
{
    [Fact]
    public void SqliteModel_HasNoPendingModelChanges_AgainstSnapshot()
    {
        var options = new DbContextOptionsBuilder<BistQuantDbContext>()
            .UseSqlite("DataSource=:memory:", b => b.MigrationsAssembly("BistQuant.Infrastructure"))
            .Options;

        using var context = new BistQuantDbContext(options);
        var differ = context.GetService<IMigrationsModelDiffer>();
        var designModel = context.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        var snapshot = new BistQuant.Infrastructure.Migrations.Sqlite.BistQuantDbContextModelSnapshot();
        var snapshotModel = GetFinalizedRelationalModel(context, snapshot);

        bool hasDifferences = differ.HasDifferences(snapshotModel, designModel);
        Assert.False(hasDifferences, "SQLite runtime model differs from Sqlite/BistQuantDbContextModelSnapshot!");
    }

    [Fact]
    public void SqlServerModel_HasNoPendingModelChanges_AgainstSnapshot()
    {
        var options = new DbContextOptionsBuilder<BistQuantDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=BistQuantModelCheck;Trusted_Connection=True;", b => b.MigrationsAssembly("BistQuant.Infrastructure"))
            .Options;

        using var context = new BistQuantDbContext(options);
        var differ = context.GetService<IMigrationsModelDiffer>();
        var designModel = context.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        var snapshot = new BistQuant.Infrastructure.Migrations.SqlServer.BistQuantDbContextModelSnapshot();
        var snapshotModel = GetFinalizedRelationalModel(context, snapshot);

        bool hasDifferences = differ.HasDifferences(snapshotModel, designModel);
        Assert.False(hasDifferences, "SQL Server runtime model differs from SqlServer/BistQuantDbContextModelSnapshot!");
    }

    private static IRelationalModel GetFinalizedRelationalModel(BistQuantDbContext context, ModelSnapshot snapshot)
    {
        var rawModel = snapshot.Model!;
        var finalizeMethod = rawModel.GetType().GetMethod("FinalizeModel", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var finalized = finalizeMethod != null ? (IModel)finalizeMethod.Invoke(rawModel, null)! : rawModel;
        var initializer = context.GetService<IModelRuntimeInitializer>();
        var runtimeModel = initializer.Initialize(finalized);
        return runtimeModel.GetRelationalModel();
    }

    [Fact]
    public void ModelIntegrity_VerifiesRequiredEntitiesAndFieldsMapped()
    {
        var options = new DbContextOptionsBuilder<BistQuantDbContext>()
            .UseSqlite("DataSource=:memory:", b => b.MigrationsAssembly("BistQuant.Infrastructure"))
            .Options;

        using var context = new BistQuantDbContext(options);
        var model = context.Model;

        // 1. BulletinFetchAttempt
        var fetchAttemptType = model.FindEntityType(typeof(BulletinFetchAttempt));
        Assert.NotNull(fetchAttemptType);
        Assert.NotNull(fetchAttemptType.FindProperty(nameof(BulletinFetchAttempt.AttemptedAtUtc)));
        Assert.NotNull(fetchAttemptType.FindProperty(nameof(BulletinFetchAttempt.NextAttemptAtUtc)));
        Assert.NotNull(fetchAttemptType.FindProperty(nameof(BulletinFetchAttempt.HttpStatusCode)));
        // Redundant aliases must NOT be mapped to EF columns
        Assert.Null(fetchAttemptType.FindProperty("AttemptedAt"));
        Assert.Null(fetchAttemptType.FindProperty("HttpStatus"));
        Assert.Null(fetchAttemptType.FindProperty("NextAttemptAt"));

        // Index on (SessionDate, AttemptedAtUtc)
        var fetchIndex = fetchAttemptType.GetIndexes().FirstOrDefault(i =>
            i.Properties.Count == 2 &&
            i.Properties[0].Name == nameof(BulletinFetchAttempt.SessionDate) &&
            i.Properties[1].Name == nameof(BulletinFetchAttempt.AttemptedAtUtc));
        Assert.NotNull(fetchIndex);

        // 2. IndicatorContinuityWarning
        var indicatorWarningType = model.FindEntityType(typeof(IndicatorContinuityWarning));
        Assert.NotNull(indicatorWarningType);

        // 3. PaperOrder properties
        var paperOrderType = model.FindEntityType(typeof(PaperOrder));
        Assert.NotNull(paperOrderType);
        Assert.NotNull(paperOrderType.FindProperty(nameof(PaperOrder.SourceSignalId)));
        Assert.NotNull(paperOrderType.FindProperty(nameof(PaperOrder.SignalSessionDate)));
        Assert.NotNull(paperOrderType.FindProperty(nameof(PaperOrder.TargetExecutionSessionDate)));
        Assert.NotNull(paperOrderType.FindProperty(nameof(PaperOrder.ExecutedSessionDate)));
        Assert.NotNull(paperOrderType.FindProperty(nameof(PaperOrder.CancellationReason)));

        // 4. MarketDataImport properties and revision index
        var importType = model.FindEntityType(typeof(MarketDataImport));
        Assert.NotNull(importType);
        Assert.NotNull(importType.FindProperty(nameof(MarketDataImport.RevisionNumber)));
        Assert.NotNull(importType.FindProperty(nameof(MarketDataImport.IsRevision)));
        Assert.NotNull(importType.FindProperty(nameof(MarketDataImport.SupersedesImportId)));
        Assert.NotNull(importType.FindProperty(nameof(MarketDataImport.IsCurrent)));
        Assert.NotNull(importType.FindProperty(nameof(MarketDataImport.NextAttemptAt)));

        var revisionIndex = importType.GetIndexes().FirstOrDefault(i =>
            i.Properties.Count == 3 &&
            i.Properties.Any(p => p.Name == nameof(MarketDataImport.Provider)) &&
            i.Properties.Any(p => p.Name == nameof(MarketDataImport.SessionDate)) &&
            i.Properties.Any(p => p.Name == nameof(MarketDataImport.Sha256)));
        Assert.NotNull(revisionIndex);
        Assert.True(revisionIndex.IsUnique);

        // 5. Symbol.LastSeenInBulletinDate
        var symbolType = model.FindEntityType(typeof(Symbol));
        Assert.NotNull(symbolType);
        Assert.NotNull(symbolType.FindProperty(nameof(Symbol.LastSeenInBulletinDate)));

        // 6. Signal unique index
        var signalType = model.FindEntityType(typeof(Signal));
        Assert.NotNull(signalType);
        var signalIndex = signalType.GetIndexes().FirstOrDefault(i =>
            i.Properties.Any(p => p.Name == nameof(Signal.SymbolId)) &&
            i.Properties.Any(p => p.Name == nameof(Signal.SourceSessionDate)) &&
            i.Properties.Any(p => p.Name == nameof(Signal.Timeframe)));
        Assert.NotNull(signalIndex);
        Assert.True(signalIndex.IsUnique);
    }
}
