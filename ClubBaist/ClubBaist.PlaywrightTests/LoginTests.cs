using ClubBaist.PlaywrightTests.Helpers;

namespace ClubBaist.PlaywrightTests;

/// <summary>
/// Verifies login flows for each role (CoreDeliverables: Login - Clerk, Pro-shop staff,
/// Membership Committee, Gold/Silver/Bronze members).
/// </summary>
[TestClass]
public class LoginTests : PageTest
{
    private string BaseUrl => AspirePlaywrightFixture.BaseUrl;

    [TestInitialize]
    public void SetTimeouts() => Page.SetDefaultTimeout(60_000);

    // ------------------------------------------------------------------ Admin
    [TestMethod]
    public async Task Login_AsAdmin_ShowsAdminNavigation()
    {
        await PageHelpers.LoginAsync(Page, BaseUrl, "admin@clubbaist.com", "Pass@word1");

        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Staff Console" }))
            .ToBeVisibleAsync();
    }

    // ------------------------------------------------------------------ Member (Gold / Shareholder)
    [TestMethod]
    public async Task Login_AsGoldMember_ShowsMemberNavigation()
    {
        await PageHelpers.LoginAsync(Page, BaseUrl, "shareholder1@clubbaist.com", "Pass@word1");

        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Tee Times" }))
            .ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "My Reservations" }))
            .ToBeVisibleAsync();
    }

    // ------------------------------------------------------------------ Membership Committee
    [TestMethod]
    public async Task Login_AsCommittee_ShowsApplicationsLink()
    {
        await PageHelpers.LoginAsync(Page, BaseUrl, "committee@clubbaist.com", "Pass@word1");

        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Applications" }))
            .ToBeVisibleAsync();
    }

    // ------------------------------------------------------------------ Invalid credentials
    [TestMethod]
    public async Task Login_WithInvalidCredentials_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/Account/Login");
        await Page.GetByLabel("Email").FillAsync("admin@clubbaist.com");
        await Page.GetByLabel("Password").FillAsync("WrongPassword!");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByText("Invalid login attempt.")).ToBeVisibleAsync();
    }
}
