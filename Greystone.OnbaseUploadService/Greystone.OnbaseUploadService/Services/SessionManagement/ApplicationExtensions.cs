using Hyland.Unity;

namespace Greystone.OnbaseUploadService.Services.SessionManagement;

public static class ApplicationExtensions
{
	public static bool IsSessionConnected(this Application application)
		=> application.IsConnected && application.Ping();
}