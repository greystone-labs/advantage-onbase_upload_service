using Hyland.Unity;

namespace Greystone.OnbaseUploadService.Services.Keywords;

public class KeywordService : IKeywordService
{
	public Keyword CreateKeyword(KeywordType keywordType, string stringKeywordValue)
	{
		var keywordDataType = keywordType.DataType;

		switch (keywordDataType)
		{
			case KeywordDataType.Undefined:
				throw new InvalidOperationException("cannot assign to an undefined keyword");
			case KeywordDataType.Numeric9:
				if (!int.TryParse(stringKeywordValue, out var numeric9Value))
					throw new Exception("cannot parse correct keyword type int");
				return keywordType.CreateKeyword(numeric9Value);
			case KeywordDataType.Numeric20:
				if (!long.TryParse(stringKeywordValue, out var numeric20Value))
					throw new Exception("cannot parse correct keyword type long");
				return keywordType.CreateKeyword(numeric20Value);
			case KeywordDataType.AlphaNumeric:
				return keywordType.CreateKeyword(stringKeywordValue);
			case KeywordDataType.Currency:
				if (!decimal.TryParse(stringKeywordValue, out var decimalValue))
					throw new Exception("cannot parse correct keyword type date");
				return keywordType.CreateKeyword(decimalValue);
			case KeywordDataType.Date:
				if (!DateOnly.TryParse(stringKeywordValue, out var dateValue))
					throw new Exception("cannot parse correct keyword type date");
				return keywordType.CreateKeyword(dateValue.ToDateTime(TimeOnly.MinValue));
			case KeywordDataType.DateTime:
				if (!DateTime.TryParse(stringKeywordValue, out var dateTimeValue))
					throw new Exception("cannot parse correct keyword type date");
				return keywordType.CreateKeyword(dateTimeValue);
			case KeywordDataType.FloatingPoint:
				if (!double.TryParse(stringKeywordValue, out var floatingPointValue))
					throw new Exception("cannot parse correct keyword type floating point");
				return keywordType.CreateKeyword(floatingPointValue);
			default:
				throw new ArgumentOutOfRangeException();
		}
	}
}