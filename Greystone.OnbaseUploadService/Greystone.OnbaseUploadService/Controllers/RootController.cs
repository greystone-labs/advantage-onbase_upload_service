using Greystone.OnbaseUploadService.Attributes;

using Microsoft.AspNetCore.Mvc;

namespace Greystone.OnbaseUploadService.Controllers;

[ApiController]
[Route("/", Name = "Root")]
public class RootController : Controller
{
	/// <summary>
	/// health check endpoint
	/// </summary>
	/// <returns>204 if success, any 4xx or 5xx if not available</returns>
	[HttpGet]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
	public IActionResult Get()
		=> NoContent();
}