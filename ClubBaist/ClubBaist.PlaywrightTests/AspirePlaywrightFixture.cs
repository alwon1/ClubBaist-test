namespace ClubBaist.PlaywrightTests;

/// <summary>
/// Assembly-level fixture that starts the full Aspire stack (SQL Server → Seeder → Web)
/// once for the entire test run, then tears it down after all tests complete.
/// </summary>
[TestClass]
public static class AspirePlaywrightFixture
{
    private static DistributedApplication? _app;

    /// <summary>Base URL of the running Web application, available to all test classes.</summary>
    public static string BaseUrl { get; private set; } = string.Empty;

    [AssemblyInitialize]
    public static async Task InitAsync(TestContext _)
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ClubBaist_AppHost>();

        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        // "web" matches the resource name in AppHost.cs: builder.AddProject<...>("web")
        var httpClient = _app.CreateHttpClient("web");
        BaseUrl = httpClient.BaseAddress!.ToString().TrimEnd('/');
    }

    [AssemblyCleanup]
    public static async Task CleanupAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }
}
