using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;


namespace Imkk.Testing {
	public class TestingLogger: ILogger, IReadOnlyList<TestingLogger.Entry> {
		#region types

		public class Entry {
			#region data

			public readonly LogLevel LogLevel;

			public readonly EventId EventId;

			public readonly string Message;

			public readonly Exception? Exception;

			#endregion


			#region constructor

			public Entry(LogLevel logLevel, EventId eventId, string message, Exception? exception) {
				// check argument
				Debug.Assert(message != null);

				// initialize members
				this.LogLevel = logLevel;
				this.EventId = eventId;
				this.Message = message;
				this.Exception = exception;
			}

			#endregion
		}

		#endregion


		#region data

		private readonly object instanceLocker = new object();


		private List<Entry> logs = new List<Entry>();

		private LogLevel logLevel = LogLevel.Debug;

		#endregion


		#region properties

		public LogLevel LogLevel {
			get {
				return this.logLevel;
			}
			set {
				lock (this.instanceLocker) {
					this.logLevel = value;
				}
			}
		}

		#endregion


		#region IEnumerable

		IEnumerator IEnumerable.GetEnumerator() {
			return this.logs.GetEnumerator();
		}

		#endregion


		#region IEnumerable<Entry>

		public IEnumerator<Entry> GetEnumerator() {
			return this.logs.GetEnumerator();
		}

		#endregion


		#region IReadOnlyCollection<Entry>

		public int Count => this.logs.Count;

		#endregion


		#region IReadOnlyList<Entry>

		public Entry this[int index] => this.logs[index];

		#endregion


		#region ILogger

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) {
			// check argument
			if (formatter == null) {
				throw new ArgumentNullException(nameof(formatter));
			}

			lock (this.instanceLocker) {
				if (this.logLevel <= logLevel) {
					string message = formatter(state, exception);
					Entry entry = new Entry(logLevel, eventId, message, exception);

					this.logs.Add(entry);
				}
			}
		}

		public bool IsEnabled(LogLevel logLevel) {
			return this.LogLevel <= logLevel;
		}

		public IDisposable BeginScope<TState>(TState state) {
			throw new NotImplementedException();
		}

		#endregion


		#region methods

		public void Clear() {
			this.logs.Clear();
		}

		#endregion
	}
}
