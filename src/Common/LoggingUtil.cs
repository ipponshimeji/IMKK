using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Imkk {
	public static class LoggingUtil {
		#region types

		private class DummyDisposableObject: IDisposable {
			#region IDisposable

			public void Dispose() {
				// do nothing
			}

			#endregion
		}

		private class RedirectingLogger: ILogger {
			#region data

			private ILogger? targetLogger = null;

			#endregion


			#region properties

			public ILogger? TargetLogger {
				get {
					return this.targetLogger;
				}
				set {
					Interlocked.Exchange(ref this.targetLogger, value);
				}
			}

			#endregion


			#region ILogger

			public IDisposable BeginScope<TState>(TState state) {
				// assign this.targetLogger into a local variable,
				// this.targetLogger may be changed by other thread
				ILogger? logger = this.targetLogger;
				return (logger != null) ? logger.BeginScope<TState>(state) : new DummyDisposableObject();
			}

			public bool IsEnabled(LogLevel logLevel) {
				// assign this.targetLogger into a local variable,
				// this.targetLogger may be changed by other thread
				ILogger? logger = this.targetLogger;
				return (logger != null)? logger.IsEnabled(logLevel): false;
			}

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) {
				// assign this.targetLogger into a local variable,
				// this.targetLogger may be changed by other thread
				ILogger? logger = this.targetLogger;
				if (logger != null) {
					logger.Log<TState>(logLevel, eventId, state, exception, formatter);
				}
			}

			#endregion
		}

		#endregion


		#region data

		public static readonly EventId DefaultEventId = new EventId();

		private static RedirectingLogger redirectingLogger = new RedirectingLogger();

		#endregion


		#region properties

		public static ILogger DefaultLogger => redirectingLogger;

		#endregion


		#region methods

		public static void SetDefaultLogger(ILoggerFactory loggerFactory) {
			// check argument
			if (loggerFactory == null) {
				throw new ArgumentNullException(nameof(loggerFactory));
			}

			// replace the target logger of the redirecting logger with the new logger
			ILogger logger = loggerFactory.CreateLogger("Imkk");
			redirectingLogger.TargetLogger = logger;
		}


		public static string FormatMessage(string? header, string? message) {
			if (string.IsNullOrEmpty(header)) {
				return message ?? string.Empty;
			} else {
				return $"[{header}] {message}";				
			}
		}

		public static string FormatLog(string message, Exception? exception) {
			return message;
		}

		public static void Log(this ILogger logger, LogLevel logLevel, string? header, string? message, Exception? exception = null, EventId eventId = default(EventId)) {
			// check argument
			if (logger == null) {
				throw new ArgumentNullException(nameof(logger));
			}

			logger.Log(logLevel, eventId, FormatMessage(header, message), exception, FormatLog);
		}

		public static void LogDebug(this ILogger logger, string? header, string? message, Exception? exception = null, EventId eventId = default(EventId)) {
			Log(logger, LogLevel.Debug, header, message, exception, eventId);
		}

		public static void LogTrace(this ILogger logger, string? header, string? message, Exception? exception = null, EventId eventId = default(EventId)) {
			Log(logger, LogLevel.Trace, header, message, exception, eventId);
		}

		public static void LogInformation(this ILogger logger, string? header, string? message, Exception? exception = null, EventId eventId = default(EventId)) {
			Log(logger, LogLevel.Information, header, message, exception, eventId);
		}

		public static void LogWarning(this ILogger logger, string? header, string? message, Exception? exception = null, EventId eventId = default(EventId)) {
			Log(logger, LogLevel.Warning, header, message, exception, eventId);
		}

		public static void LogError(this ILogger logger, string? header, string? message, Exception? exception = null, EventId eventId = default(EventId)) {
			Log(logger, LogLevel.Error, header, message, exception, eventId);
		}

		public static void LogCritical(this ILogger logger, string? header, string? message, Exception? exception = null, EventId eventId = default(EventId)) {
			Log(logger, LogLevel.Critical, header, message, exception, eventId);
		}

		#endregion
	}
}
