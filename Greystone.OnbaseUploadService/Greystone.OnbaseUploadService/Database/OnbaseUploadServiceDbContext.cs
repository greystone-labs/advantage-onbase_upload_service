using Greystone.OnbaseUploadService.Database.Models;

using Microsoft.EntityFrameworkCore;

namespace Greystone.OnbaseUploadService.Database;

public sealed class OnbaseUploadServiceDbContext : DbContext
{
	public DbSet<UploadTask> UploadTasks { get; set; } = null!;

	public DbSet<UploadFile> UploadFiles { get; set; } = null!;

	public OnbaseUploadServiceDbContext(DbContextOptions<OnbaseUploadServiceDbContext> options) :
		base(options)
	{
	}
}