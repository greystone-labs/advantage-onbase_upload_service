using Greystone.OnbaseUploadService.Models.Configuration;

using Hyland.Unity;

namespace Greystone.OnbaseUploadService.Services.SessionManagement;

public class SessionFactory : ISessionFactory
{
	private readonly IConfiguration _configuration;

	public SessionFactory(IConfiguration configuration)
	{
		_configuration = configuration;
	}

	public Application CreateApplication()
	{
		var authProps = _configuration.GetRequiredSection("OnBaseAuthentication")
			.Get<ConfigurationAuthenticationProperties>();

		var authenticationProperties = authProps.Type.ToUpper() switch
		{
			"DOMAIN" => Application.CreateDomainAuthenticationProperties(
				authProps.Url,
				authProps.DataSource),
			_ => (AuthenticationProperties) Application.CreateOnBaseAuthenticationProperties(
				authProps.Url,
				authProps.Username,
				authProps.Password,
				authProps.DataSource)
		};

		var application = Application.Connect(authenticationProperties);

		return application;
	}
}