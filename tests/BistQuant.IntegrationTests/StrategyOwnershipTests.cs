using BistQuant.Application.Common.Interfaces;
using BistQuant.Application.DTOs.Strategies;
using BistQuant.Application.Services;
using BistQuant.Domain.Entities;
using BistQuant.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BistQuant.IntegrationTests;

public class StrategyOwnershipTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StrategyOwnershipTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StrategyOwnership_AuthorizationRules_EnforcedCorrectly()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var strategyEngine = scope.ServiceProvider.GetRequiredService<IStrategyEngine>();

        // 1. Create two test users
        var userA = new User { Email = $"usera_{Guid.NewGuid():N}@test.com", DisplayName = "User A", Role = "User" };
        var userB = new User { Email = $"userb_{Guid.NewGuid():N}@test.com", DisplayName = "User B", Role = "User" };
        context.Users.AddRange(userA, userB);
        await context.SaveChangesAsync();

        // 2. User A creates a custom strategy
        var createRequest = new CreateStrategyRequest(
            Name: "User A Proprietary Alpha",
            Description: "Private strategy of user A",
            StrategyType: "Momentum",
            Timeframe: Timeframe.Daily,
            Rules: new List<StrategyRuleDto>
            {
                new(0, "RSI", RuleOperator.GreaterThan, 50, null, null, 25, "Default", true)
            }
        );

        var createdStrategy = await strategyEngine.CreateStrategyAsync(createRequest, userA.Id);
        Assert.NotNull(createdStrategy);
        Assert.Equal(userA.Id, createdStrategy.UserId);
        Assert.False(createdStrategy.IsSystem);

        // 3. User B attempts to delete User A's strategy -> MUST FAIL
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await strategyEngine.DeleteStrategyAsync(createdStrategy.Id, userId: userB.Id, isAdmin: false);
        });

        // 4. Ensure strategy still exists in database
        var stratAfterFailedDelete = await context.Strategies.FindAsync(createdStrategy.Id);
        Assert.NotNull(stratAfterFailedDelete);

        // 5. User A attempts to delete a System Strategy -> MUST FAIL
        await strategyEngine.SeedPredefinedStrategiesAsync();
        var systemStrategy = await context.Strategies.FirstOrDefaultAsync(s => s.IsSystem);
        Assert.NotNull(systemStrategy);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await strategyEngine.DeleteStrategyAsync(systemStrategy.Id, userId: userA.Id, isAdmin: false);
        });

        // 6. User A deletes their OWN custom strategy -> MUST SUCCEED
        var deleted = await strategyEngine.DeleteStrategyAsync(createdStrategy.Id, userId: userA.Id, isAdmin: false);
        Assert.True(deleted);

        var stratAfterOwnDelete = await context.Strategies.FindAsync(createdStrategy.Id);
        Assert.Null(stratAfterOwnDelete);
    }

    [Fact]
    public async Task StrategyPrivacy_ReadAccessControl_EnforcedCorrectly()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var strategyEngine = scope.ServiceProvider.GetRequiredService<IStrategyEngine>();

        // Create users: Owner, Another User, Admin
        var owner = new User { Email = $"owner_{Guid.NewGuid():N}@test.com", DisplayName = "Strategy Owner", Role = "User" };
        var otherUser = new User { Email = $"other_{Guid.NewGuid():N}@test.com", DisplayName = "Other User", Role = "User" };
        var admin = new User { Email = $"admin_{Guid.NewGuid():N}@test.com", DisplayName = "Admin User", Role = "Admin" };
        context.Users.AddRange(owner, otherUser, admin);
        await context.SaveChangesAsync();

        // Create custom strategy for Owner
        var createRequest = new CreateStrategyRequest(
            Name: "Confidential Alpha Model",
            Description: "Proprietary model",
            StrategyType: "Trend",
            Timeframe: Timeframe.Daily,
            Rules: new List<StrategyRuleDto>
            {
                new(0, "Close", RuleOperator.GreaterThan, null, null, "EMA20", 30, "Trend", true)
            }
        );

        var customStrategy = await strategyEngine.CreateStrategyAsync(createRequest, owner.Id);
        Assert.NotNull(customStrategy);

        // Seed system strategies
        await strategyEngine.SeedPredefinedStrategiesAsync();
        var systemStrategy = await context.Strategies.FirstOrDefaultAsync(s => s.IsSystem);
        Assert.NotNull(systemStrategy);

        // Test 1: Anonymous user cannot read private custom strategy (returns null -> 404)
        var anonRead = await strategyEngine.GetStrategyByIdAsync(customStrategy.Id, currentUserId: null, isAdmin: false);
        Assert.Null(anonRead);

        // Test 2: Other normal user cannot read private custom strategy (returns null -> 404/403)
        var crossUserRead = await strategyEngine.GetStrategyByIdAsync(customStrategy.Id, currentUserId: otherUser.Id, isAdmin: false);
        Assert.Null(crossUserRead);

        // Test 3: Owner CAN read own strategy
        var ownerRead = await strategyEngine.GetStrategyByIdAsync(customStrategy.Id, currentUserId: owner.Id, isAdmin: false);
        Assert.NotNull(ownerRead);
        Assert.Equal("Confidential Alpha Model", ownerRead.Name);

        // Test 4: Admin CAN read any custom strategy
        var adminRead = await strategyEngine.GetStrategyByIdAsync(customStrategy.Id, currentUserId: admin.Id, isAdmin: true);
        Assert.NotNull(adminRead);
        Assert.Equal("Confidential Alpha Model", adminRead.Name);

        // Test 5: System strategies are publicly readable by anonymous user
        var anonSystemRead = await strategyEngine.GetStrategyByIdAsync(systemStrategy.Id, currentUserId: null, isAdmin: false);
        Assert.NotNull(anonSystemRead);
        Assert.True(anonSystemRead.IsSystem);

        // Test 6: System strategies are readable by normal user
        var userSystemRead = await strategyEngine.GetStrategyByIdAsync(systemStrategy.Id, currentUserId: otherUser.Id, isAdmin: false);
        Assert.NotNull(userSystemRead);

        // Test 7: Admin can delete custom strategy
        var adminDeleteResult = await strategyEngine.DeleteStrategyAsync(customStrategy.Id, userId: admin.Id, isAdmin: true);
        Assert.True(adminDeleteResult);
    }
}
