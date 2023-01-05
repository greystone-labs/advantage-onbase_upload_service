namespace Greystone.OnbaseUploadService.Services.Locking
{
	public class FileLock : IDisposable
	{
		private readonly Action _disposer;

		public FileLock(Action disposer)
		{
			_disposer = disposer;
		}

		public void Dispose()
			=> _disposer();
	}
}
