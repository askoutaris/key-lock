using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KeyLock.InMemory
{
	public class SemaphoreLockFactory<TKey> : ILockFactory<TKey> where TKey : notnull
	{
		private readonly ConcurrentDictionary<TKey, SemaphoreSlim> _locks;

		public SemaphoreLockFactory()
		{
			_locks = [];
		}

		public Task<IDisposable> AquireLocks(IReadOnlyCollection<TKey> keys)
			=> AquireLocks(keys, TimeSpan.Zero);

		public async Task<IDisposable> AquireLocks(IReadOnlyCollection<TKey> keys, TimeSpan timeout)
		{
			var orderedKeys = keys
				.Distinct()
				.OrderBy(x => x)
				.ToArray();

			var newLock = new SemaphoreSlim(0);

			foreach (TKey key in orderedKeys)
			{
				while (true)
				{
					var activeLock = _locks.GetOrAdd(key, newLock);

					if (ReferenceEquals(newLock, activeLock))
						break;

					if (timeout == TimeSpan.Zero)
						await activeLock.WaitAsync(1); // pass 1 as millisecondTimeout so that lock will fail immidiately if is held by someone else
					else
						await activeLock.WaitAsync(timeout);
				}
			}

			return new Lock(() => Release(keys));
		}

		private void Release(IReadOnlyCollection<TKey> keys)
		{
			SemaphoreSlim? @lock = null;

			foreach (TKey key in keys)
				if (_locks.TryRemove(key, out var keySync))
					@lock = keySync;

			@lock?.Release(int.MaxValue);
		}
	}
}
