using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using Dragonfly44.Infrastructure.DependencyInjection;
using Dragonfly44.Infrastructure.Exceptions;
using Dragonfly44.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dragonfly44.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddDragonflyInfrastructure_ShouldResolveAllRequiredServices()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:DatabaseFileName"] = "test.db",
                ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDragonflyInfrastructure(configuration, "Data Source=:memory:");

        var provider = services.BuildServiceProvider();

        // Act & Assert
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<Dragonfly44DbContext>();
        var initializer = scope.ServiceProvider.GetService<IDatabaseInitializer>();
        var projectRepo = scope.ServiceProvider.GetService<IProjectRepository>();
        var projectService = scope.ServiceProvider.GetService<IProjectService>();
        var engineRegistry = scope.ServiceProvider.GetService<IScanEngineRegistry>();
        var exceptionHandler = scope.ServiceProvider.GetService<GlobalExceptionHandler>();

        dbContext.Should().NotBeNull();
        initializer.Should().NotBeNull();
        projectRepo.Should().NotBeNull();
        projectService.Should().NotBeNull();
        engineRegistry.Should().NotBeNull();
        exceptionHandler.Should().NotBeNull();
    }
}
