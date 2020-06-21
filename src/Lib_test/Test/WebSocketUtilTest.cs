using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;


namespace IMKK.Tests {
	public class TaskUtilTest {
		#region types

		public class ProcContext {
			#region data

			public bool Run { get; set; } = false;

			#endregion
		}

		public class ProcContext<T>: ProcContext {
			#region data

			public readonly T ReturnValue;

			#endregion


			#region creation

			public ProcContext(T returnValue) {
				this.ReturnValue = returnValue;
			}

			#endregion
		}

		#endregion


		#region constants

		public const int DefaultDelay = 1000;			// 1 [s]

		public const int DefaultWaitingInterval = 300;	// 300 [ms]

		#endregion


		#region utilities

		protected static void TaskProc(ProcContext context, int millisecondsDelay, Exception? exception) {
			// check arguments
			if (context == null) {
				throw new ArgumentNullException(nameof(context));
			}
			if (millisecondsDelay < 0) {
				throw new ArgumentOutOfRangeException(nameof(millisecondsDelay));
			}

			// insert delay
			if (0 < millisecondsDelay) {
				Thread.Sleep(millisecondsDelay);
			}

			// run the task
			Debug.Assert(context.Run == false);
			if (exception == null) {
				context.Run = true;
			} else {
				// throw the exception
				throw exception;
			}
		}

		protected static T TaskProc<T>(ProcContext<T> context, int millisecondsDelay, Exception? exception) {
			TaskProc((ProcContext)context, millisecondsDelay, exception);
			return context.ReturnValue;
		}

		protected static Task CreateTask(ProcContext context, int millisecondsDelay, Exception? exception) {
			// create a Task instance which runs TestProc()
			Task task = new Task(() => TaskProc(context, millisecondsDelay, exception));
			if (millisecondsDelay == 0) {
				task.Start();
			}

			return task;
		}

		protected static Task<T> CreateTask<T>(ProcContext<T> context, int millisecondsDelay, Exception? exception) {
			// create a Task<T> instance which runs TestProc<T>()
			Task<T> task = new Task<T>(() => TaskProc<T>(context, millisecondsDelay, exception));
			if (millisecondsDelay == 0) {
				task.Start();
			}

			return task;
		}

		protected static async ValueTask CreateValueTask(ProcContext context, int millisecondsDelay, Exception? exception) {
			// create a ValueTask instance which runs TaskProc()
			void task() {
				TaskProc(context, millisecondsDelay, exception);
			}

			if (0 < millisecondsDelay) {
				await Task.Run(task);
			} else {
				task();
			}
		}

		protected static async ValueTask<T> CreateValueTask<T>(ProcContext<T> context, int millisecondsDelay, Exception? exception) {
			// create a ValueTask<T> instance which runs TaskProc<T>()
			T task() {
				TaskProc(context, millisecondsDelay, exception);
				return context.ReturnValue;
			}

			if (0 < millisecondsDelay) {
				return await Task<T>.Run(task);
			} else {
				return task();
			}
		}

		#endregion


		#region Sync(Task, int)

		public class Sync_Task {
			#region tests

			[Fact(DisplayName = "task: null")]
			public void task_null() {
				// arrange
				Task task = null!;

				// act
				ArgumentNullException actualException = Assert.Throws<ArgumentNullException>(() => {
					TaskUtil.Sync(task);
				});

				// assert
				Assert.Equal("task", actualException.ParamName);
			}

			[Fact(DisplayName = "task: not completed, successful task")]
			public void task_NotCompleted_Succeeded() {
				// arrange
				ProcContext context = new ProcContext();
				Debug.Assert(context.Run == false);
				Task task = CreateTask(context: context, millisecondsDelay: DefaultDelay, exception: null);

				// act
				task.Start();
				task.Sync();

				// assert
				Assert.True(task.IsCompletedSuccessfully);
				Assert.True(context.Run);
			}

