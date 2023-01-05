using Greystone.OnbaseUploadService.Models.Dto.Documents;

namespace Greystone.OnbaseUploadService.Models.Dto.Upload;

public class DocumentUploadIndex : DocumentIndex
{
    /// <summary>
    /// The number of files which will comprise the document in OnBase
    /// </summary>
    public int FileCount { get; set; }
}