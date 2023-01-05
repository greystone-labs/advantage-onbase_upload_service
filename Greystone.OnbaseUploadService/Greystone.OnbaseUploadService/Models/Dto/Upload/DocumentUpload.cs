namespace Greystone.OnbaseUploadService.Models.Dto.Upload;

public class DocumentUpload
{
    public int Index { get; set; }

    public IFormFile File { get; set; } = null!;
}