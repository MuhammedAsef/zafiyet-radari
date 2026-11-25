using System.Reflection;
using NetArchTest.Rules;

namespace ZafiyetRadari.UnitTests.Architecture;

/// <summary>
/// Katman kurallarını derleme sonrası doğrular. Biri yanlış referans eklerse test kırılır.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(Domain.AssemblyMarker).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(Application.AssemblyMarker).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(Infrastructure.AssemblyMarker).Assembly;
    private static readonly Assembly WebAssembly = typeof(Web.AssemblyMarker).Assembly;

    private const string ApplicationNs = "ZafiyetRadari.Application";
    private const string InfrastructureNs = "ZafiyetRadari.Infrastructure";
    private const string WebNs = "ZafiyetRadari.Web";
    private const string WorkerNs = "ZafiyetRadari.Worker";

    [Fact]
    public void Domain_ShouldNotDependOn_OtherLayers()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNs, InfrastructureNs, WebNs, WorkerNs)
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Domain_ShouldNotDependOn_EntityFramework()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Application_ShouldNotDependOn_InfrastructureOrHosts()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNs, WebNs, WorkerNs, "Microsoft.EntityFrameworkCore", "Microsoft.Data.SqlClient")
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOn_Hosts()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(WebNs, WorkerNs)
            .GetResult();

        AssertSuccessful(result);
    }

    [Fact]
    public void Web_ShouldNotAccess_DatabaseDirectly()
    {
        // Controller'lar DbContext'e veya SqlClient'a dokunmaz; veri Application servisleri üzerinden gelir.
        var result = Types.InAssembly(WebAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "ZafiyetRadari.Infrastructure.Persistence",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Data.SqlClient")
            .GetResult();

        AssertSuccessful(result);
    }

    private static void AssertSuccessful(TestResult result)
    {
        var failing = result.FailingTypeNames ?? [];
        Assert.True(result.IsSuccessful, $"Kuralı ihlal eden tipler: {string.Join(", ", failing)}");
    }
}
