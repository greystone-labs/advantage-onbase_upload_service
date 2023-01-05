using Hyland.Unity;

namespace Greystone.OnbaseUploadService.Services.Keywords;

public interface IKeywordService
{
	Keyword CreateKeyword(KeywordType keywordType, string stringKeywordValue);
}