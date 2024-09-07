using System;

namespace KeyLock
{
	public class Lock : IDisposable
	{
		private bool _isDisposed = false;
		private readonly Action _onDispose;

		public Lock(Action onDispose)
		{
			_onDispose = onDispose;
		}

		public void Dispose()
		{
			DisposeInternal(false);

			GC.SuppressFinalize(this);
		}

		~Lock()
		{
			DisposeInternal(true);
		}

		private void DisposeInternal(bool finalizing)
		{
			if (!_isDisposed)
			{
				try
				{
					_onDispose();
				}
				catch (Exception)
				{
					// do not throw exception if is called by finilizer
					if (!finalizing)
						throw;
				}

				_isDisposed = true;
			}
		}
	}
}
