using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Tracker.RuntimeHost;

namespace Tracker.Tests;

/// <summary>
/// RUNTIME-HOST-008 の RuntimeHost scaffold / configuration contract を固定する。
/// </summary>
public class RuntimeHostScaffoldContractTests
{
    /// <summary>
    /// 何を確認しているか: Tracker.RuntimeHost project が solution entry として存在することを確認する。
    /// </summary>
    [Fact]
    public void RuntimeHostProject_IsIncludedInSolution()
    {
        var solutionPath = RepositoryPath("Duck.slnx");

        var solution = XDocument.Load(solutionPath);
        var projectPaths = solution
            .Descendants()
            .Where(element => element.Name.LocalName == "Project")
            .Select(element => element.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();

        Assert.Contains(
            "Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj",
            projectPaths);
    }

    /// <summary>
    /// 何を確認しているか: RuntimeHost options が設定なしでも default 実行周期を持つことを確認する。
    /// </summary>
    [Fact]
    public void RuntimeHostOptions_HasDefaultOperationLoopInterval()
    {
        using var host = BuildHost([]);

        var options = host.Services.GetRequiredService<IOptions<RuntimeHostOptions>>().Value;

        Assert.Equal(
            RuntimeHostOptions.DefaultOperationLoopIntervalMilliseconds,
            options.OperationLoopIntervalMilliseconds);
    }

    /// <summary>
    /// 何を確認しているか: RuntimeHost:OperationLoopIntervalMilliseconds を options へ binding できることを確認する。
    /// </summary>
    [Fact]
    public void RuntimeHostOptions_BindsOperationLoopInterval()
    {
        using var host = BuildHost(
        [
            KeyValuePair.Create<string, string?>(
                "RuntimeHost:OperationLoopIntervalMilliseconds",
                "25"),
        ]);

        var options = host.Services.GetRequiredService<IOptions<RuntimeHostOptions>>().Value;

        Assert.Equal(25, options.OperationLoopIntervalMilliseconds);
    }

    /// <summary>
    /// 何を確認しているか: RuntimeHost 実行周期が 0 以下の場合は起動時 validation error になることを確認する。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RuntimeHostOptions_RejectsNonPositiveOperationLoopIntervalOnStart(int intervalMilliseconds)
    {
        using var host = BuildHost(
        [
            KeyValuePair.Create<string, string?>(
                "RuntimeHost:OperationLoopIntervalMilliseconds",
                intervalMilliseconds.ToString()),
        ]);

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains(
            "RuntimeHost:OperationLoopIntervalMilliseconds",
            exception.Message,
            StringComparison.Ordinal);
    }

    private static IHost BuildHost(IEnumerable<KeyValuePair<string, string?>> configurationValues)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            EnvironmentName = "Testing",
        });
        builder.Configuration.AddInMemoryCollection(configurationValues);
        builder.Services.AddRuntimeHost(builder.Configuration);
        return builder.Build();
    }

    private static string RepositoryPath(params string[] segments)
    {
        return Path.Combine([FindRepositoryRoot(), .. segments]);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Duck.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root containing Duck.slnx was not found.");
    }
}
