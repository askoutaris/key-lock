using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RedLockNet;
using StackExchange.Redis;

namespace KeyLock.Redis
{
	public class RedisLockFactory<TKey> : ILockFactory<TKey> where TKey : notnull
	{
		private readonly IDistributedLockFactory _lockFactory;
		private readonly string _keyPrefix;
		private readonly TimeSpan _expiry;
		private readonly TimeSpan _wait;
		private readonly TimeSpan _retry;

		/// <summary>
		/// RedisLockFactory constructor
		/// </summary>
		/// <param name="lockFactory">Lock factory as provided by RedLockNet</param>
		/// <param name="keyPrefix">A prefix for each key to be used as a discriminator in redis key</param>
		/// <param name="expiry">See RedLockNet.IDistributedLockFactory documentation</param>
		/// <param name="retry">See RedLockNet.IDistributedLockFactory documentation</param>
		public RedisLockFactory(IDistributedLockFactory lockFactory, string keyPrefix, TimeSpan expiry, TimeSpan retry)
		{
			_lockFactory = lockFactory;
			_keyPrefix = keyPrefix;
			_expiry = expiry;
			_retry = retry;
		}

		public Task<IDisposable> AquireLocks(IReadOnlyCollection<TKey> keys)
			=> AquireLocks(keys, TimeSpan.Zero);

		public async Task<IDisposable> AquireLocks(IReadOnlyCollection<TKey> keys, TimeSpan timeout)
		{
			var orderedKeys = keys
				.Distinct()
				.OrderBy(x => x)
				.Select(GetKey)
				.ToArray();

			var locks = new List<IRedLock>(orderedKeys.Length);

			try
			{
				foreach (var key in orderedKeys)
				{
					var redLock = timeout == TimeSpan.Zero
						? await _lockFactory.CreateLockAsync(key, _expiry)
						: await _lockFactory.CreateLockAsync(key, _expiry, timeout, _retry);

					if (redLock.IsAcquired)
						locks.Add(redLock);
					else
						throw new TimeoutException($"Lock {key} not aquired with status: {redLock.Status} , InstanceSummary: {redLock.InstanceSummary}");
				}
			}
			catch (Exception)
			{
				Release(locks);

				throw;
			}

			return new Lock(() => Release(locks));
		}

		private void Release(IReadOnlyCollection<IRedLock> locks)
		{
			foreach (var @lock in locks)
				@lock.Dispose();
		}

		private RedisKey GetKey(TKey key)
			=> $"{_keyPrefix}.{key}";
	}
}
