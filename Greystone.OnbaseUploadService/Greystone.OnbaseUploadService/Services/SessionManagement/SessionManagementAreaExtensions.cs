namespace Greystone.OnbaseUploadService.Services.SessionManagement;

public static class SessionManagementAreaExtensions
{
	public static IServiceCollection AddOnBaseSessionManagement(
		this IServiceCollection serviceCollection)
	{
		serviceCollection.AddSingleton<ISessionFactory, SessionFactory>();
		serviceCollection.AddSingleton<IUnitySessionService, UnitySessionService>();

		return serviceCollection;
	}
}