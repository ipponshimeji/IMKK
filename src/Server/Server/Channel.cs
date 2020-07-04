using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using IMKK.WebSockets;
using IMKK.Server.Storage;


namespace IMKK.Server {
	public class Channel: IDisposable {
		#region constants

		public const int DefaultMaxConnectionCount = 8;

		#endregion


		#region data

		private readonly object instanceLocker = new object();


		public readonly string Key;

		private int maxConnectionCount = DefaultMaxConnectionCount;

		private int connectionCount = 0;

		private Queue<WebSocketConnection> connections = new Queue<WebSocketConnection>();

		#endregion


		#region properties
		#endregion


		#region creation & disposal

		public Channel(ChannelInfo info) {
			// check argument
			if (info == null) {
				throw new ArgumentNullException(nameof(info));
			}
			string? key = info.Key;
			if (key == null) {
				throw new ArgumentNullException(nameof(info.Key));
			}

			// initialize member
			this.Key = key;
		}

		public virtual void Dispose() {
		}

		#endregion


		#region methods

		public void AddConnection(WebSocketConnection connection) {
			// check argument
			if (connection == null) {
				throw new ArgumentNullException(nameof(connection));
			}
			if (connection.State != WebSocketState.Open) {
				throw new ArgumentException("It is not open state.", nameof(connection));
			}

			lock (this.instanceLocker) {
				// check state
				if (this.maxConnectionCount <= this.connectionCount) {
					throw new InvalidOperationException("Too many connections are added.");
				}

				// queue the connection
				this.connections.Enqueue(connection);
				++this.connectionCount;
			}
		}

		public async ValueTask ProcessMessageAsync(Func<SendMessageStream, ValueTask> sender, Func<ReceiveMessageStream, ValueTask> receiver) {
			throw new NotImplementedException();
		}

		public async ValueTask<string> ProcessTextAsync(string request, int millisecondsTimeout = Timeout.Infinite, CancellationToken cancellationToken = default(CancellationToken)) {
			throw new NotImplementedException();
		}

		public async ValueTask<TResponse> ProcessJsonAsync<TRequest, TResponse>(TRequest request, int millisecondsTimeout = Timeout.Infinite, CancellationToken cancellationToken = default(CancellationToken)) {
			throw new NotImplementedException();
		}

		#endregion


		#region privates

		private async ValueTask<WebSocketConnection> GetConnectionAsync(int millisecondsTimeOut, CancellationToken cancellationToken) {
			WebSocketConnection? connection = null;

			do {
				lock (this.instanceLocker) {
					while (this.connections.TryDequeue(out connection)) {
						if (connection.State == WebSocketState.Open) {
							return connection;
						} else {
							// unregister the connection
							--this.connectionCount;
							try {
								connection.Dispose();
							} catch {
								// continue
							} finally {
								connection = null;
							}
						}
					}
				}

				// wait for 
			} while (connection == null);

			return connection;
		}

		#endregion
	}
}
