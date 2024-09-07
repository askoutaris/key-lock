using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KeyLock
{
	public interface ILockFactory<TKey> where TKey : notnull
	{
		/// <summary>
		/// Try to aquire a lock. Will fail immidiately if a lock for a specific key is held by someone else
		/// </summary>
		/// <param name="keys">Keys to try get a lock for each of them</param>
		/// <returns>An IDisposable instance which must be disposed when lock is not needed any more in order to release it </returns>
		Task<IDisposable> AquireLocks(IReadOnlyCollection<TKey> keys);

		/// <summary>
		/// Try to aquire a lock. It will wait and try to aquire the lock till timeout is reached
		/// </summary>
		/// <param name="keys">Keys to try get a lock for each of them</param>
		/// <param name="timeout"></param>
		/// <returns>An IDisposable instance which must be disposed when lock is not needed any more in order to release it</returns>
		Task<IDisposable> AquireLocks(IReadOnlyCollection<TKey> keys, TimeSpan timeout);
	}
}
