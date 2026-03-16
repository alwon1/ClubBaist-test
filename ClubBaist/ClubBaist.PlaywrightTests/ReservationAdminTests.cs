using ClubBaist.PlaywrightTests.Helpers;

namespace ClubBaist.PlaywrightTests;

/// <summary>
/// Covers CoreDeliverables admin/staff tee time flows:
///   - Create Reservation (staff books on behalf of a member via Staff Console)
///   - View Reservation details
///   - Update Reservation
///   - Delete/Cancel Reservation
/// </summary>
[TestClass]
public class ReservationAdminTests : PageTest
{
    private string BaseUrl => AspirePlaywrightFixture.BaseUrl;

    // A date well within the 2026 season (Apr 1 – Sep 30, 2026)
    private const string SeasonDate = "2026-07-10";

    [TestInitialize]
    public void SetTimeouts() => Page.SetDefaultTimeout(60_000);

    // ------------------------------------------------------------------ Create via Staff Console
    [TestMethod]
    public async Task Admin_CreateReservation_ForMember_Succeeds()
    {
        await PageHelpers.LoginAsync(Page, BaseUrl, "admin@clubbaist.com", "Pass@word1");

        // Navigate to the Staff Console and select Alice Shareholder (member #1)
        await Page.GotoAsync($"{BaseUrl}/teetimes/staff");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Row)
            .Filter(new() { HasText = "Alice" })
            .GetByRole(AriaRole.Button, new() { Name = "Select" })
            .ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click "Book on Behalf" to start a booking for Alice
        await Page.GetByRole(AriaRole.Button, new() { Name = "Book on Behalf" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // We are now on the Availability page (or redirected with member context).
        // Select the season date and pick an available slot.
        await Page.GetByLabel("Select Date").FillAsync(SeasonDate);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click the first available "Book" button to navigate to CreateReservation
        await Page.GetByRole(AriaRole.Button, new() { Name = "Book" }).First.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // On CreateReservation: confirm and submit
        await Page.GetByRole(AriaRole.Button, new() { Name = "Book Tee Time" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByText("booked successfully")).ToBeVisibleAsync();
    }

    // ------------------------------------------------------------------ View Reservation Detail
    [TestMethod]
    public async Task Admin_ViewReservationDetail_ShowsCorrectInfo()
    {
        await PageHelpers.LoginAsync(Page, BaseUrl, "admin@clubbaist.com", "Pass@word1");

        // Navigate to Staff Console → Reservations tab to find an existing booking
        await Page.GotoAsync($"{BaseUrl}/teetimes/staff");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Select Alice to see her reservations
        await Page.GetByRole(AriaRole.Row)
            .Filter(new() { HasText = "Alice" })
            .GetByRole(AriaRole.Button, new() { Name = "Select" })
            .ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Switch to the Reservations tab
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Reservations" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Open the first reservation's detail page
        await Page.GetByRole(AriaRole.Link, new() { Name = "View/Edit" }).First.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Reservation Details" }))
            .ToBeVisibleAsync();
        await Expect(Page.GetByText("Date:")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Time:")).ToBeVisibleAsync();
    }

    // ------------------------------------------------------------------ Update Reservation
    [TestMethod]
    public async Task Admin_UpdateReservation_Succeeds()
    {
        await PageHelpers.LoginAsync(Page, BaseUrl, "admin@clubbaist.com", "Pass@word1");

        // Get to a reservation detail page via Staff Console
        await Page.GotoAsync($"{BaseUrl}/teetimes/staff");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Row)
            .Filter(new() { HasText = "Alice" })
            .GetByRole(AriaRole.Button, new() { Name = "Select" })
            .ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Tab, new() { Name = "Reservations" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Link, new() { Name = "View/Edit" }).First.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click Edit Players to enter edit mode
        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit Players" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Save without making changes (verifies the save flow works)
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save Changes" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByText("updated successfully")).ToBeVisibleAsync();
    }

    // ------------------------------------------------------------------ Delete / Cancel Reservation
    [TestMethod]
    public async Task Admin_CancelReservation_Succeeds()
    {
        await PageHelpers.LoginAsync(Page, BaseUrl, "admin@clubbaist.com", "Pass@word1");

        // Create a reservation first so we have one to cancel
        await Page.GotoAsync($"{BaseUrl}/teetimes/staff");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Row)
            .Filter(new() { HasText = "Alice" })
            .GetByRole(AriaRole.Button, new() { Name = "Select" })
            .ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Book on Behalf" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByLabel("Select Date").FillAsync("2026-08-05");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Book" }).First.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Book Tee Time" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByText("booked successfully")).ToBeVisibleAsync();

        // Now navigate to the staff console Reservations view and cancel that reservation
        await Page.GotoAsync($"{BaseUrl}/teetimes/staff");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Row)
            .Filter(new() { HasText = "Alice" })
            .GetByRole(AriaRole.Button, new() { Name = "Select" })
            .ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Tab, new() { Name = "Reservations" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Cancel the most recent (first) active reservation inline from staff console
        await Page.GetByRole(AriaRole.Row)
            .Filter(new() { HasText = "Aug" })
            .GetByRole(AriaRole.Button, new() { Name = "Cancel" })
            .First.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Confirm the cancellation prompt
        await Page.GetByRole(AriaRole.Button, new() { Name = "Confirm" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByText("cancelled successfully")).ToBeVisibleAsync();
    }
}
