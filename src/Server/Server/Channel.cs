using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using IMKK.WebSockets;
using IMKK.Server.Storage;
using IMKK.Communication;

namespace IMKK.Server {
	public class Channel: IDisposable {
		#region data

		private readonly object instanceLocker = new object();


		public readonly string Key;

		public readonly int MaxConnectionCount;

		private int connectionCount = 0;

		private Queue<WebSocketConnection> connections = new Queue<WebSocketConnection>();

		#endregion


		#region properties
		#endregion


		#region creation & disposal

		public Channel(ChannelConfig config) {
			// check argument
			if (config == null) {
				throw new ArgumentNullException(nameof(config));
			}

			string? key = config.Key;
			if (key == null) {
				throw new ArgumentNullException(nameof(config.Key));
			}

			int maxConnectionCount = config.MaxConnectionCount;
			if (maxConnectionCount < 0) {
				throw new ArgumentOutOfRangeException(nameof(config.MaxConnectionCount));
			}

			// initialize member
			this.Key = key;
			this.MaxConnectionCount = maxConnectionCount;
		}

		public virtual void Dispose() {
		}

		#endregion


		#region methods

		/// <remarks>
		/// The ownership of the connection is moved to this srever. 
		/// </remarks>
		public async Task AddConnectionAsync(WebSocketConnection connection) {
			// check argument
			if (connection == null) {
				throw new ArgumentNullException(nameof(connection));
			}

			try {
				NegotiateResponse response = new NegotiateResponse(NegotiateStatus.Error, NegotiateResponse.StandardMessages.Error);

				// check connection state
				if (connection.State != WebSocketState.Open) {
					throw new ArgumentException("It is not open state.", nameof(connection));
				}

				try {
					// check state
					lock (this.instanceLocker) {
						if (this.MaxConnectionCount <= this.connectionCount) {
							response.Status = NegotiateStatus.TooManyConnection;
							response.Message = NegotiateResponse.StandardMessages.TooManyConnection;
							throw new InvalidOperationException(response.Message);
						}
						++this.connectionCount;
					}

					// send the successful negotiate response
					response.Status = NegotiateStatus.Succeeded;
					response.Message = null;
					await connection.SendJsonAsync<NegotiateResponse>(response);

					// add the connection to the queue
					lock (this.instanceLocker) {
						// add the connection after send the response,
						// not to be used before the negotiation completes
						this.connections.Enqueue(connection);
					}
				} catch {
					// send the failed negotiate response
					// Note that the response has been sent if response.Status is Succeeded.
					if (response.Status != NegotiateStatus.Succeeded) {
						await connection.SendJsonAsync<NegotiateResponse>(response);
					}

					// roll back the connection count
					if (response.Status != NegotiateStatus.TooManyConnection) {
						lock (this.instanceLocker) {
							--this.connectionCount;
						}
					}

					await connection.CloseAsync();
					throw;
				}
			} catch {
				// TODO: logging
				connection.Dispose();
				return;	// end the task
			}
		}

		public ValueTask ProcessMessageAsync(Func<SendMessageStream, ValueTask> sender, Func<ReceiveMessageStream, ValueTask> receiver) {
			throw new NotImplementedException();
		}

		public ValueTask<string> ProcessTextAsync(string request, int millisecondsTimeout = Timeout.Infinite, CancellationToken cancellationToken = default(CancellationToken)) {
			throw new NotImplementedException();
		}

		public ValueTask<TResponse> ProcessJsonAsync<TRequest, TResponse>(TRequest request, int millisecondsTimeout = Timeout.Infinite, CancellationToken cancellationToken = default(CancellationToken)) {
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
