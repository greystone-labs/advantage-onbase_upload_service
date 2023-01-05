namespace Greystone.OnbaseUploadService.Services.Locking;

public interface IFileLockService
{
    Task<FileLock> AcquireLock(string fileName);
}