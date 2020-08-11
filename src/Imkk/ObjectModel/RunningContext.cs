using System;
using Microsoft.Extensions.Logging;


namespace Imkk.ObjectModel {
	public class RunningContext: IRunningContext {
		#region data

		public static readonly RunningContext Default = new RunningContext();


		private readonly ILogger? logger;

		private readonly RunningTaskTable runningTaskTable;

		#endregion


		#region creation

		public RunningContext(ILogger? logger, RunningTaskTable? runningTaskTable) {
			// check argument
			// logger can be null
			if (runningTaskTable == null) {
				runningTaskTable = TaskUtil.RunningTaskTable;
			}

			// initialize member
			this.logger = logger;
			this.runningTaskTable = runningTaskTable;
		}

		public RunningContext(): this(LoggingUtil.DefaultLogger, TaskUtil.RunningTaskTable) {
		}

		#endregion


		#region IRunningContext

		public virtual ILogger? Logger => this.logger;

		public virtual RunningTaskTable RunningTaskTable => this.runningTaskTable;

		#endregion
	}
}
