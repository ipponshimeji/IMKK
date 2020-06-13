using System;
using System.Threading.Tasks;


namespace IMKK.Lib {
	public static class TaskUtil {
		#region methods

		public static void Sync(this Task task) {
			// check argument
			if (task == null) {
				throw new ArgumentNullException(nameof(task));
			}

			if (task.IsCompleted == false) {
				task.Wait();
			}
		}

		public static void Sync(this ValueTask valueTask) {
			if (valueTask.IsCompleted == false) {
				valueTask.AsTask().Sync();
			}
		}

		public static T Sync<T>(this Task<T> task) {
			// check argument
			if (task == null) {
				throw new ArgumentNullException(nameof(task));
			}

			if (task.IsCompleted == false) {
				task.Wait();
			}
			return task.Result;
		}

		public static T Sync<T>(this ValueTask<T> valueTask) {
			return valueTask.IsCompleted ? valueTask.Result : valueTask.AsTask().Sync();
		}

		#endregion
	}
}
