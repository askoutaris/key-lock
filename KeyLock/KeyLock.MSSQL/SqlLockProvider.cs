using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace KeyLock.MSSQL
{
	class SqlLockProvider
	{
		private readonly string _tableName;
		private readonly SqlConnection _connection;

		public SqlLockProvider(string tableName, SqlConnection connection)
		{
			_tableName = tableName;
			_connection = connection;
		}

		public async Task CreateLock(string key, TimeSpan expiry, TimeSpan wait, TimeSpan retry)
		{
			var sql = $@"
				declare @expiresAt datetime2 = DATEADD(MILLISECOND, {expiry.TotalMilliseconds}, GETDATE());
				declare @startedAt datetime2 = GETDATE();

				WHILE(1=1) BEGIN
					MERGE {_tableName} AS locks
					USING (SELECT '{key}' AS lockKey) AS source
					ON locks.LockKey = source.lockKey
					WHEN MATCHED AND locks.ExpiresAt < GETDATE()
					THEN 
						UPDATE SET ExpiresAt = @expiresAt
					WHEN NOT MATCHED
					THEN 
						INSERT (LockKey, ExpiresAt)
						VALUES ('{key}', @expiresAt);

					IF(@@ROWCOUNT = 1) BEGIN
						SELECT 1;
						BREAK;
					END

					IF(DATEDIFF(MILLISECOND, @startedAt, GETDATE()) > {wait.TotalMilliseconds}) BEGIN
						SELECT 0;
						BREAK;
					END

					WAITFOR DELAY '{retry}'
				END;
			";

			var cmd = new SqlCommand(sql, _connection);

			using var result = await cmd.ExecuteReaderAsync();

			if (!result.Read())
				throw new Exception($"Lock did not aquired {key}");

			var affectedRows = result.GetInt32(0);

			if (affectedRows == 0)
				throw new Exception($"Lock did not aquired {key}");
		}

		public void ReleaseLocks(IReadOnlyCollection<string> keys)
		{
			var keysArray = GetKeysStringArray(keys);

			var sql = $"DELETE FROM {_tableName} WHERE LockKey IN ({keysArray})";

			var cmd = new SqlCommand(sql, _connection);

			cmd.ExecuteNonQuery();
		}

		public async Task Extend(string[] keys, TimeSpan extend)
		{
			var keysArray = GetKeysStringArray(keys);

			var sql = $@"
				UPDATE {_tableName}
				SET ExpiresAt = DATEADD(MILLISECOND, {extend.TotalMilliseconds}, GETDATE())
				WHERE LockKey in ({keysArray})
			";

			var cmd = new SqlCommand(sql, _connection);

			await cmd.ExecuteNonQueryAsync();
		}

		private string GetKeysStringArray(IReadOnlyCollection<string> keys)
			=> string.Join(',', keys.Select(key => $"'{key}'"));
	}
}
