using System;
using System.Threading.Tasks;


namespace IMKK.Lib {
	public static class TaskUtil {
		#region methods

		public static void Sync(this Task task, bool passThroughAggregateException = false) {
			// check argument
			if (task == null) {
				throw new ArgumentNullException(nameof(task));
			}

			// check state
			if (task.IsCompleted) {
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
						throw innerException;
					} else {
						throw;
					}
				}
			}
		}

		public static void Sync(this ValueTask valueTask, bool passThroughAggregateException = false) {
			if (valueTask.IsCompleted == false) {
				valueTask.AsTask().Sync(passThroughAggregateException);
			}
		}

		public static T Sync<T>(this Task<T> task, bool passThroughAggregateException = false) {
			Sync((Task)task, passThroughAggregateException);
			return task.Result;
		}

		public static T Sync<T>(this ValueTask<T> valueTask, bool passThroughAggregateException = false) {
			return valueTask.IsCompleted ? valueTask.Result : valueTask.AsTask().Sync(passThroughAggregateException);
		}

		#endregion
	}
}
