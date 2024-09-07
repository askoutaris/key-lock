using KeyLock;
using KeyLock.InMemory;
using KeyLock.MSSQL;
using KeyLock.Redis;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;

namespace Workbench
{
	internal class Program
	{
		static void Main(string[] args)
		{
			ILockFactory<int> lockFactory = GetLockFactoryInMemory();

			_ = Task.Run(async () =>
			{
				Console.WriteLine("Waiting 1");

				using var @lock = await lockFactory.AquireLocks([1]);

				Console.WriteLine("Working 1");

				await Task.Delay(20000);

				Console.WriteLine("Done 1");
			});

			Console.ReadLine();

			_ = Task.Run(async () =>
			{
				Console.WriteLine("Waiting 2");

				using var @lock = await lockFactory.AquireLocks([1], TimeSpan.FromMinutes(1));

				Console.WriteLine("Done 2");
			});

			Console.ReadLine();
		}

		private static ILockFactory<int> GetLockFactoryRedis()
		{
			var multiplexers = new RedLockMultiplexer[] {
				ConnectionMultiplexer.Connect("{your_redis_connection_string}")
			};

			var lockFactory = RedLockFactory.Create(multiplexers);

			return new RedisLockFactory<int>(
				lockFactory: lockFactory,
				keyPrefix: "Lock",
				expiry: TimeSpan.FromSeconds(10),
				retry: TimeSpan.FromSeconds(1));
		}

		private static ILockFactory<int> GetLockFactoryMSSQL()
		{
			return new MSSQLLockFactory<int>(
				connectionString: "{your_connection_string}",
				tableName: "DistributedLocks",
				keyPrefix: "Lock",
				expiry: TimeSpan.FromSeconds(10),
				retry: TimeSpan.FromSeconds(1),
				logger: null,
				cancellationToken: CancellationToken.None);
		}

		private static ILockFactory<int> GetLockFactoryInMemory()
		{
			return new SemaphoreLockFactory<int>();
		}
	}
}
