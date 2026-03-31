using ClubBaist.PlaywrightTests.Helpers;

namespace ClubBaist.PlaywrightTests;

/// <summary>
/// Covers CoreDeliverables membership flows:
///   - UC-MA-01: Submit Membership Application
///   - UC-MA-02: Review and Decide Membership Application (Approve / Reject)
/// </summary>
[TestClass]
public class MembershipApplicationTests : PageTest
{
    private string BaseUrl => AspirePlaywrightFixture.BaseUrl;

    [TestInitialize]
    public void SetTimeouts() => Page.SetDefaultTimeout(60_000);

    // ------------------------------------------------------------------ Submit Application
    [TestMethod]
    public async Task SubmitApplication_WithValidData_Succeeds()
    {
        // Login as an authenticated user (authorization required to access /membership/apply)
        await PageHelpers.LoginAsync(Page, BaseUrl, "silver@clubbaist.com", "Pass@word1");

        await Page.GotoAsync($"{BaseUrl}/membership/apply");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Fill personal information
        var id = Guid.NewGuid().ToString("N")[..8];
        await Page.GetByLabel("First Name").FillAsync($"Test{id}");
        await Page.GetByLabel("Last Name").FillAsync("Applicant");
        await Page.GetByLabel("Date of Birth").FillAsync("1990-06-15");
        await Page.GetByLabel("Email").FillAsync($"test{id}@example.com");
        await Page.GetByLabel("Phone").FillAsync("403-555-0199");
        await Page.GetByLabel("Address").FillAsync("789 Test Avenue");
        await Page.GetByLabel("Postal Code").FillAsync("T2P 2B2");

        // Fill employment information
        await Page.GetByLabel("Occupation").FillAsync("Engineer");
        await Page.GetByLabel("Company Name").FillAsync("Test Corp");

        // Membership details: select Associate category and two shareholder sponsors
        await Page.GetByLabel("Membership Category").SelectOptionAsync(new SelectOptionValue { Label = "Associate" });
        await Page.GetByLabel("Sponsor 1").SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await Page.GetByLabel("Sponsor 2").SelectOptionAsync(new SelectOptionValue { Index = 2 });

        await Page.GetByRole(AriaRole.Button, new() { Name = "Submit Application" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByText("submitted successfully")).ToBeVisibleAsync();
    }

    // ------------------------------------------------------------------ Review and Approve
    [TestMethod]
    public async Task ReviewApplication_Approve_ChangesStatusToAccepted()
    {
        // Step 1: Submit a new application as bronze member
        await PageHelpers.LoginAsync(Page, BaseUrl, "bronze@clubbaist.com", "Pass@word1");

        await Page.GotoAsync($"{BaseUrl}/membership/apply");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var id = Guid.NewGuid().ToString("N")[..8];
        var firstName = $"Approve{id}";
        await Page.GetByLabel("First Name").FillAsync(firstName);
        await Page.GetByLabel("Last Name").FillAsync("Tester");
        await Page.GetByLabel("Date of Birth").FillAsync("1985-03-20");
        await Page.GetByLabel("Email").FillAsync($"approve{id}@example.com");
        await Page.GetByLabel("Phone").FillAsync("403-555-0200");
        await Page.GetByLabel("Address").FillAsync("100 Approval Lane");
        await Page.GetByLabel("Postal Code").FillAsync("T2P 3C3");
        await Page.GetByLabel("Occupation").FillAsync("Manager");
        await Page.GetByLabel("Company Name").FillAsync("Approve Corp");
        await Page.GetByLabel("Membership Category").SelectOptionAsync(new SelectOptionValue { Label = "Associate" });
        await Page.GetByLabel("Sponsor 1").SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await Page.GetByLabel("Sponsor 2").SelectOptionAsync(new SelectOptionValue { Index = 2 });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Submit Application" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.GetByText("submitted successfully")).ToBeVisibleAsync();

        // Step 2: Switch to committee account
        await PageHelpers.LogoutAsync(Page, BaseUrl);
        await PageHelpers.LoginAsync(Page, BaseUrl, "committee@clubbaist.com", "Pass@word1");

        // Step 3: Open the application inbox and find the submitted application
        await Page.GotoAsync($"{BaseUrl}/membership/applications");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Row)
            .Filter(new() { HasText = firstName })
            .GetByRole(AriaRole.Link, new() { Name = "Review" })
            .ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Step 4: Approve the application
        await Page.GetByLabel("New Status").SelectOptionAsync(new SelectOptionValue { Label = "Accepted" });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Submit Decision" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByText("Application status changed to")).ToBeVisibleAsync();
    }

    // ------------------------------------------------------------------ Review and Reject
    [TestMethod]
    public async Task ReviewApplication_Reject_ChangesStatusToDenied()
    {
        // Step 1: Submit a new application as silver member
        await PageHelpers.LoginAsync(Page, BaseUrl, "silver@clubbaist.com", "Pass@word1");

        await Page.GotoAsync($"{BaseUrl}/membership/apply");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var id = Guid.NewGuid().ToString("N")[..8];
        var firstName = $"Reject{id}";
        await Page.GetByLabel("First Name").FillAsync(firstName);
        await Page.GetByLabel("Last Name").FillAsync("Tester");
        await Page.GetByLabel("Date of Birth").FillAsync("1992-07-10");
        await Page.GetByLabel("Email").FillAsync($"reject{id}@example.com");
        await Page.GetByLabel("Phone").FillAsync("403-555-0300");
        await Page.GetByLabel("Address").FillAsync("200 Rejection Road");
        await Page.GetByLabel("Postal Code").FillAsync("T2P 4D4");
        await Page.GetByLabel("Occupation").FillAsync("Analyst");
        await Page.GetByLabel("Company Name").FillAsync("Reject Corp");
        await Page.GetByLabel("Membership Category").SelectOptionAsync(new SelectOptionValue { Label = "Associate" });
        await Page.GetByLabel("Sponsor 1").SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await Page.GetByLabel("Sponsor 2").SelectOptionAsync(new SelectOptionValue { Index = 2 });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Submit Application" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Expect(Page.GetByText("submitted successfully")).ToBeVisibleAsync();

        // Step 2: Switch to committee account
        await PageHelpers.LogoutAsync(Page, BaseUrl);
        await PageHelpers.LoginAsync(Page, BaseUrl, "committee@clubbaist.com", "Pass@word1");

        // Step 3: Open inbox and find the application
        await Page.GotoAsync($"{BaseUrl}/membership/applications");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Row)
            .Filter(new() { HasText = firstName })
            .GetByRole(AriaRole.Link, new() { Name = "Review" })
            .ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Step 4: Reject the application
        await Page.GetByLabel("New Status").SelectOptionAsync(new SelectOptionValue { Label = "Denied" });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Submit Decision" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByText("Application status changed to")).ToBeVisibleAsync();
    }
}
