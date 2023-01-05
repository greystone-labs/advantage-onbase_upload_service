using Greystone.OnbaseUploadService.Attributes;
using Greystone.OnbaseUploadService.Models.Dto.DocumentTypes;
using Greystone.OnbaseUploadService.Services.SessionManagement;

using Microsoft.AspNetCore.Mvc;

namespace Greystone.OnbaseUploadService.Controllers;


[ApiController]
[ApiKeyAuthentication]
[Route("v1/[controller]")]
public class DocumentTypesController : Controller
{
	private readonly IUnitySessionService _unitySessionService;

	public DocumentTypesController(IUnitySessionService unitySessionService)
	{
		_unitySessionService = unitySessionService;
	}


	/// <summary>
	/// Queries OnBase for all of the document type groups that the current
	/// user has access to
	/// </summary>
	/// <remarks>
	/// The user in question will be the user who has been configured in the web
	/// configuration file.
	/// </remarks>
	/// <returns>the list of document types the user has access to</returns>
	/// <response code="200">The doc types that the user has access to</response>
	/// <response code="403">The provided API key does not match the configured API key</response>
	/// <response code="404">The configured user has no document types available</response>
	/// <response code="500">the server was unable to get the document types, with reasons</response>
	[HttpGet(Name = "Document Types")]
	[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<DocumentTypeModel>))]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public IActionResult GetDocumentTypes()
	{
		using var connection = _unitySessionService.RentConnection();
		var application = connection.UnityApplication;

		var documentTypeList = application.Core.DocumentTypes.Select(
				v => new DocumentTypeModel
				{
					DocumentTypeGroup = v.DocumentTypeGroup.Name,
					DocumentTypeGroupId = v.DocumentTypeGroup.ID,
					DocumentTypeId = v.ID,
					DocumentTypeName = v.Name
				})
			.ToList();

		if (documentTypeList.Count == 0)
			return NotFound();

		return Ok(documentTypeList);
	}
}