using BookingScheduleSystem.Contracts.Users;

namespace BookingScheduleSystem.Web.Services;

public interface IUserService
{
    Task<ListUsersResponse?> ListUsersAsync(bool? isProvider = null, bool? isActive = null, int page = 1, int pageSize = 20);
    Task<UserResponse?> GetUserAsync(Guid userId);
    Task<UserResponse?> UpdateUserAsync(Guid userId, UpdateUserRequest request);
    Task DeactivateUserAsync(Guid userId);
    Task DeleteUserAsync(Guid userId);
    Task ResetPasswordAsync(Guid userId, string newPassword);
}
