namespace Greystone.OnbaseUploadService.Services.SessionManagement;

/// <summary>
/// Provides a mechanism to "rent" an OnBase session for use during a single transaction.
/// </summary>
public interface IUnitySessionService
{
    /// <summary>
    /// request a new unity client session
    /// </summary>
    /// <returns>A rented session, or an exception if a connection could not be made</returns>
    public IRentedSession RentConnection();
}