using System;
using Microsoft.Extensions.Logging;


namespace Imkk.ObjectModel {
	public class ObjectWithRunningContext<T>: LoggingObject where T: class, IRunningContext {
		#region data

		protected readonly T RunningContext;

		#endregion


		#region creation

		public ObjectWithRunningContext(T runningContext): base(runningContext?.Logger) {
			// check argument
			if (runningContext == null) {
				throw new ArgumentNullException(nameof(runningContext));
			}

			// initialize member
			this.RunningContext = runningContext;
		}

		#endregion
	}
}
