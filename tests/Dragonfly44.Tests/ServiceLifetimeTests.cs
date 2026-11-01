using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using Dragonfly44.Infrastructure.DependencyInjection;
using Dragonfly44.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dragonfly44.Tests;

public class ServiceLifetimeTests
{
    private IServiceProvider CreateTestServiceProvider(string? testDbPath = null)
    {
        var dbFile = testDbPath ?? Path.Combine(Path.GetTempPath(), $"dragonfly44_di_test_{Guid.NewGuid():N}.db");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:DatabaseFileName"] = Path.GetFileName(dbFile),
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={dbFile}"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDragonflyInfrastructure(configuration, $"Data Source={dbFile}");

        // Add application services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IWindowStateService, WindowStateService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IKeyboardShortcutService, KeyboardShortcutService>();
        services.AddSingleton<IDialogService, DialogService>();

        // Build with validateScopes = true to catch improper root resolution
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    [Fact]
    public void ScopedServices_WhenResolvedFromRoot_WithScopeValidation_ShouldThrowException()
    {
        var provider = CreateTestServiceProvider();

        // Resolving scoped service from root provider directly should throw InvalidOperationException when scope validation is on
        var resolveScopedFromRoot = () => provider.GetRequiredService<IProjectService>();
        resolveScopedFromRoot.Should().Throw<InvalidOperationException>();

        var resolveDbContextFromRoot = () => provider.GetRequiredService<Dragonfly44DbContext>();
        resolveDbContextFromRoot.Should().Throw<InvalidOperationException>();

        var resolveInitializerFromRoot = () => provider.GetRequiredService<IDatabaseInitializer>();
        resolveInitializerFromRoot.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ScopedServices_WhenResolvedWithinExplicitScope_ShouldSucceedAndIsolateContexts()
    {
        var provider = CreateTestServiceProvider();

        Dragonfly44DbContext? context1;
        Dragonfly44DbContext? context2;

        using (var scope1 = provider.CreateScope())
        {
            var projectService1 = scope1.ServiceProvider.GetRequiredService<IProjectService>();
            context1 = scope1.ServiceProvider.GetRequiredService<Dragonfly44DbContext>();

            projectService1.Should().NotBeNull();
            context1.Should().NotBeNull();
        }

        using (var scope2 = provider.CreateScope())
        {
            var projectService2 = scope2.ServiceProvider.GetRequiredService<IProjectService>();
            context2 = scope2.ServiceProvider.GetRequiredService<Dragonfly44DbContext>();

            projectService2.Should().NotBeNull();
            context2.Should().NotBeNull();
        }

        // Contexts across scopes must be distinct instances
        context1.Should().NotBeSameAs(context2);
    }

    [Fact]
    public async Task ServiceScopeFactory_ShouldAllowOperationsToExecuteScopedWorkUnitsSafely()
    {
        var provider = CreateTestServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        using (var scope = scopeFactory.CreateScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
            await initializer.InitializeAsync();

            var projectService = scope.ServiceProvider.GetRequiredService<IProjectService>();
            var projects = await projectService.GetAllProjectsAsync();
            projects.Should().BeEmpty("Database is genuinely empty without production sample seeding");
        }
    }
}
