using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace KeyLock.MSSQL
{
	public class MSSQLLockFactory<TKey> : ILockFactory<TKey> where TKey : notnull
	{
		private readonly ConcurrentDictionary<string, LockedKey> _lockedKeys;
		private readonly string _connectionString;
		private readonly string _tableName;
		private readonly string _keyPrefix;
		private readonly TimeSpan _expiry;
		private readonly TimeSpan _retry;
		private readonly PeriodicWork _extendLocksJob;

		public MSSQLLockFactory(string connectionString, string tableName, string keyPrefix, TimeSpan expiry, TimeSpan retry, ILogger? logger, CancellationToken cancellationToken)
		{
			_connectionString = connectionString;
			_tableName = tableName;
			_keyPrefix = keyPrefix;
			_expiry = expiry;
			_retry = retry;
			_lockedKeys = [];
			_extendLocksJob = new PeriodicWork(OnExtendLocks, expiry / 5, logger);
			_extendLocksJob.Start(cancellationToken);
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

			var lockedKeys = new List<string>(orderedKeys.Length);

			var lockProvider = GetLockProvider();

			try
			{
				foreach (var key in orderedKeys)
				{
					await lockProvider.CreateLock(key, _expiry, timeout, _retry);

					_lockedKeys.TryAdd(key, new LockedKey(key, DateTime.UtcNow));

					lockedKeys.Add(key);
				}
			}
			catch (Exception)
			{
				Release(lockedKeys);

				throw;
			}

			return new Lock(() => Release(lockedKeys));
		}

		private void Release(IReadOnlyCollection<string> lockedKeys)
		{
			foreach (var key in lockedKeys)
				_lockedKeys.TryRemove(key, out var _);

			var lockProvider = GetLockProvider();

			lockProvider.ReleaseLocks(lockedKeys);
		}

		private async Task OnExtendLocks()
		{
			var lockedKeys = _lockedKeys.ToArray();

			var halfExpiry = _expiry / 2;

			var extendKeys = lockedKeys
				.Select(x => x.Value)
				.Where(x => x.AquiredAt + halfExpiry < DateTime.UtcNow)
				.ToArray();

			if (extendKeys.Length == 0)
				return;

			var lockProvider = GetLockProvider();

			var keys = extendKeys
				.Select(x => x.Key)
				.ToArray();

			var now = DateTime.UtcNow;

			await lockProvider.Extend(keys, _expiry);

			foreach (var key in extendKeys)
				key.SetAquiredAt(now);
		}

		private SqlLockProvider GetLockProvider()
		{
			var connection = new SqlConnection(_connectionString);

			connection.Open();

			return new SqlLockProvider(_tableName, connection);
		}

		private string GetKey(TKey key)
			=> $"{_keyPrefix}.{key}";

		class LockedKey
		{
			public string Key { get; }
			public DateTime AquiredAt { get; private set; }

			public LockedKey(string key, DateTime aquiredAt)
			{
				Key = key;
				AquiredAt = aquiredAt;
			}

			public void SetAquiredAt(DateTime value)
				=> AquiredAt = value;
		}
	}
}
