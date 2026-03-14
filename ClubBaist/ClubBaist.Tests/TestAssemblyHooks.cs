namespace ClubBaist.Tests;

[TestClass]
public sealed class TestAssemblyHooks
{
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext _)
    {
        TestServiceHost.Initialize();
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        TestServiceHost.Cleanup();
    }
}