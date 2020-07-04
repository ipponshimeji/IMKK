using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;


namespace IMKK {
	public static class TaskUtil {
		#region data

		// global running task table
		public static readonly RunningTaskTable RunningTaskTable = new RunningTaskTable();

		#endregion


		#region methods

		public static void Sync(this Task task, bool passThroughAggregateException = false) {
			// check argument
			if (task == null) {
				throw new ArgumentNullException(nameof(task));
			}

			// check state
			if (task.IsCompletedSuccessfully) {
				return;
			}

			// wait for the completion of the task
			if (passThroughAggregateException) {
				task.Wait();
			} else {
				try {
					task.Wait();
				} catch (AggregateException exception) {
					Exception? innerException = exception.InnerException;
					if (innerException != null) {
						ExceptionDispatchInfo.Capture(innerException).Throw();
					} else {
						throw;
					}
				}
			}
		}

		public static void Sync(this ValueTask valueTask, bool passThroughAggregateException = false) {
			if (valueTask.IsCompletedSuccessfully == false) {
				valueTask.AsTask().Sync(passThroughAggregateException);
			}
		}

		public static T Sync<T>(this Task<T> task, bool passThroughAggregateException = false) {
			Sync((Task)task, passThroughAggregateException);
			return task.Result;
		}

		public static T Sync<T>(this ValueTask<T> valueTask, bool passThroughAggregateException = false) {
			return valueTask.IsCompletedSuccessfully ? valueTask.Result : valueTask.AsTask().Sync(passThroughAggregateException);
		}

		#endregion
	}
}
