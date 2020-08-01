using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace Imkk {
	public class RunningTaskTable {
		#region data

		private object instanceLocker = new object();

		private readonly Dictionary<Task, Task> tasks = new Dictionary<Task, Task>();

		public bool Closed { get; private set; } = false;

		#endregion


		#region properties

		public int Count {
			get {
				return tasks.Count;
			}
		}

		#endregion


		#region methods

		public void MonitorTask(Task task) {
			// check argument
			if (task == null) {
				throw new ArgumentNullException(nameof(task));
			}
			if (task.IsCompleted) {
				// nothing to do
				// TODO: logging
				return;
			}

			// add the task to the task table
			// assign task to keyTask provisionally to suppress NRT warning
			Task keyTask = task;
			keyTask = new Task(async () => {
				try {
					await task;
				} finally {
					RemoveTask(keyTask);
				}
			});

			AddTask(keyTask, task);
			try {
				task.Start();
			} catch {
				RemoveTask(keyTask);
				throw;
			}
			// TODO: logging
		}

		public void MonitorTask(ValueTask valueTask) {
			if (valueTask.IsCompletedSuccessfully == false) {
				MonitorTask(valueTask.AsTask());
			}
		}

		public void MonitorTask(Action action) {
			// check argument
			if (action == null) {
				throw new ArgumentNullException(nameof(action));
			}

			// add the task to the task table
			Task task = null!;
			task = new Task(() => {
				try {
					action();
				} finally {
					RemoveTask(task);
				}
			});

			AddTask(task, task);
			try {
				task.Start();
			} catch {
				RemoveTask(task);
				throw;
			}
		}

		public async Task RunTaskAsync(Task task) {
			// check argument
			if (task == null) {
				throw new ArgumentNullException(nameof(task));
			}
			if (task.IsCompleted) {
				// nothing to do
				return;
			}

			// run the task
			// use tash as its key
			AddTask(task, task);
			try {
				await task;
			} finally {
				RemoveTask(task);
			}
		}

		public bool WaitForAllTasks(bool close = true, int millisecondsTimeout = Timeout.Infinite) {
			Task[] tasks;

			// update state
			lock (this.instanceLocker) {
				if (close) {
					this.Closed = true;
				}
				tasks = this.tasks.Keys.ToArray();
			}

			// wait for all tasks
			if (tasks.Length <= 0) {
				// no task to be waited for
				return true;
			}
			bool result = Task.WaitAll(tasks, millisecondsTimeout);
			Debug.Assert(result == false || close == false || this.tasks.Count == 0);

			return result;
		}

		public Task? WaitForAnyTask(int millisecondsTimeout = Timeout.Infinite, CancellationToken cancellationToken = default(CancellationToken)) {
			Task[] keys;
			Task[] values;

			// get task list
			lock (this.instanceLocker) {
				keys = this.tasks.Keys.ToArray();
				values = this.tasks.Values.ToArray();
			}

			// wait for tasks
			if (keys.Length <= 0) {
				return null;
			} else {
				int index = Task.WaitAny(keys, millisecondsTimeout, cancellationToken);
				return (index < 0) ? null : values[index];
			}
		}

		public async ValueTask<Task?> WaitForAnyTaskAsync(int millisecondsTimeout = Timeout.Infinite, CancellationToken cancellationToken = default(CancellationToken)) {
			Task[] keys;
			Task[] values;

			// get task list
			lock (this.instanceLocker) {
				keys = this.tasks.Keys.ToArray();
				values = this.tasks.Values.ToArray();
			}

			// wait for tasks
			if (keys.Length <= 0) {
				// no task
				return null;
			} else {
				int index = await Task.Run<int>(() => Task.WaitAny(keys, millisecondsTimeout, cancellationToken));
				return (index < 0) ? null : values[index];
			}
		}

		#endregion


		#region privates

		private void AddTask(Task keyTask, Task task) {
			// check argument
			Debug.Assert(keyTask != null);
			Debug.Assert(task != null);

			lock (this.instanceLocker) {
				// check state
				if (this.Closed) {
					throw new ObjectDisposedException("Running Task Table");
				}

				Debug.Assert(this.tasks.ContainsKey(keyTask) == false);
				this.tasks.Add(keyTask, task);
			}
		}

		private void RemoveTask(Task keyTask) {
			// check argument
			Debug.Assert(keyTask != null);

			lock (this.instanceLocker) {
				this.tasks.Remove(keyTask);
			}
		}

		#endregion
	}
}
