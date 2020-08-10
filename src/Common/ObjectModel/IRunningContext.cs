using System;
using Microsoft.Extensions.Logging;


namespace Imkk.ObjectModel {
	public interface IRunningContext {
		ILogger? Logger { get; }

		RunningTaskTable RunningTaskTable { get; }
	}
}
