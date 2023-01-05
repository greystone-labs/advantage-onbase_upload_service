using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Greystone.OnbaseUploadService.Attributes;
using Greystone.OnbaseUploadService.Models.Dto.Documents;
using Greystone.OnbaseUploadService.Services.Keywords;
using Greystone.OnbaseUploadService.Services.SessionManagement;

using Hyland.Unity;

using Microsoft.AspNetCore.Mvc;

namespace Greystone.OnbaseUploadService.Controllers;

// "fef18a25-2acd-4f30-aa30-130de81cc317"

[ApiController]
[ApiKeyAuthentication]
[Route("v1/[controller]")]
public class DocumentsController : Controller
{
	private readonly IUnitySessionService _unitySessionService;
	private readonly IKeywordService _keywordService;
	private readonly ILogger<DocumentsController> _logger;

	public DocumentsController(
		IUnitySessionService unitySessionService,
		IKeywordService keywordService,
		ILogger<DocumentsController> logger)
	{
		_unitySessionService = unitySessionService;
		_keywordService = keywordService;
		_logger = logger;
	}

	/// <summary>
	/// Compute a base64 encoded MD5 hash from the combination of the documents latest revision Id and all of the keywords
	/// included in the <paramref name="keywordCollection"/> query string parameter
	/// </summary>
	/// <param name="documentId">The OnBase document id of the document for which the last modified date is being requested</param>
	/// <param name="keywordCollection">the collection of keywords that will be included in the hash</param>
	/// <returns>the last modified date of the document</returns>
	/// <response code="200">Returns the hash of the latest revision ID with all of the keywords included</response>
	/// <response code="403">If the API key does not match configured</response>
	/// <response code="404">If the document with the provided <paramref name="documentId"/> is not present in OnBase</response>
	[HttpGet("{documentId:long}/hash", Name = "Get Document Last Modified Hash")]
	[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
	public IActionResult GetDocumentLastModified(
		long documentId,
		[FromQuery] string[] keywordCollection)
	{
		using var connection = _unitySessionService.RentConnection();

		var document = connection.UnityApplication.Core.GetDocumentByID(
			documentId,
			DocumentRetrievalOptions.LoadKeywords);

		if (document == null)
			return NotFound();

		var stringToHash = document.LatestRevision.ID.ToString(CultureInfo.InvariantCulture);

		foreach (KeywordRecord keywordRecord in document.KeywordRecords)
		{
			foreach (var keyword in keywordRecord.Keywords)
			{
				var keywordName = keyword.KeywordType.Name;

				if (keywordCollection.Any(
					    keywordCollectionValue => keywordName.Equals(
						    keywordCollectionValue,
						    StringComparison.InvariantCultureIgnoreCase)))
				{
					stringToHash += keyword.ToString();
				}
			}
		}


		// take a list of keywords from the query string, combine, produce md5 hash
		using var md5 = MD5.Create();
		var output = md5.ComputeHash(Encoding.UTF8.GetBytes(stringToHash));
		return Ok(Convert.ToBase64String(output));
	}

	/// <summary>
	/// Delete a document in OnBase with the given documentId
	/// </summary>
	/// <param name="documentId">The OnBase document id of the document which will be deleted</param>
	/// <returns>204 if accepted, 404 if no document, 503 if service could not authenticate</returns>
	/// <response code="204">The document was deleted successfully</response>
	/// <response code="403">If the API key does not match configured</response>
	/// <response code="404">The document with the given document id could not be found</response>
	/// <response code="503">The current OnBase user does not have the ability to delete the target document</response>
	[HttpDelete("{documentId:long}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
	public IActionResult DeleteDocument(long documentId)
	{
		using var connection = _unitySessionService.RentConnection();
		var application = connection.UnityApplication;

		var document = application.Core.GetDocumentByID(documentId);

		if (document == null)
			return NotFound();

		if (!document.DocumentType.CanI(DocumentTypePrivileges.DocumentDeletion))
			return Problem(
				"Configured user does not have the necessary privileges to delete a document",
				statusCode: StatusCodes.Status503ServiceUnavailable);

		application.Core.Storage.DeleteDocument(document);

		return NoContent();
	}

	/// <summary>
	/// Re-index the provided with the provided OnBase <paramref name="documentId"/>
	/// </summary>
	/// <remarks>
	/// For the document with the given id, attempt to re-index the document
	///
	/// This will replace all existing keywords for the given document.
	/// </remarks>
	/// <example>
	/// POST /Documents/123/Index
	/// {
	/// "documentTypeName": "Loans",
	/// "keywords": {
	///		"keywordOne": "Value"
	///		"keywordTwo": ["Value1", "Value2"]
	///		}
	/// }
	/// </example>
	/// <param name="documentId">the OnBase id of the OnBase document that will be re-indexed</param>
	/// <param name="documentIndex">the properties that will go into re-indexing the document</param>
	/// <returns></returns>
	/// <response code="204">The document was successfully re-indexed</response>
	/// <response code="403">If the API key does not match configured</response>
	/// <response code="404">The document with the given document id does not exist, or the desired document type does not exist</response>
	/// <response code="403">The configured service user does not have permission to reindex the given document</response>
	[HttpPost("{documentId:long}/index", Name = "Reindex Document")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
	public IActionResult Index(long documentId, DocumentIndex documentIndex)
	{
		using var connection = _unitySessionService.RentConnection();
		var application = connection.UnityApplication;

		var document = application.Core.GetDocumentByID(
			documentId,
			DocumentRetrievalOptions.LoadKeywords);
		if (document is null)
			return NotFound("No document exists with specified id");

		var documentType = application.Core.DocumentTypes.Find(documentIndex.DocumentTypeName);
		if (documentType is null)
			return NotFound("Desired document type could not be found in OnBase");


		EditableKeywordModifier editableKeywordModifier;
		if (documentType.ID != document.DocumentType.ID)
		{
			if (!documentType.CanI(DocumentTypePrivileges.ReindexDocument))
				return Problem(
					"Configured user does not have access to reindex this document",
					statusCode: 503);

			var reindexProps = application.Core.Storage.CreateReindexProperties(document, documentType);
			reindexProps.Options = StoreDocumentOptions.SkipWorkflow;

			editableKeywordModifier = reindexProps;
		}
		else
		{
			if (!documentType.CanI(DocumentTypePrivileges.ModifyKeywords))
				return Problem(
					"Configured user does not have access to modify keywords on this document",
					statusCode: 503);

			editableKeywordModifier = document.CreateKeywordModifier();
		}

		var obKeywordDictionary = document.KeywordRecords.ToList()
			.SelectMany(v => v.Keywords)
			.ToList()
			.GroupBy(v => v.KeywordType.Name, v => v)
			.ToDictionary(v => v.Key, v => v.ToList());

		foreach (var (obKeywordName, obKeywordValue) in obKeywordDictionary)
		{
			var keywordOccurrenceCount = 0;
			foreach (var obKeyword in obKeywordValue)
			{
				if (!documentIndex.Keywords.ContainsKey(obKeywordName)
				    || documentIndex.Keywords[obKeywordName] is null)
				{
					editableKeywordModifier.RemoveKeyword(obKeyword);
					continue;
				}

				if (documentIndex.Keywords[obKeywordName] is JsonElement jsonElement)
				{
					if (jsonElement.ValueKind == JsonValueKind.String)
					{
						var stringKeywordValue = jsonElement.GetString();
						if (stringKeywordValue is null)
							continue;

						if (keywordOccurrenceCount == 0)
						{
							editableKeywordModifier.UpdateKeyword(
								obKeyword,
								_keywordService.CreateKeyword(obKeyword.KeywordType, stringKeywordValue));
						}
						else
						{
							// if the keyword that we got was a string value and we've already seen the keyword, well, we didn't get
							// an array so we'll have to remove the extras
							editableKeywordModifier.RemoveKeyword(obKeyword);
						}
					}

					if (jsonElement.ValueKind == JsonValueKind.Array)
					{
						var stringArrayValue = jsonElement.EnumerateArray().ToList();

						if (stringArrayValue.Count > keywordOccurrenceCount)
						{
							var value = stringArrayValue[keywordOccurrenceCount].GetString();

							if (value is null)
								continue;

							var newKeyword = _keywordService.CreateKeyword(obKeyword.KeywordType, value);

							editableKeywordModifier.UpdateKeyword(obKeyword, newKeyword);
						}
						else
						{
							// we have more keywords in OnBase than we have in the post request, so we have to remove a few
							editableKeywordModifier.RemoveKeyword(obKeyword);
						}
					}
				}

				keywordOccurrenceCount++;
			}

			// if our array ended early and it exists
			if (documentIndex.Keywords.ContainsKey(obKeywordName))
			{
				if (documentIndex.Keywords[obKeywordName] is JsonElement
				    {
					    ValueKind: JsonValueKind.Array
				    } jsonElement)
				{
					var array = jsonElement.EnumerateArray().ToList();

					if (array.Count > obKeywordDictionary[obKeywordName].Count)
					{
						// there will always be at least one for this entry to exist
						var keywordType = obKeywordDictionary[obKeywordName][0].KeywordType;

						for (int i = keywordOccurrenceCount; i < array.Count; i++)
						{
							var value = array[i].ToString();
							if (value is null)
								continue;

							editableKeywordModifier.AddKeyword(_keywordService.CreateKeyword(keywordType, value));
						}
					}
				}
			}
		}

		// we have to iterate over all of our own index, if we have entries that don't
		// exist in the index we have to add them to OnBase
		foreach (var (indexKeywordName, indexKeywordValue) in documentIndex.Keywords)
		{
			if (!obKeywordDictionary.ContainsKey(indexKeywordName))
			{
				// if the keyword isn't present in the record then we have to skip and warn
				// we only have to worry about this here because we can assume that the document
				// is coming in with valid documents
				if (documentType.KeywordRecordTypes.FindKeywordType(indexKeywordName) is null)
				{
					_logger.LogWarning(
						"index keyword '{0}' was not present in document type, skipping",
						indexKeywordName);
				}

				var obKeywordType = application.Core.KeywordTypes.Find(indexKeywordName);
				if (obKeywordType == null)
					throw new Exception("keyword type not found");

				if (indexKeywordValue is JsonElement jsonElement)
				{
					if (jsonElement.ValueKind == JsonValueKind.String)
					{
						var stringValue = jsonElement.GetString();
						if (stringValue is null)
							continue;

						var newKeyword = _keywordService.CreateKeyword(obKeywordType, stringValue);
						editableKeywordModifier.AddKeyword(newKeyword);
					}

					if (jsonElement.ValueKind == JsonValueKind.Array)
					{
						var stringArrayValue = jsonElement.EnumerateArray();
						foreach (var arrayValue in stringArrayValue)
						{
							if (arrayValue.ValueKind != JsonValueKind.String)
								return Problem("Invalid json data type - " + indexKeywordValue, statusCode: 500);

							var stringValue = arrayValue.GetString();
							if (stringValue is null)
								continue;

							var newKeyword = _keywordService.CreateKeyword(obKeywordType, stringValue);
							editableKeywordModifier.AddKeyword(newKeyword);
						}
					}
				}
			}
		}

		{
			if (editableKeywordModifier is ReindexProperties reindexProps)
				application.Core.Storage.ReindexDocument(reindexProps);
			else if (editableKeywordModifier is KeywordModifier keyMod)
				keyMod.ApplyChanges();
		}

		return NoContent();
	}
}