namespace Greystone.OnbaseUploadService.Models.Dto.Upload;

public class ChunkedUploadResponseModel
{
    public required int ChunkSize { get; set; }

    public required int ChunkCount { get; set; }
}