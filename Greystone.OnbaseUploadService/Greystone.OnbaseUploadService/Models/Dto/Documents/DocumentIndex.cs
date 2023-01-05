namespace Greystone.OnbaseUploadService.Models.Dto.Documents;

public class DocumentIndex
{
    public string DocumentTypeName { get; set; } = string.Empty;

    /// <summary>
    /// this field can either be a string, or a list of strings
    ///
    /// string | string[]
    /// </summary>
    public KeywordCollection Keywords { get; set; } = new();
}