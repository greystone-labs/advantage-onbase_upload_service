namespace Greystone.OnbaseUploadService.Models.Dto.Upload;

public class ChunkedUploadRequestModel
{
    public long FileBytes { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
}