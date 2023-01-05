using Hyland.Unity;

namespace Greystone.OnbaseUploadService.Services.SessionManagement;

/// <summary>
/// Rented session provides a mechanism for using a give-and-take connection style for
/// unity sessions.
/// On dispose, the session should be returned to availability 
/// </summary>
public interface IRentedSession : IDisposable
{
    /// <summary>
    /// An authenticated ready to use unity session
    /// </summary>
    public Application UnityApplication { get; }
}