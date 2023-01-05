using Hyland.Unity;

namespace Greystone.OnbaseUploadService.Services.SessionManagement;

public class RentedSession : IRentedSession
{
	private readonly UnitySessionService _unitySessionService;

	public RentedSession(UnitySessionService unitySessionService, Application application)
	{
		_unitySessionService = unitySessionService;
		UnityApplication = application;
	}

	public void Dispose()
		=> _unitySessionService.Release(this);

	public Application UnityApplication { get; }
}