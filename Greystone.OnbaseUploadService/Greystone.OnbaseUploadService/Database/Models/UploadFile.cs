using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Greystone.OnbaseUploadService.Database.Models;

public class UploadFile
{
	[Key]
	[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
	public int Id { get; set; }
	
	public UploadTask UploadTask { get; set; }
	
	public Guid UploadTaskId { get; set; }
	
	public required int Index { get; set; }
	
	public required string FileName { get; set; }
	
	public required string ContentType { get; set; }
	
	public required bool Chunked { get; set; }
}