			[Fact(DisplayName = "task: not completed, faulted task; passThroughAggregateException: false")]
			public void task_NotCompleted_Faulted_passThroughAggregateException_false() {
				// arrange
				string errorMessage = "error.";
				ApplicationException exception = new ApplicationException(errorMessage);
				ProcContext context = new ProcContext();
				Debug.Assert(context.Run == false);
				Task task = CreateTask(context: context, millisecondsDelay: DefaultDelay, exception: exception);
				bool passThroughAggregateException = false;

				// act
				task.Start();
				ApplicationException actualException = Assert.Throws<ApplicationException>(() => {
					task.Sync(passThroughAggregateException);
				});

				// assert
				Assert.True(task.IsFaulted);
				Assert.False(context.Run);
				Assert.Equal(errorMessage, actualException.Message);
			}

			[Fact(DisplayName = "task: not completed, faulted task; passThroughAggregateException: true")]
			public void task_NotCompleted_Faulted_passThroughAggregateException_true() {
				// arrange
				string errorMessage = "error.";
				ApplicationException exception = new ApplicationException(errorMessage);
				ProcContext context = new ProcContext();
				Debug.Assert(context.Run == false);
				Task task = CreateTask(context: context, millisecondsDelay: DefaultDelay, exception: exception);
				bool passThroughAggregateException = true;

				// act
				task.Start();
				AggregateException actualException = Assert.Throws<AggregateException>(() => {
					task.Sync(passThroughAggregateException);
				});

				// assert
				Assert.True(task.IsFaulted);
				Assert.False(context.Run);
				ApplicationException? innerException = actualException.InnerException as ApplicationException;
				Assert.NotNull(innerException);
				Assert.Equal(errorMessage, innerException!.Message);
			}

			[Fact(DisplayName = "task: completed, successful task")]
			public void task_Completed_Succeeded() {
				// arrange
				ProcContext context = new ProcContext();
				Debug.Assert(context.Run == false);
				Task task = CreateTask(context: context, millisecondsDelay: 0, exception: null);

				// act
				while (task.IsCompleted == false) {
					Thread.Sleep(DefaultWaitingInterval);
				}
				task.Sync();

				// assert
				Assert.True(task.IsCompletedSuccessfully);
				Assert.True(context.Run);
			}

			[Fact(DisplayName = "task: completed, faulted task; passThroughAggregateException: false")]
			public void task_Completed_Faulted_passThroughAggregateException_false() {
				// arrange
				string errorMessage = "error.";
				ApplicationException exception = new ApplicationException(errorMessage);
				ProcContext context = new ProcContext();
				Debug.Assert(context.Run == false);
				Task task = CreateTask(context: context, millisecondsDelay: 0, exception: exception);
				bool passThroughAggregateException = false;

				// act
				while (task.IsCompleted == false) {
					Thread.Sleep(DefaultWaitingInterval);
				}
				ApplicationException actualException = Assert.Throws<ApplicationException>(() => {
					task.Sync(passThroughAggregateException);
				});

				// assert
				Assert.True(task.IsFaulted);
				Assert.False(context.Run);
				Assert.Equal(errorMessage, actualException.Message);
			}

			[Fact(DisplayName = "task: completed, faulted task, passThroughAggregateException: true")]
			public void task_Completed_Faulted_passThroughAggregateException_true() {
				// arrange
				string errorMessage = "error.";
				ApplicationException exception = new ApplicationException(errorMessage);
				ProcContext context = new ProcContext();
				Debug.Assert(context.Run == false);
				Task task = CreateTask(context: context, millisecondsDelay: 0, exception: exception);
				bool passThroughAggregateException = true;

				// act
				while (task.IsCompleted == false) {
					Thread.Sleep(DefaultWaitingInterval);
				}
				AggregateException actualException = Assert.Throws<AggregateException>(() => {
					task.Sync(passThroughAggregateException);
				});

				// assert
				Assert.True(task.IsFaulted);
				Assert.False(context.Run);
				ApplicationException? innerException = actualException.InnerException as ApplicationException;
				Assert.NotNull(innerException);
				Assert.Equal(errorMessage, innerException!.Message);
			}

