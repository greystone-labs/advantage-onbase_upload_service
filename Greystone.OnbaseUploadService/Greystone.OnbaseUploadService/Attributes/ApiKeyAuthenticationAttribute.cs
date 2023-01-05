using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Greystone.OnbaseUploadService.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class ApiKeyAuthenticationAttribute : Attribute, IAsyncActionFilter
{

	private static readonly string ApiKeyName = "X-API-KEY";
	
	public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
	{
		if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyName, out var extractedApiKey))
		{
			context.Result = new ContentResult { StatusCode = 401, Content = "No Api Key was provided" };

			return;
		}

		var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
		var apiKey = configuration.GetValue<string>("ApiKey");

		if (apiKey is null)
		{
			context.Result =
				new ContentResult { StatusCode = 500, Content = "No API Key configured on server" };

			return;
		}

		if (!apiKey.Equals(extractedApiKey))
		{
			context.Result = new ContentResult { StatusCode = 401, Content = "Api Key is not valid" };

			return;
		}
		
		await next();
	}
}