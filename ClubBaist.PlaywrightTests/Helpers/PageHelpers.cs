namespace ClubBaist.PlaywrightTests.Helpers;

public static class PageHelpers
{
    /// <summary>Navigates to the login page, fills credentials, and waits for the redirect.</summary>
    public static async Task LoginAsync(IPage page, string baseUrl, string email, string password)
    {
        await page.GotoAsync($"{baseUrl}/Account/Login");
        await page.GetByLabel("Email").FillAsync(email);
        await page.GetByLabel("Password").FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>Clears session cookies so the next navigation is unauthenticated.</summary>
    public static async Task LogoutAsync(IPage page, string baseUrl)
    {
        await page.Context.ClearCookiesAsync();
        await page.GotoAsync($"{baseUrl}/");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