			#endregion
		}

		#endregion


		#region Sync(ValueTask, int)

		public class Sync_ValueTask {
			#region tests

			[Fact(DisplayName = "task: not completed, successful task")]
			public void task_NotCompleted_Succeeded() {
				// arrange
				ProcContext context = new ProcContext();
				Debug.Assert(context.Run == false);
				ValueTask task = CreateValueTask(context: context, millisecondsDelay: DefaultDelay, exception: null);

				// act
				task.Sync();

				// assert
				Assert.True(task.IsCompleted);
				Assert.True(context.Run);
			}

			[Fact(DisplayName = "task: not completed, faulted task; passThroughAggregateException: false")]
			public void task_NotCompleted_Faulted_passThroughAggregateException_false() {
				// arrange
				string errorMessage = "error.";
				ApplicationException exception = new ApplicationException(errorMessage);
				ProcContext context = new ProcContext();
				Debug.Assert(context.Run == false);
				ValueTask task = CreateValueTask(context: context, millisecondsDelay: DefaultDelay, exception: exception);
				bool passThroughAggregateException = false;

				// act
				ApplicationException actualException = Assert.Throws<ApplicationException>(() => {
					task.Sync(passThroughAggregateException);
				});

				// assert
				Assert.True(task.IsFaulted);
				Assert.False(context.Run);
				Assert.Equal(errorMessage, actualException.Message);
			}

			[Fact(DisplayName = "task: not completed, faulted task; passThroughAggregateException: true")]
			public void task_NotCompleted_Faulted_passThroughAggregateException_true() {
				// arrange
				string errorMessage = "error.";
				ApplicationException exception = new ApplicationException(errorMessage);
				ProcContext context = new ProcContext();
				Debug.Assert(context.Run == false);
				ValueTask task = CreateValueTask(context: context, millisecondsDelay: DefaultDelay, exception: exception);
				bool passThroughAggregateException = true;

				// act
				AggregateException actualException = Assert.Throws<AggregateException>(() => {
					task.Sync(passThroughAggregateException);
				});

				// assert
				Assert.True(task.IsFaulted);
				Assert.False(context.Run);
				ApplicationException? innerException = actualException.InnerException as ApplicationException;
				Assert.NotNull(innerException);
				Assert.Equal(errorMessage, innerException!.Message);
			}

			[Fact(DisplayName = "task: completed, successful task")]
			public void task_Completed_Succeeded() {
				// arrange
				ProcContext context = new ProcContext();
				Debug.Assert(context.Run == false);
				ValueTask task = CreateValueTask(context: context, millisecondsDelay: 0, exception: null);

				// act
				while (task.IsCompleted == false) {
					Thread.Sleep(DefaultWaitingInterval);
				}
				task.Sync();

				// assert
				Assert.True(task.IsCompletedSuccessfully);
				Assert.True(context.Run);
			}

			[Fact(DisplayName = "task: completed, faulted task; passThroughAggregateException: false")]
			public void task_Completed_Faulted_passThroughAggregateException_false() {
				// arrange
				string errorMessage = "error.";
				ApplicationException exception = new ApplicationException(errorMessage);
				ProcContext context = new ProcContext();
				Debug.Assert(context.Run == false);
				ValueTask task = CreateValueTask(context: context, millisecondsDelay: 0, exception: exception);
				bool passThroughAggregateException = false;

				// act
				while (task.IsCompleted == false) {
					Thread.Sleep(DefaultWaitingInterval);
				}
				ApplicationException actualException = Assert.Throws<ApplicationException>(() => {
					task.Sync(passThroughAggregateException);
				});

				// assert
				Assert.True(task.IsFaulted);
				Assert.False(context.Run);
				Assert.Equal(errorMessage, actualException.Message);
			}

