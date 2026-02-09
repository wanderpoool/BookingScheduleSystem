namespace BookingScheduleSystem.Web.Services;

/// <summary>
/// Service for storing and retrieving data from browser local storage
/// </summary>
public interface ILocalStorageService
{
    Task<T?> GetItemAsync<T>(string key);
    Task SetItemAsync<T>(string key, T value);
    Task RemoveItemAsync(string key);
    Task ClearAsync();
}
