using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Imkk.WebSockets;
using Imkk.Server.Configurations;
using Imkk.Communication;
using System.Runtime.InteropServices;

namespace Imkk.Server {
	public class Channel: IDisposable {
		#region types

		protected class WaitingRequest: IDisposable {
			#region data

			private ManualResetEventSlim? resumeEvent = new ManualResetEventSlim(initialState: false);

			public WebSocketConnection? Connection { get; set; } = null;

			#endregion


			#region properties

			public bool IsDisposed {
				get {
					return this.resumeEvent == null;
				}
			}

			#endregion


			#region creation & disposal

			public WaitingRequest() {
			}

			public virtual void Dispose() {
				// Do not dispose this.Connection.
				// Its lifetime is managed/monitored by the Channel object.

				// dispose this.resumeEvent
				ManualResetEventSlim? e = Interlocked.Exchange(ref this.resumeEvent, null);
				if (e != null) {
					e.Dispose();
				}
			}

			#endregion


			#region methods

			protected ManualResetEventSlim EnsureNotDisposed() {
				// check state
				ManualResetEventSlim? e = this.resumeEvent;
				if (e == null) {
					throw new ObjectDisposedException(null);
				}

				return e;
			}

			public bool Wait(int millisecondsTimeout, CancellationToken cancellationToken) {
				return EnsureNotDisposed().Wait(millisecondsTimeout, cancellationToken);
			}

			public void Resume() {
				EnsureNotDisposed().Set();
			}

			#endregion
		}

		#endregion


		#region data

		private readonly object instanceLocker = new object();


		public readonly string Key;

		public readonly int MaxConnectionCount;

		private int connectionCount = 0;

		private Queue<WebSocketConnection> readyConnectionQueue = new Queue<WebSocketConnection>();

		private Queue<WaitingRequest> waitingRequestQueue = new Queue<WaitingRequest>();

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

