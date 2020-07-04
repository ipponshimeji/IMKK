using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using IMKK.WebSockets;
using IMKK.Communication;
using IMKK.Server.Storage;

namespace IMKK.Server {
	public class IMMKServer: IDisposable {
		#region data

		private object instanceLocker = new object();

		private Dictionary<string, Channel>? channels = null;

		private bool disposed = false;

		#endregion


		#region creation & disposal

		public IMMKServer() {
		}

		protected virtual void Initialize(IIMKKStorage storage) {
			// check argument
			if (storage == null) {
				throw new ArgumentNullException(nameof(storage));
			}

			// create channels
			Dictionary<string, Channel> channels = new Dictionary<string, Channel>();
			foreach (ChannelInfo info in storage.Channels) {
				Channel channel = new Channel(info);
				Debug.Assert(channel.Key != null);
				channels.Add(channel.Key, channel);
			}

			// initialize state
			lock (this.instanceLocker) {
				// check state
				if (this.channels != null) {
					throw new InvalidOperationException("The IMKK server is already initialized.");
				}
				if (this.disposed) {
					throw new ObjectDisposedException(null);
				}

				// initialize
				this.channels = channels;
			}
		}

		public virtual void Dispose() {
			Dictionary<string, Channel>? channels;

			lock (this.instanceLocker) {
				this.disposed = true;
				channels = Interlocked.Exchange(ref this.channels, null);
			}

			if (channels != null) {
				// TODO : terminate communications
			}
		}

		#endregion


		#region methods

		// This method is NTS (Not Thread Safe).
		// Use it in a lock (this.instanceLocker) scope.
		protected Dictionary<string, Channel> EnsureNotDisposedNTS() {
			// check state
			Dictionary<string, Channel>? channels = this.channels;
			if (channels == null) {
				if (this.disposed) {
					throw new ObjectDisposedException(null);
				} else {
					throw new InvalidOperationException("The IMKK server is not initialized.");
				}
			}

			return channels;
		}


		public void AddConnection(string key, WebSocketConnection connection) {
			// check arguments
			if (key == null) {
				throw new ArgumentNullException(nameof(key));
			}
			if (connection == null) {
				throw new ArgumentNullException(nameof(connection));
			}

			// add the connection to the channel table
			lock (this.instanceLocker) {
				Channel channel = GetChannel(key);
				channel.AddConnection(connection);
			}
		}

		public async ValueTask NegotiateAndAddConnectionAsync(WebSocket webSocket) {
			// check arguments
			if (webSocket == null) {
				throw new ArgumentNullException(nameof(webSocket));
			}

			// negotiate and add the web socket to the channel table
			WebSocketConnection connection = new WebSocketConnection(webSocket);
			try {
				NegotiateStatus status = NegotiateStatus.Succeeded;
				string? message = null;

				NegotiateRequest request = await connection.ReceiveJsonAsync<NegotiateRequest>();
				try {
					if (string.IsNullOrEmpty(request.Key)) {
						status = NegotiateStatus.InvalidKey;
					} else {
						AddConnection(request.Key, connection);
						// TODO: error handling
					}

				} finally {
					NegotiateResponse response = new NegotiateResponse(status, message);
					await connection.SendJsonAsync<NegotiateResponse>(response);
				}
			} catch {
				connection.Dispose();
				throw;
			}
		}

		public ValueTask<string> ProcessTextAsync(string key, string request, int millisecondsTimeout = Timeout.Infinite, CancellationToken cancellationToken = default(CancellationToken)) {
			return GetChannel(key).ProcessTextAsync(request, millisecondsTimeout, cancellationToken);
		}

		public ValueTask<TResponse> ProcessJsonAsync<TRequest, TResponse>(string key, TRequest request, int millisecondsTimeout = Timeout.Infinite, CancellationToken cancellationToken = default(CancellationToken)) {
			return GetChannel(key).ProcessJsonAsync<TRequest, TResponse>(request, millisecondsTimeout, cancellationToken);
		}

		#endregion


		#region privates

		private Channel GetChannel(string key) {
			// check argument
			if (key == null) {
				throw new ArgumentNullException(nameof(key));
			}

			lock (this.instanceLocker) {
				// check state
				Dictionary<string, Channel> channels = EnsureNotDisposedNTS();

				// get the specified channel
				Channel? channel;
				if (channels.TryGetValue(key, out channel) == false) {
					throw new InvalidOperationException("The channel of the key is not found.");
				}
				return channel;
			}
		}

		#endregion
	}
}
