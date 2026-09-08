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
        var userA = new User { Email = "usera@test.com", DisplayName = "User A", Role = "User" };
        var userB = new User { Email = "userb@test.com", DisplayName = "User B", Role = "User" };
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
}