			// initialize members
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
		public async Task<bool> RegisterConnectionAsync(WebSocketConnection connection) {
			// check argument
			if (connection == null) {
				throw new ArgumentNullException(nameof(connection));
			}

			bool registered = false;
			try {
				NegotiateResponse response = new NegotiateResponse(NegotiateStatus.Error, NegotiateResponse.StandardMessages.Error);
				Debug.Assert(response.Status == NegotiateStatus.Error);

				// check connection state
				if (connection.State != WebSocketState.Open) {
					throw new ArgumentException("It is not in open state.", nameof(connection));
				}

				try {
					// check state
					lock (this.instanceLocker) {
						Debug.Assert(0 <= this.connectionCount);
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

					// add the connection to the ready connection queue
					// Add the connection after send the response,
					// not to be used before the negotiation completes.
					ReleaseConnection(connection);
				} catch {
					// send the failed negotiate response
					// Note that the response has been sent if response.Status is Succeeded.
					if (response.Status != NegotiateStatus.Succeeded) {
						await connection.SendJsonAsync<NegotiateResponse>(response);
					}

					// roll back the connection count
					// Note that the connection count was not incremented in TooManyConnection error.
					if (response.Status != NegotiateStatus.TooManyConnection) {
						lock (this.instanceLocker) {
							--this.connectionCount;
						}
					}

					await connection.CloseAsync();
					throw;
				}
				registered = true;
			} catch {
				// TODO: logging
				connection.Dispose();
				Debug.Assert(registered == false);
			}

			return registered;
		}

		public ValueTask ProcessMessageAsync(Func<SendMessageStream, ValueTask> sender, Func<ReceiveMessageStream, ValueTask> receiver) {
			throw new NotImplementedException();
		}

		/// <summary>
		/// Sends a text request to a web socket connection in the channel,
		/// and receives a text response.
		/// </summary>
		/// <param name="request">
		/// A text request to be sent.
		/// </param>
		/// <param name="millisecondsTimeout">
		/// The number of milliseconds to wait, or Infinite (-1) to wait indefinitely.
		/// It works only when it is waiting for an available connection.
		/// Timeout in communication is controled by the settings of the web connection.
		/// </param>
		/// <param name="cancellationToken">
		/// A cancellation token to observe while waiting for the task to complete.
		/// It works only when it is waiting for an available connection.
		/// Its communication on a web socket cannot be canceled.
		/// </param>
		/// <returns>The response text.</returns>
		public async ValueTask<string> ProcessTextAsync(string request, int millisecondsTimeout = Timeout.Infinite, CancellationToken cancellationToken = default(CancellationToken)) {
			// check argument
			if (request == null) {
				throw new ArgumentNullException(nameof(request));
			}

			// get a ready connection and communicate using it
			WebSocketConnection connection = await GetConnectionAsync(millisecondsTimeout, cancellationToken);
			try {
				await connection.SendTextAsync(request);
				return await connection.ReceiveTextAsync();
			} catch (Exception) {
				// TODO: logging
				throw;
			} finally {
				ReleaseConnection(connection);
			}
		}

		/// <summary>
		/// Sends a JSON request to a web socket connection in the channel,
		/// and receives a JSON response.
		/// JSON objects are serialized/deserialized by UTF8JSON.
		/// </summary>
		/// <typeparam name="TRequest">
		/// The type of request object.
		/// </typeparam>
		/// <typeparam name="TResponse">
		/// The type of response object.
		/// </typeparam>
		/// <param name="request">
		/// A text request to be sent.
		/// </param>
		/// <param name="millisecondsTimeout">
		/// The number of milliseconds to wait, or Infinite (-1) to wait indefinitely.
		/// It works only when it is waiting for an available connection.
		/// Timeout in communication is controled by the settings of the web connection.
		/// </param>
		/// <param name="cancellationToken">
		/// A cancellation token to observe while waiting for the task to complete.
		/// It works only when it is waiting for an available connection.
		/// Its communication on a web socket cannot be canceled.
		/// </param>
		/// <returns>The response JSON.</returns>
		public async ValueTask<TResponse> ProcessJsonAsync<TRequest, TResponse>(TRequest request, int millisecondsTimeout = Timeout.Infinite, CancellationToken cancellationToken = default(CancellationToken)) {
			// check argument
			if (request == null) {
				throw new ArgumentNullException(nameof(request));
			}

			// get a ready connection and communicate using it
			WebSocketConnection connection = await GetConnectionAsync(millisecondsTimeout, cancellationToken);
			try {
				await connection.SendJsonAsync<TRequest>(request);
				return await connection.ReceiveJsonAsync<TResponse>();
			} catch (Exception) {
				// TODO: logging
				throw;
			} finally {
				ReleaseConnection(connection);
			}
		}

		#endregion


		#region privates

		private void UnregisterConnectionNTS(WebSocketConnection connection) {
			// unregister the connection
			Debug.Assert(0 < this.connectionCount);
			--this.connectionCount;

			TaskUtil.RunningTaskTable.MonitorTask(() => { connection.Dispose(); });
		}

		private async ValueTask<WebSocketConnection> GetConnectionAsync(int millisecondsTimeOut, CancellationToken cancellationToken) {
			WebSocketConnection? connection = null;
			WaitingRequest waitingRequest;
			Task<bool> waitingTask;

			lock (this.instanceLocker) {
				// check state
				if (this.connectionCount <= 0) {
					throw new InvalidOperationException("There is no connection to handle the request.");
				}

				// try to get a connection from the ready connection queue
				while (this.readyConnectionQueue.TryDequeue(out connection)) {
					if (connection.State == WebSocketState.Open) {
						return connection;
					} else {
						// unregister the closed connection
						UnregisterConnectionNTS(connection);
					}
				}

				// There is no ready connection at this point.
				// add this request to the waiting request queue
				// to wait until a connection completes its current task and become ready 
				Debug.Assert(connection == null);
				waitingRequest = new WaitingRequest();
				this.waitingRequestQueue.Enqueue(waitingRequest);
				waitingTask = Task<bool>.Run(() => waitingRequest.Wait(millisecondsTimeOut, cancellationToken));
			}

			// wait for a ready connection
			// Note that you must wait for the task outside of the lock (this.instanceLocker) scope to avoid deadlock.
			await waitingTask;
			if (waitingRequest.Connection != null) {
				return waitingRequest.Connection;
			} else {
				if (waitingTask.IsCanceled) {
					throw new OperationCanceledException();
				} else {
					throw new TimeoutException();
				}
			}
		}

		private void ReleaseConnection(WebSocketConnection connection) {
			// check argument
			Debug.Assert(connection != null);
			if (connection.State != WebSocketState.Open) {
				// unregister the unavailable connection
				lock (this.instanceLocker) {
					UnregisterConnectionNTS(connection);
				}
				return;
			}

			// recycle the connection
			lock (this.instanceLocker) {
				// try to dequeue a waiting request
				WaitingRequest? waitingRequest;
				while (this.waitingRequestQueue.TryDequeue(out waitingRequest)) {
					// wasn't the request timeouted nor canceled?
					if (waitingRequest.IsDisposed == false) {
						break;
					}
				}

				// recycle the connection according to the situation
				if (waitingRequest == null) {
					// There is no waiting request.
					// return it to the connection queue
					this.readyConnectionQueue.Enqueue(connection);
				} else {
					// There is a waiting request.
					// pass it to the waiting request
					waitingRequest.Connection = connection;
					waitingRequest.Resume();
				}
			}
		}

		#endregion
	}
}
