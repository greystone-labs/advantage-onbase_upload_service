using System.Text.Json;

using Greystone.OnbaseUploadService.Attributes;
using Greystone.OnbaseUploadService.Database;
using Greystone.OnbaseUploadService.Database.Models;
using Greystone.OnbaseUploadService.Models.Dto.Upload;
using Greystone.OnbaseUploadService.Services.Keywords;
using Greystone.OnbaseUploadService.Services.Locking;
using Greystone.OnbaseUploadService.Services.SessionManagement;

using Hyland.Unity;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Greystone.OnbaseUploadService.Controllers;

// "fef18a25-2acd-4f30-aa30-130de81cc317"

[ApiKeyAuthentication]
[ApiController]
[Route("v1/[controller]")]
public class UploadController : ControllerBase
{
	const int ChunkSize = 1_000_000;


	private readonly IUnitySessionService _unitySessionService;
	private readonly IFileLockService _fileLockService;
	private readonly IKeywordService _keywordService;
	private readonly OnbaseUploadServiceDbContext _dbContext;
	private readonly IConfiguration _configuration;
	private readonly ILogger<UploadController> _logger;

	public UploadController(
		IUnitySessionService unitySessionService,
		IFileLockService fileLockService,
		IKeywordService keywordService,
		OnbaseUploadServiceDbContext dbContext,
		IConfiguration configuration,
		ILogger<UploadController> logger)
	{
		_unitySessionService = unitySessionService;
		_fileLockService = fileLockService;
		_keywordService = keywordService;
		_dbContext = dbContext;
		_configuration = configuration;
		_logger = logger;
	}

