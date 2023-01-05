using Greystone.OnbaseUploadService.Services.SessionManagement;

namespace Greystone.OnbaseUploadService.Services.Locking;

public static class LockingAreaExtensions
{
	public static IServiceCollection AddFileLocking(
		this IServiceCollection serviceCollection)
	{
		serviceCollection.AddSingleton<IFileLockService, FileLockService>();

		return serviceCollection;
	}
}