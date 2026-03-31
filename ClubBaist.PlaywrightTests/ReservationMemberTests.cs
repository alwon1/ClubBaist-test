using ClubBaist.PlaywrightTests.Helpers;

namespace ClubBaist.PlaywrightTests;

/// <summary>
/// Covers CoreDeliverables member tee time flows:
///   - Create Reservation by Membership Type (Gold/Shareholder can book any time)
///   - View "My Reservations"
///   - Update own Reservation
///   - Delete/Cancel own Reservation
///   - Business rule: cannot book a past date
/// </summary>
[TestClass]
public class ReservationMemberTests : PageTest
{
    private string BaseUrl => AspirePlaywrightFixture.BaseUrl;

    // Valid dates within the 2026 season (Apr 1 – Sep 30, 2026)
    private const string SeasonDateJune = "2026-06-20";
    private const string SeasonDateSept = "2026-09-05";

    [TestInitialize]
    public void SetTimeouts() => Page.SetDefaultTimeout(60_000);

    // ------------------------------------------------------------------ Gold member books any time
    [TestMethod]
    public async Task GoldMember_CreateReservation_AnyTime_Succeeds()
    {
        // shareholder1 is a Gold (Shareholder) member — can book any time of day
        await PageHelpers.LoginAsync(Page, BaseUrl, "shareholder1@clubbaist.com", "Pass@word1");

        await Page.GotoAsync($"{BaseUrl}/teetimes");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Select a date within the active season
        await Page.GetByLabel("Select Date").FillAsync(SeasonDateJune);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click the first available "Book" button to go to CreateReservation
        await Page.GetByRole(AriaRole.Button, new() { Name = "Book" }).First.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Confirm booking (no additional players needed for minimum 1-player reservation)
        await Page.GetByRole(AriaRole.Button, new() { Name = "Book Tee Time" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByText("booked successfully")).ToBeVisibleAsync();
    }

    // ------------------------------------------------------------------ View My Reservations
    [TestMethod]
    public async Task Member_ViewMyReservations_ShowsOwnBookings()
    {
        await PageHelpers.LoginAsync(Page, BaseUrl, "shareholder1@clubbaist.com", "Pass@word1");

        await Page.GotoAsync($"{BaseUrl}/teetimes/my");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My Reservations" }))
            .ToBeVisibleAsync();

        // At least one booking should be present (created by GoldMember_CreateReservation or seeded)
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "View/Edit" }).First)
            .ToBeVisibleAsync();
    }

    // ------------------------------------------------------------------ Update own Reservation
    [TestMethod]
    public async Task Member_UpdateReservation_Succeeds()
    {
        // Create a reservation to update
        await PageHelpers.LoginAsync(Page, BaseUrl, "shareholder2@clubbaist.com", "Pass@word1");

        await Page.GotoAsync($"{BaseUrl}/teetimes");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByLabel("Select Date").FillAsync(SeasonDateSept);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Book" }).First.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Book Tee Time" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByText("booked successfully")).ToBeVisibleAsync();

        // Navigate to My Reservations and open the newly created reservation
        await Page.GotoAsync($"{BaseUrl}/teetimes/my");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Link, new() { Name = "View/Edit" }).First.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Enter edit mode and add a player
        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit Players" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Button, new() { Name = "+ Add Player" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Select second player from the dropdown (index 1 to skip the empty placeholder)
        var playerDropdowns = Page.GetByLabel(new Regex("^Player \\d+$"));
        await playerDropdowns.First.SelectOptionAsync(new SelectOptionValue { Index = 1 });

        await Page.GetByRole(AriaRole.Button, new() { Name = "Save Changes" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByText("updated successfully")).ToBeVisibleAsync();
    }

    // ------------------------------------------------------------------ Delete / Cancel own Reservation
    [TestMethod]
    public async Task Member_CancelReservation_Succeeds()
    {
        // Create a fresh reservation to cancel
        await PageHelpers.LoginAsync(Page, BaseUrl, "shareholder3@clubbaist.com", "Pass@word1");

        await Page.GotoAsync($"{BaseUrl}/teetimes");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByLabel("Select Date").FillAsync("2026-05-22");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Book" }).First.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Book Tee Time" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByText("booked successfully")).ToBeVisibleAsync();

        // Navigate to My Reservations and cancel it
        await Page.GotoAsync($"{BaseUrl}/teetimes/my");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).First.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Confirm the inline cancel prompt
        await Page.GetByRole(AriaRole.Button, new() { Name = "Confirm" }).ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.GetByText("cancelled successfully")).ToBeVisibleAsync();
    }

    // ------------------------------------------------------------------ Business rule: no past-date booking
    [TestMethod]
    public async Task Member_CannotBookPastDate_ShowsValidationError()
    {
        await PageHelpers.LoginAsync(Page, BaseUrl, "shareholder1@clubbaist.com", "Pass@word1");

        await Page.GotoAsync($"{BaseUrl}/teetimes");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Select a date in the past
        await Page.GetByLabel("Select Date").FillAsync("2024-01-01");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // All slots should show as unavailable / no "Book" buttons should be enabled
        // OR clicking Book navigates to CreateReservation which shows a validation error
        var bookButtons = Page.GetByRole(AriaRole.Button, new() { Name = "Book" });
        var bookCount = await bookButtons.CountAsync();

        if (bookCount > 0)
        {
            // If a book button exists, clicking it should result in a validation error
            await bookButtons.First.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await Page.GetByRole(AriaRole.Button, new() { Name = "Book Tee Time" }).ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            await Expect(Page.GetByText(new Regex("past|invalid|cannot book", RegexOptions.IgnoreCase)))
                .ToBeVisibleAsync();
        }
        else
        {
            // No available slots shown for a past date — the availability page itself prevents booking
            await Expect(Page.GetByText(new Regex("no.*available|outside.*season|past", RegexOptions.IgnoreCase)))
                .ToBeVisibleAsync();
        }
    }
}