	/// <summary>
	/// Create a new upload job
	/// </summary>
	/// <remarks>
	/// This method starts a new OnBase API upload job. After specifying a document type name
	///
	/// Additionally, this method will check to see if the user has the access required to insert
	/// a document into this document type
	///
	/// The FileCount parameter is critical here. The File Count is the number of files which will comprise
	/// an OB Document. If at the commit time the number of files indicated in the upload job is not the same
	/// as specified in this request, the upload will fail until the count matches.
	/// 
	/// </remarks>
	/// <param name="documentUploadIndex">the properties that should be associated with this document</param>
	/// <returns>an id that will be used to continue the upload</returns>
	/// <response code="200">an upload job is created for this document</response>
	/// <response code="403">the provided API key does not match the one configured on the server</response>
	/// <response code="404">the requested document type could not be found in OnBase</response>
	/// <response code="503">the configured OnBase user does not have access to create a document of the specified type</response>
	[HttpPost]
	[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Guid))]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
	public async Task<IActionResult> CreateUploadTask(DocumentUploadIndex documentUploadIndex)
	{
		using var rentedSession = _unitySessionService.RentConnection();
		var application = rentedSession.UnityApplication;

		var documentType = application.Core.DocumentTypes.Find(documentUploadIndex.DocumentTypeName);
		if (documentType is null)
			return NotFound("The given document type could not be found in OnBase");

		if (!documentType.CanI(DocumentTypePrivileges.DocumentCreation))
			return Problem(
				"The configured user does not have access to create a document of this type",
				statusCode: 503);

		var id = Guid.NewGuid();

		var uploadTask = new UploadTask
		{
			Id = id,
			FileCount = documentUploadIndex.FileCount,
			JsonDocumentIndex = JsonSerializer.Serialize(documentUploadIndex)
		};

		_dbContext.Add(uploadTask);
		await _dbContext.SaveChangesAsync();

		return Ok(id);
	}

	/// <summary>
	/// Upload a file for an upload job. Index is 0-based
	/// </summary>
	/// <remarks>
	/// This method appends a file to a provided upload job in a specified index.
	/// </remarks>
	/// <param name="uploadId">The id of the upload job provided by /create which this document will be associated with</param>
	/// <param name="index">the index (location) where the provided file should be placed in the created document</param>
	/// <param name="file">the file that will be appended to the document at the given position</param>
	/// <returns>status code 200 if successful, or erroneous status code if failed</returns>
	/// <response code="200">the given file was appended to the document</response>
	/// <response code="400">the provided index is out of valid range or a non-image mime type was uploaded</response>
	/// <response code="403">there is no API key provided or the API key does not match one configured on the server</response>
	/// <response code="404">the provided upload id does not exist</response>
	/// <response code="500">an unknown error has prevented the document from being appended</response>
	[HttpPut("{uploadId:guid}/files/{index:int}")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> UploadFile(
		[FromRoute] Guid uploadId,
		[FromRoute] int index,
		[FromForm] IFormFile file)
	{
		var uploadTask = await _dbContext.UploadTasks.FindAsync(uploadId);
		if (uploadTask is null)
			return NotFound();

		if (index < 0 || index >= uploadTask.FileCount)
			return Problem("Index out of range", statusCode: 400);

		if (string.IsNullOrWhiteSpace(file.ContentType))
			return BadRequest("File missing content type header");

		if (uploadTask.FileCount > 1 && !IsImageMimeType(file.ContentType))
			return BadRequest("Only multiple image file types can be appended");

		var uploadFile =
			_dbContext.UploadFiles.FirstOrDefault(v => v.Index == index && v.UploadTaskId == uploadId);

		var filePath = SynthesizeResourcePath(uploadId, index, file.FileName);
		if (filePath is null)
			return Problem("Failed to generate resource path", statusCode: 500);

		if (uploadFile is null)
		{
			uploadFile = new UploadFile
			{
				Chunked = false,
				FileName = filePath,
				ContentType = file.ContentType,
				Index = index,
				UploadTask = uploadTask
			};
			_dbContext.UploadFiles.Add(uploadFile);
		}
		else
		{
			if (!RemoveResource(uploadFile.FileName))
				return Problem("File could not be removed", statusCode: 500);
			uploadFile.Chunked = false;
			uploadFile.FileName = filePath;
			uploadFile.Index = index;
		}

		await _dbContext.SaveChangesAsync();

		await using var readStream = file.OpenReadStream();
		if (!await UploadResourceAsync(filePath, readStream))
			return Problem("resource could not be written to disk", statusCode: 500);

		return NoContent();
	}

	/// <summary>
	/// starts a file upload using the chunked upload protocol.
	/// </summary>
	/// <remarks>
	/// In the request the user should indicate the size of the file and the file name, the api
	/// will then respond with the number of chunks required and the size that each chunk should be
	/// </remarks>
	/// <param name="uploadId">The id of the upload job provided by /create which this document will be associated with</param>
	/// <param name="index">the index (location) where the provided file should be placed in the created document</param>
	/// <param name="chunkedRequest">metadata about the document that will be uploaded</param>
	/// <response code="200">the given file was appended to the document</response>
	/// <response code="400">the provided index is out of valid range or a non image file was uploaded</response>
	/// <response code="403">there is no API key provided or the API key does not match one configured on the server</response>
	/// <response code="404">the provided upload id does not exist</response>
	/// <response code="500">an unknown error has prevented the document from being appended</response>
	[HttpPost("{uploadId:guid}/files/{index:int}/chunk")]
	[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ChunkedUploadResponseModel))]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> UploadFile(
		[FromRoute] Guid uploadId,
		[FromRoute] int index,
		[FromBody] ChunkedUploadRequestModel chunkedRequest)
	{
		var uploadTask = await _dbContext.UploadTasks.FindAsync(uploadId);
		if (uploadTask is null)
			return NotFound();

		if (index < 0 || index >= uploadTask.FileCount)
			return Problem("Index out of range", statusCode: 403);

		if (uploadTask.FileCount > 1 && !IsImageMimeType(chunkedRequest.ContentType))
			return BadRequest("Only multiple image file types can be appended");

		var uploadFile =
			_dbContext.UploadFiles.FirstOrDefault(v => v.Id == index && v.UploadTaskId == uploadId);

		var filePath = SynthesizeResourcePath(uploadId, index, chunkedRequest.FileName);
		if (filePath is null)
			return Problem("Failed to generate resource path", statusCode: 500);

		if (uploadFile is null)
		{
			uploadFile = new UploadFile
			{
				Chunked = true,
				FileName = filePath,
				ContentType = chunkedRequest.ContentType,
				Index = index,
				UploadTask = uploadTask
			};
			_dbContext.UploadFiles.Add(uploadFile);
		}
		else
		{
			if (!RemoveResource(uploadFile.FileName))
				return Problem("File already existed and could not be deleted", statusCode: 500);
			uploadFile.Chunked = true;
			uploadFile.FileName = filePath;
			uploadFile.ContentType = chunkedRequest.ContentType;
		}

		await _dbContext.SaveChangesAsync();

		if (!await CreateChunkedFileAsync(filePath, chunkedRequest.FileBytes))
			return Problem("Failed to create chunked file", statusCode: 500);


		var requiredChunkCount = (int)Math.Ceiling((double)chunkedRequest.FileBytes / ChunkSize);

		return Ok(
			new ChunkedUploadResponseModel { ChunkCount = requiredChunkCount, ChunkSize = ChunkSize });
	}

	/// <summary>
	/// place a chunk of the designated file index on the server
	/// </summary>
	/// <remarks>
	/// this endpoint places a chunk of the file designated by the index parameter on the server.
	/// The chunk will be added to the final file in the order of the chunk index parameter
	/// </remarks>
	/// <remarks>
	/// for the chunk parameter, the file name and mime type should match the file name and mime type
	/// that was originally sent to the server
	/// </remarks>
	/// <param name="uploadId">The id of the upload job provided by /create which this document will be associated with</param>
	/// <param name="index">the index (location) where the provided file should be placed in the created document</param>
	/// <param name="chunkIndex">the index (location) where this particular chunk data should appear in the final file</param>
	/// <param name="chunk">the data that will be added to the final file</param>
	/// <returns></returns>
	[HttpPut("{uploadId:guid}/files/{index:int}/chunk/{chunkIndex:int}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> UploadDocumentChunk(
		[FromRoute] Guid uploadId,
		[FromRoute] int index,
		[FromRoute] int chunkIndex,
		[FromForm] IFormFile chunk)
	{
		var uploadFile =
			await _dbContext.UploadFiles.FirstOrDefaultAsync(
				v => v.UploadTaskId == uploadId && v.Index == index);

		if (uploadFile is null)
			return NotFound();

		if (!uploadFile.Chunked)
			return Problem("Failed to upload document", statusCode: 400);

		var filePath = SynthesizeResourcePath(uploadId, index, chunk.FileName);
		if (filePath is null)
			return Problem("Failed to generate resource path", statusCode: 500);

		await using var chunkData = chunk.OpenReadStream();
		if (!await UploadResourceAsync(filePath, chunkIndex, chunkData))
			return Problem("Failed to upload chunk", statusCode: 500);

		return NoContent();
	}

	/// <summary>
	/// Commit an upload ID to OnBase and create a document
	/// </summary>
	/// <remarks>
	/// When creating a document in OnBase, workflow will not be started on the document
	/// </remarks>
	/// <param name="uploadId">The id of the upload job that will be committed to OnBase</param>
	/// <returns></returns>
	/// <response code="200">The document was created in OnBase and the job has been deleted</response>
	/// <response code="404">A job with the given upload id could not be found</response>
	/// <response code="400">
	/// a known error has occurred during the upload, reasons are provided. This could be due to
	/// keyword formatting or permissions.
	/// </response>
	/// <response code="500">An unexpected error has occurred while uploading the document to OnBase</response>
	[HttpPost("{uploadId:guid}/commit")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public async Task<IActionResult> Commit(Guid uploadId)
	{
		using var rentedConnection = _unitySessionService.RentConnection();
		var connection = rentedConnection.UnityApplication;

		var upload = await _dbContext.UploadTasks.FindAsync(uploadId);
		if (upload is null)
			return NotFound();

		var uploadIndex = JsonSerializer.Deserialize<DocumentUploadIndex>(upload.JsonDocumentIndex);
		if (uploadIndex is null)
			return Problem("Failed to parse upload index", statusCode: 500);

		var documentType = connection.Core.DocumentTypes.Find(uploadIndex.DocumentTypeName);
		if (documentType is null)
			return NotFound("Document type not found");

		var pageDataCollection = new List<PageData>();
		for (int i = 0; i < upload.FileCount; i++)
		{
			var result =
				await _dbContext.UploadFiles.FirstOrDefaultAsync(
					v => v.UploadTaskId == uploadId && v.Index == i);

			if (result is null)
				return BadRequest("not all files required for upload are present");

			var pageData = connection.Core.Storage.CreatePageData(
				System.IO.File.OpenRead(result.FileName),
				Path.GetExtension(result.FileName));

			pageDataCollection.Add(pageData);
		}

		var fileType = pageDataCollection.Count > 1
			? connection.Core.FileTypes.Find("Image File Format")
			: connection.Core.FileTypes.Find(GetOnBaseFileTypeNumber(pageDataCollection[0].Extension));

		if (fileType is null)
			return Problem("Unable to resolve file type", statusCode: 500);

		var properties = connection.Core.Storage.CreateStoreNewDocumentProperties(
			documentType,
			fileType);

		foreach (var (key, value) in uploadIndex.Keywords)
		{
			var keywordType = documentType.KeywordRecordTypes.FindKeywordType(key);
			if (keywordType is null)
			{
				_logger.LogWarning("attempt to add keyword to document that is not present");
				continue;
			}

			if (value is JsonElement jsonValue)
			{
				if (jsonValue.ValueKind == JsonValueKind.String)
				{
					var stringValue = jsonValue.GetString();
					if (stringValue is null)
						continue;

					var newKeyword = _keywordService.CreateKeyword(keywordType, stringValue);
					properties.AddKeyword(newKeyword);
				}
				else if (jsonValue.ValueKind == JsonValueKind.Array)
				{
					var stringArrayValue = jsonValue.EnumerateArray();
					foreach (var arrayValue in stringArrayValue)
					{
						if (arrayValue.ValueKind != JsonValueKind.String)
							return Problem("Invalid data type - " + key, statusCode: 500);

						var stringValue = arrayValue.GetString();
						if (stringValue is null)
							continue;

						var newKeyword = _keywordService.CreateKeyword(keywordType, stringValue);
						properties.AddKeyword(newKeyword);
					}
				}
				else
				{
					return Problem("Invalid data type", statusCode: 500);
				}
			}
		}

		var newDocument = connection.Core.Storage.StoreNewDocument(pageDataCollection, properties);
		
		foreach (var pageData in pageDataCollection)
			pageData.Dispose();

		return Ok(newDocument.ID);
	}

	private bool IsImageMimeType(string mimeType)
	{
		mimeType = mimeType.ToLower();

		return mimeType switch
		{
			"image/bmp" => true,
			"image/jpeg" => true,
			"image/png" => true,
			"image/tiff" => true,
			_ => false,
		};
	}


	/// <summary>
	/// Delete an upload job
	/// </summary>
	/// <param name="uploadId">the id of the upload that will be deleted</param>
	/// <returns></returns>
	/// <response code="200">The upload job was successfully deleted</response>
	/// <response code="403">No provided API key or the API key did not match one configured</response>
	/// <response code="404">The given upload job was not found</response>
	/// <response code="500">An unexpected error has occurred while deleting the upload job</response>
	[HttpDelete("{uploadId:guid}")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public IActionResult Delete(Guid uploadId)
	{
		return Ok();
	}

	private string? SynthesizeResourcePath(Guid uploadId, int index, string fileName)
	{
		var workingDirectory = _configuration["WorkingDirectory"];
		if (workingDirectory is null)
		{
			_logger.LogError(
				"Failed to synthesize resource path, working directory not configured in appsettings.json");
			return null;
		}

		var outputPath = Path.Combine(workingDirectory, uploadId.ToString(), $"{index}_{fileName}");

		var directory = Path.GetDirectoryName(outputPath);
		if (directory is not null)
			Directory.CreateDirectory(directory);

		return outputPath;
	}


	private async Task<bool> CreateChunkedFileAsync(string resourcePath, long size)
	{
		try
		{
			using var fileLock = await _fileLockService.AcquireLock(resourcePath);
			await using var fileStream = new FileStream(
				resourcePath,
				FileMode.CreateNew,
				FileAccess.Write,
				FileShare.None);

			fileStream.SetLength(size);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to create chunked file");
			return false;
		}

		return true;
	}

	// for chunks
	private async Task<bool> UploadResourceAsync(string resourcePath, int chunkId, Stream stream)
	{
		try
		{
			using var fileLock = await _fileLockService.AcquireLock(resourcePath);
			await using var fileStream = new FileStream(
				resourcePath,
				FileMode.Open,
				FileAccess.ReadWrite,
				FileShare.None);

			fileStream.Seek(chunkId * ChunkSize, SeekOrigin.Begin);
			await stream.CopyToAsync(fileStream);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to append to chunked file");
			return false;
		}

		return true;
	}

	// for single files
	private async Task<bool> UploadResourceAsync(string resourcePath, Stream file)
	{
		try
		{
			using var fileLock = await _fileLockService.AcquireLock(resourcePath);
			await using var diskFile = new FileStream(
				resourcePath,
				FileMode.Create,
				FileAccess.ReadWrite,
				FileShare.None);

			await file.CopyToAsync(diskFile);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to write standalone file");
			return false;
		}

		return true;
	}

	private bool RemoveResource(string resourcePath)
	{
		try
		{
			if (System.IO.File.Exists(resourcePath))
				System.IO.File.Delete(resourcePath);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to delete file wth resource path '{0}'", resourcePath);
			return false;
		}
	}

	private int GetOnBaseFileTypeNumber(string fileExtension)
	{
		int ret = -1;
		int unknownFileTypeNum = 2;
		fileExtension = fileExtension.Replace(".", "");
		switch (fileExtension.ToLower())
		{
			case "txt":
			case "rda": //Need to do more research on this, how does NEEE use these?
				//case "notes": //double check to see if notes are stored as text
				ret = 1;
				break;
			case "bmp":
			case "gif":
			case "jpg":
			case "jpeg":
			case "tif":
			case "tiff":
			case "ico":
			case "png":
				ret = 2;
				break;
			case "doc":
			case "docx":
				ret = 12;
				break;
			case "xls":
			case "xlsx":
			case "xlsm":
			case "xlsb": //What is this?
			case "csv": //May want txt instead
				ret = 13;
				break;
			case "ppt":
			case "pptx":
				ret = 14;
				break;
			case "rtf":
				ret = 15;
				break;
			case "pdf":
				ret = 16;
				break;
			case "htm":
			case "html":
			case "mht": //May need to double check these
				ret = 17;
				break;
			case "avi":
				ret = 18;
				break;
			case "wav":
				ret = 20;
				break;
			case "pcl":
				ret = 21;
				break;
			case "xml":
				ret = 32;
				break;
			case "msg":
				ret = 35;
				break;
			case "eml":
				//case "vcf": //vCard info, does this belong here?  Sending to catch-all for now
				ret = 63;
				break;
			case "rar":
			case "7z":
			case "bin": //Maybe?
			case "zip":
				ret = 70;
				break;
			//case "xps":
			//  ret = 102; //Assumedly 102, based on creation of this file format in OB
			//break;
		}

		if (ret == -1)
		{
			return unknownFileTypeNum;
		}

		return ret;
	}
}