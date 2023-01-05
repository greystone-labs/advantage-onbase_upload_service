using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Greystone.OnbaseUploadService.Database.Models;

public class UploadTask
{
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public Guid Id { get; set; }
	
	public int FileCount { get; set; }

	public string JsonDocumentIndex { get; set; } = string.Empty;
}