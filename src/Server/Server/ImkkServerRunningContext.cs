using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Imkk.ObjectModel;


namespace Imkk.Server {
	public class ImkkServerRunningContext: RunningContext, IImkkServerRunningContext {
		#region data

		public new static readonly ImkkServerRunningContext Default = new ImkkServerRunningContext();

		#endregion


		#region creation

		public ImkkServerRunningContext(ILogger? logger, RunningTaskTable? runningTaskTable): base(logger, runningTaskTable) {
		}

		public ImkkServerRunningContext() : base() {
		}

		#endregion


		#region ImkkServerRunningContext

		public virtual ImkkServer CreateImkkServer(IConfigurationSection config) {
			return ImkkServer.Create(this, config);
		}


		public virtual Channel CreateChannel(IConfigurationSection config) {
			return new Channel(this, config);
		}

		#endregion
	}
}
