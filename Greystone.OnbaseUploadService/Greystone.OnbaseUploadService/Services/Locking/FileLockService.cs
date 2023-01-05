namespace Greystone.OnbaseUploadService.Services.Locking;

public class FileLockService : IFileLockService
{
	private record SemaphoreMonitor(SemaphoreSlim Semaphore, int QueueSize);

	private readonly Dictionary<string, SemaphoreMonitor> _fileLockDictionary = new();

	public async Task<FileLock> AcquireLock(string fileName)
	{
		SemaphoreMonitor semaphoreMonitor;
		
		lock (_fileLockDictionary)
		{
			if (_fileLockDictionary.ContainsKey(fileName))
			{
				var existingItem = _fileLockDictionary[fileName];
				_fileLockDictionary[fileName] =
					existingItem with { QueueSize = existingItem.QueueSize + 1 };
			}
			else
			{
				_fileLockDictionary.Add(fileName, new SemaphoreMonitor(new SemaphoreSlim(1, 1), 1));
			}

			semaphoreMonitor = _fileLockDictionary[fileName];
		}

		await semaphoreMonitor.Semaphore.WaitAsync();

		var fileLock = new FileLock(() => Release(fileName));
		
		return fileLock;
	}

	private void Release(string key)
	{
		lock (_fileLockDictionary)
		{
			var existingTracker = _fileLockDictionary[key];
			var newTracker = _fileLockDictionary[key] with { QueueSize = existingTracker.QueueSize - 1 };
			
			existingTracker.Semaphore.Release();

			if (newTracker.QueueSize == 0)
			{
				_fileLockDictionary.Remove(key);
				newTracker.Semaphore.Dispose();
			}
			else
			{
				_fileLockDictionary[key] = newTracker;
			}
		}
	}
}