			[Fact(DisplayName = "task: completed, faulted task, passThroughAggregateException: true")]
			public void task_Completed_Faulted_passThroughAggregateException_true() {
				// arrange
				string errorMessage = "error.";
				ApplicationException exception = new ApplicationException(errorMessage);
				ProcContext context = new ProcContext();
				Debug.Assert(context.Run == false);
				ValueTask task = CreateValueTask(context: context, millisecondsDelay: 0, exception: exception);
				bool passThroughAggregateException = true;

				// act
				while (task.IsCompleted == false) {
					Thread.Sleep(DefaultWaitingInterval);
				}
				AggregateException actualException = Assert.Throws<AggregateException>(() => {
					task.Sync(passThroughAggregateException);
				});

				// assert
				Assert.True(task.IsFaulted);
				Assert.False(context.Run);
				ApplicationException? innerException = actualException.InnerException as ApplicationException;
				Assert.NotNull(innerException);
				Assert.Equal(errorMessage, innerException!.Message);
			}

			#endregion
		}

		#endregion


		#region Sync(Task<T>, int)

		public class Sync_TaskT {
			#region tests

			[Fact(DisplayName = "task: null")]
			public void task_null() {
				// arrange
				Task<bool> task = null!;

				// act
				ArgumentNullException actualException = Assert.Throws<ArgumentNullException>(() => {
					TaskUtil.Sync(task);
				});

				// assert
				Assert.Equal("task", actualException.ParamName);
			}

			[Fact(DisplayName = "task, not completed, successful task")]
			public void task_NotCompleted_Succeeded() {
				// arrange
				ProcContext<int> context = new ProcContext<int>(5);
				Debug.Assert(context.Run == false);
				Task<int> task = CreateTask<int>(context: context, millisecondsDelay: DefaultDelay, exception: null);

				// act
				task.Start();
				int actualReturnValue = task.Sync();

				// assert
				Assert.True(task.IsCompletedSuccessfully);
				Assert.True(context.Run);
				Assert.Equal(context.ReturnValue, actualReturnValue);
			}

			[Fact(DisplayName = "task: not completed, faulted task; passThroughAggregateException: false")]
			public void task_NotCompleted_Faulted_passThroughAggregateException_false() {
				// arrange
				string errorMessage = "error.";
				ApplicationException exception = new ApplicationException(errorMessage);
				ProcContext<bool> context = new ProcContext<bool>(true);
				Debug.Assert(context.Run == false);
				Task<bool> task = CreateTask<bool>(context: context, millisecondsDelay: DefaultDelay, exception: exception);
				bool passThroughAggregateException = false;

				// act
				task.Start();
				ApplicationException actualException = Assert.Throws<ApplicationException>(() => {
					task.Sync(passThroughAggregateException);
				});

				// assert
				Assert.True(task.IsFaulted);
				Assert.False(context.Run);
				Assert.Equal(errorMessage, actualException.Message);
			}

			[Fact(DisplayName = "task: not completed, faulted task; passThroughAggregateException: true")]
			public void task_NotCompleted_Faulted_passThroughAggregateException_true() {
				// arrange
				string errorMessage = "error.";
				ApplicationException exception = new ApplicationException(errorMessage);
				ProcContext<bool> context = new ProcContext<bool>(true);
				Debug.Assert(context.Run == false);
				Task<bool> task = CreateTask<bool>(context: context, millisecondsDelay: DefaultDelay, exception: exception);
				bool passThroughAggregateException = true;

				// act
				task.Start();
				AggregateException actualException = Assert.Throws<AggregateException>(() => {
					task.Sync(passThroughAggregateException);
				});

				// assert
				Assert.True(task.IsFaulted);
				Assert.False(context.Run);
				ApplicationException? innerException = actualException.InnerException as ApplicationException;
				Assert.NotNull(innerException);
				Assert.Equal(errorMessage, innerException!.Message);
			}

