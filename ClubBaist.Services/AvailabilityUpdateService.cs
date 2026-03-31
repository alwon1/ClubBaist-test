namespace ClubBaist.Services;

/// <summary>
/// Singleton event bus that notifies Blazor circuits when tee-time availability changes.
/// Components subscribe on mount and unsubscribe on dispose to receive push updates.
/// </summary>
public sealed class AvailabilityUpdateService
{
    public event Action<DateOnly>? AvailabilityChanged;

    public void Notify(DateOnly date) => AvailabilityChanged?.Invoke(date);
}
