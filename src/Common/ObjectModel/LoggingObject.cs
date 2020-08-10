using System;
using Microsoft.Extensions.Logging;


namespace Imkk.ObjectModel {
	public class LoggingObject {
		#region data

		protected readonly ILogger? Logger;

		public string? NameForLogging { get; protected set; } = null;

		#endregion


		#region creation

		public LoggingObject(ILogger? logger) {
			// check argument
			// logger can be null

			// initialize member
			this.Logger = logger;
		}

		#endregion


		#region methods

		protected void LogDebug(string? message, Exception? exception = null, EventId eventId = default(EventId)) {
			ILogger? logger = this.Logger;
			if (logger != null) {
				LoggingUtil.LogDebug(logger, this.NameForLogging, message, exception, eventId);
			}
		}

		protected void LogTrace(string? message, Exception? exception = null, EventId eventId = default(EventId)) {
			ILogger? logger = this.Logger;
			if (logger != null) {
				LoggingUtil.LogTrace(logger, this.NameForLogging, message, exception, eventId);
			}
		}

		protected void LogInformation(string? message, Exception? exception = null, EventId eventId = default(EventId)) {
			ILogger? logger = this.Logger;
			if (logger != null) {
				LoggingUtil.LogInformation(logger, this.NameForLogging, message, exception, eventId);
			}
		}

		protected void LogWarning(string? message, Exception? exception = null, EventId eventId = default(EventId)) {
			ILogger? logger = this.Logger;
			if (logger != null) {
				LoggingUtil.LogWarning(logger, this.NameForLogging, message, exception, eventId);
			}
		}

		protected void LogError(string? message, Exception? exception = null, EventId eventId = default(EventId)) {
			ILogger? logger = this.Logger;
			if (logger != null) {
				LoggingUtil.LogError(logger, this.NameForLogging, message, exception, eventId);
			}
		}

		protected void LogCritical(string? message, Exception? exception = null, EventId eventId = default(EventId)) {
			ILogger? logger = this.Logger;
			if (logger != null) {
				LoggingUtil.LogCritical(logger, this.NameForLogging, message, exception, eventId);
			}
		}

		#endregion
	}
}