			[Fact(DisplayName = "task: completed, successful task")]
			public void task_Completed_Succeeded() {
				// arrange
				ProcContext<string> context = new ProcContext<string>("return value");
				Debug.Assert(context.Run == false);
				Task<string> task = CreateTask<string>(context: context, millisecondsDelay: 0, exception: null);

				// act
				while (task.IsCompleted == false) {
					Thread.Sleep(DefaultWaitingInterval);
				}
				string actualReturnValue = task.Sync();

				// assert
				Assert.True(task.IsCompletedSuccessfully);
				Assert.True(context.Run);
				Assert.Equal(context.ReturnValue, actualReturnValue);
			}

			[Fact(DisplayName = "task: completed, faulted task; passThroughAggregateException: false")]
			public void task_Completed_Faulted_passThroughAggregateException_false() {
				// arrange
				string errorMessage = "error.";
				ApplicationException exception = new ApplicationException(errorMessage);
				ProcContext<bool> context = new ProcContext<bool>(true);
				Debug.Assert(context.Run == false);
				Task<bool> task = CreateTask<bool>(context: context, millisecondsDelay: 0, exception: exception);
				bool passThroughAggregateException = false;

				// act
				while (task.IsCompleted == false) {
					Thread.Sleep(DefaultWaitingInterval);
				}
				ApplicationException actualException = Assert.Throws<ApplicationException>(() => {
					task.Sync(passThroughAggregateException);
				});

				// assert
				Assert.True(task.IsFaulted);
				Assert.False(context.Run);
				Assert.Equal(errorMessage, actualException.Message);
			}

			[Fact(DisplayName = "task: completed, faulted task; passThroughAggregateException: true")]
			public void task_Completed_Faulted_passThroughAggregateException_true() {
				// arrange
				string errorMessage = "error.";
				ApplicationException exception = new ApplicationException(errorMessage);
				ProcContext<bool> context = new ProcContext<bool>(true);
				Debug.Assert(context.Run == false);
				Task<bool> task = CreateTask<bool>(context: context, millisecondsDelay: 0, exception: exception);
				bool passThroughAggregateException = true;

				// act
				while (task.IsCompleted == false) {
					Thread.Sleep(DefaultWaitingInterval);
				}
				AggregateException actualException = Assert.Throws<AggregateException>(() => {
					task.Sync(passThroughAggregateException);
				});

				// assert
				Assert.True(task.IsFaulted);
				Assert.False(context.Run);
				ApplicationException? innerException = actualException.InnerException as ApplicationException;
				Assert.NotNull(innerException);
				Assert.Equal(errorMessage, innerException!.Message);
			}

			#endregion
		}

		#endregion


		#region Sync(ValueTask<T>, int)

		public class Sync_ValueTaskT {
			#region tests

			[Fact(DisplayName = "task, not completed, successful task")]
			public void task_NotCompleted_Succeeded() {
				// arrange
				ProcContext<int> context = new ProcContext<int>(-5);
				Debug.Assert(context.Run == false);
				ValueTask<int> task = CreateValueTask<int>(context: context, millisecondsDelay: DefaultDelay, exception: null);

				// act
				int actualReturnValue = task.Sync();

				// assert
				Assert.True(task.IsCompletedSuccessfully);
				Assert.True(context.Run);
				Assert.Equal(context.ReturnValue, actualReturnValue);
			}

			[Fact(DisplayName = "task: not completed, faulted task; passThroughAggregateException: false")]
			public void task_NotCompleted_Faulted_passThroughAggregateException_false() {
				// arrange
				string errorMessage = "error.";
				ApplicationException exception = new ApplicationException(errorMessage);
				ProcContext<bool> context = new ProcContext<bool>(true);
				Debug.Assert(context.Run == false);
				ValueTask<bool> task = CreateValueTask<bool>(context: context, millisecondsDelay: DefaultDelay, exception: exception);
				bool passThroughAggregateException = false;

				// act
				ApplicationException actualException = Assert.Throws<ApplicationException>(() => {
					task.Sync(passThroughAggregateException);
				});

				// assert
				Assert.True(task.IsFaulted);
				Assert.False(context.Run);
				Assert.Equal(errorMessage, actualException.Message);
			}

