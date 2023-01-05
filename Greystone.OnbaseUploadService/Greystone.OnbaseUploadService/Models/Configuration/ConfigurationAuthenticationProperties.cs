namespace Greystone.OnbaseUploadService.Models.Configuration;

public class ConfigurationAuthenticationProperties
{
	public string Type { get; set; } = "OnBase"; // can also be domain
	
	public string Url { get; set; } = string.Empty;

	public string Username { get; set; } = string.Empty;

	public string Password { get; set; } = string.Empty;

	public string DataSource { get; set; } = string.Empty;


}