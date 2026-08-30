using FluentAssertions;
using LanguageExt.UnitTesting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SvxlinkManagerV2.Infrastructure.Persistence;

namespace SvxlinkManagerV2.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests unitaires pour UserAccountService avec SQLite in-memory et ASP.NET Identity.
/// Une connexion SQLite est maintenue ouverte pour toute la durée du test afin de
/// conserver la base de données en mémoire entre les résolutions de DbContext par le DI.
/// </summary>
public class UserAccountServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly SvxlinkDbContext _dbContext;

    public UserAccountServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var services = new ServiceCollection();

        services.AddDbContext<SvxlinkDbContext>(options =>
            options.UseSqlite(_connection));

        services.AddIdentityCore<IdentityUser>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;
        })
        .AddEntityFrameworkStores<SvxlinkDbContext>();

        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<SvxlinkDbContext>();
        _dbContext.Database.EnsureCreated();
    }

    private UserAccountService CreateService()
    {
        var userManager = _serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var logger = _serviceProvider.GetRequiredService<ILogger<UserAccountService>>();
        return new UserAccountService(userManager, logger);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _serviceProvider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task HasAnyUserAsync_WhenNoUsers_ReturnsFalse()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.HasAnyUserAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasAnyUserAsync_WhenUserExists_ReturnsTrue()
    {
        // Arrange
        var service = CreateService();
        await service.CreateUserAsync("testuser", "Password123");

        // Act
        var result = await service.HasAnyUserAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CreateUserAsync_WithValidCredentials_ReturnsSuccess()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.CreateUserAsync("newuser", "password123");

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task CreateUserAsync_WithShortPassword_ReturnsFail()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.CreateUserAsync("user", "abc");

        // Assert
        result.ShouldBeFail();
    }

    [Fact]
    public async Task CreateUserAsync_WithDuplicateUsername_ReturnsFail()
    {
        // Arrange
        var service = CreateService();
        await service.CreateUserAsync("admin", "password123");

        // Act
        var result = await service.CreateUserAsync("admin", "otherpassword");

        // Assert
        result.ShouldBeFail();
    }

    [Fact]
    public async Task ChangePasswordAsync_WithCorrectCurrentPassword_ReturnsSuccess()
    {
        // Arrange
        var service = CreateService();
        await service.CreateUserAsync("changeuser", "oldpassword");

        var userManager = _serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByNameAsync("changeuser");
        var userId = user!.Id;

        // Act
        var result = await service.ChangePasswordAsync(userId, "oldpassword", "newpassword");

        // Assert
        result.ShouldBeSuccess();
    }

    [Fact]
    public async Task ChangePasswordAsync_WithWrongCurrentPassword_ReturnsFailWithWrongPasswordCode()
    {
        // Arrange
        var service = CreateService();
        await service.CreateUserAsync("changeuser2", "correctpassword");

        var userManager = _serviceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await userManager.FindByNameAsync("changeuser2");
        var userId = user!.Id;

        // Act
        var result = await service.ChangePasswordAsync(userId, "wrongpassword", "newpassword");

        // Assert
        result.ShouldBeFail();
        result.IfFail(errors =>
            errors.Any(e => e.Message.Contains("USER_WRONG_CURRENT_PASSWORD")).Should().BeTrue());
    }

    [Fact]
    public async Task ChangePasswordAsync_WithNonExistentUserId_ReturnsFail()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.ChangePasswordAsync("nonexistent-id", "password", "newpassword");

        // Assert
        result.ShouldBeFail();
    }
}
