namespace ClubBaist.Tests;

[TestClass]
public sealed class TestAssemblyHooks
{
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext _)
    {
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
    }
}