using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KeyLock.MSSQL
{
	class PeriodicWork
	{
		private readonly Func<Task> _action;
		private readonly TimeSpan _interval;
		private readonly ILogger? _logger;
		private Task? _task;

		public PeriodicWork(Func<Task> action, TimeSpan interval, ILogger? logger = null)
		{
			_action = action;
			_interval = interval;
			_logger = logger;
		}

		public void Start(CancellationToken ct)
		{
			_task = Task.Run(async () =>
			{
				using PeriodicTimer timer = new(_interval);

				try
				{
					while (await timer.WaitForNextTickAsync(ct))
					{
						try
						{
							await _action();
						}
						catch (Exception ex)
						{
							_logger?.LogError(ex, ex.Message);
						}
					}
				}
				catch (OperationCanceledException)
				{
				}
			}, ct);
		}

		public Task Stop()
			=> _task ?? Task.CompletedTask;
	}
}