			[Fact(DisplayName = "task: not completed, faulted task; passThroughAggregateException: true")]
			public void task_NotCompleted_Faulted_passThroughAggregateException_true() {
				// arrange
				string errorMessage = "error.";
				ApplicationException exception = new ApplicationException(errorMessage);
				ProcContext<bool> context = new ProcContext<bool>(true);
				Debug.Assert(context.Run == false);
				ValueTask<bool> task = CreateValueTask<bool>(context: context, millisecondsDelay: DefaultDelay, exception: exception);
				bool passThroughAggregateException = true;

				// act
				AggregateException actualException = Assert.Throws<AggregateException>(() => {
					task.Sync(passThroughAggregateException);
				});

				// assert
				Assert.True(task.IsFaulted);
				Assert.False(context.Run);
				ApplicationException? innerException = actualException.InnerException as ApplicationException;
				Assert.NotNull(innerException);
				Assert.Equal(errorMessage, innerException!.Message);
			}

			[Fact(DisplayName = "task: completed, successful task")]
			public void task_Completed_Succeeded() {
				// arrange
				ProcContext<string> context = new ProcContext<string>("return value");
				Debug.Assert(context.Run == false);
				ValueTask<string> task = CreateValueTask<string>(context: context, millisecondsDelay: 0, exception: null);

				// act
				while (task.IsCompleted == false) {
					Thread.Sleep(DefaultWaitingInterval);
				}
				string actualReturnValue = task.Sync();

				// assert
				Assert.True(task.IsCompletedSuccessfully);
				Assert.True(context.Run);
				Assert.Equal(context.ReturnValue, actualReturnValue);
			}

			[Fact(DisplayName = "task: completed, faulted task; passThroughAggregateException: false")]
			public void task_Completed_Faulted_passThroughAggregateException_false() {
				// arrange
				string errorMessage = "error.";
				ApplicationException exception = new ApplicationException(errorMessage);
				ProcContext<bool> context = new ProcContext<bool>(true);
				Debug.Assert(context.Run == false);
				ValueTask<bool> task = CreateValueTask<bool>(context: context, millisecondsDelay: 0, exception: exception);
				bool passThroughAggregateException = false;

				// act
				while (task.IsCompleted == false) {
					Thread.Sleep(DefaultWaitingInterval);
				}
				ApplicationException actualException = Assert.Throws<ApplicationException>(() => {
					task.Sync(passThroughAggregateException);
				});

				// assert
				Assert.True(task.IsFaulted);
				Assert.False(context.Run);
				Assert.Equal(errorMessage, actualException.Message);
			}

			[Fact(DisplayName = "task: completed, faulted task; passThroughAggregateException: true")]
			public void task_Completed_Faulted_passThroughAggregateException_true() {
				// arrange
				string errorMessage = "error.";
				ApplicationException exception = new ApplicationException(errorMessage);
				ProcContext<bool> context = new ProcContext<bool>(true);
				Debug.Assert(context.Run == false);
				ValueTask<bool> task = CreateValueTask<bool>(context: context, millisecondsDelay: 0, exception: exception);
				bool passThroughAggregateException = true;

				// act
				while (task.IsCompleted == false) {
					Thread.Sleep(DefaultWaitingInterval);
				}
				AggregateException actualException = Assert.Throws<AggregateException>(() => {
					task.Sync(passThroughAggregateException);
				});

				// assert
				Assert.True(task.IsFaulted);
				Assert.False(context.Run);
				ApplicationException? innerException = actualException.InnerException as ApplicationException;
				Assert.NotNull(innerException);
				Assert.Equal(errorMessage, innerException!.Message);
			}

			#endregion
		}

		#endregion
	}
}
