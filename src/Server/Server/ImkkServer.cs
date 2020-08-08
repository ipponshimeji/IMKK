using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Imkk.WebSockets;
using Imkk.Communication;


namespace Imkk.Server {
	public class ImkkServer: IDisposable {
		#region types

		public static class ConfigNames {
			#region constants

			public const string Channels = "channels";

			#endregion
		}

		#endregion


		#region data

		private object instanceLocker = new object();

		private Dictionary<string, Channel>? channels = null;

		private bool disposed = false;

		#endregion


		#region creation & disposal

		public ImkkServer() {
		}

		protected virtual void Initialize(IConfigurationSection config, ILogger? logger) {
			// check argument
			if (config == null) {
				throw new ArgumentNullException(nameof(config));
			}
			// logger can be null

			// create channels
			Dictionary<string, Channel> channels = new Dictionary<string, Channel>();
			foreach (IConfigurationSection channelConfig in config.GetSection(ConfigNames.Channels).GetChildren()) {
				Channel channel = CreateChannel(channelConfig, logger);
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


		public static ImkkServer Create(IConfigurationSection config, ILogger? logger) {
			// create and initialize a server
			ImkkServer server = new ImkkServer();
			server.Initialize(config, logger);
			return server;
		}

		public static ImkkServer Create(IConfiguration config, string key, ILogger? logger) {
			// check arguments
			if (config == null) {
				throw new ArgumentNullException(nameof(config));
			}
			if (key == null) {
				throw new ArgumentNullException(nameof(key));
			}

			// get configuration section to be used
			IConfigurationSection subConfig = config.GetSection(key);

			// create and initialize a server
			ImkkServer server = new ImkkServer();
			server.Initialize(subConfig, logger);
			return server;
		}

		public virtual void Dispose() {
			Dictionary<string, Channel>? channels;

			lock (this.instanceLocker) {
				this.disposed = true;
				channels = Interlocked.Exchange(ref this.channels, null);
			}

			// dispose channels
			if (channels != null) {
				foreach (Channel channel in channels.Values) {
					TaskUtil.RunningTaskTable.MonitorTask(() => {
						channel.Dispose();
					});
				}
			}
		}

		#endregion


		#region methods

		/// <remarks>
		/// The ownership of the webSocket is moved to this srever. 
		/// </remarks>
		public async ValueTask NegotiateAndAddConnectionAsync(WebSocket webSocket) {
			// check arguments
			if (webSocket == null) {
				throw new ArgumentNullException(nameof(webSocket));
			}

			// negotiate and add the web socket to the channel table
			Channel? channel = null;
			WebSocketConnection connection = new WebSocketConnection(webSocket);
			try {
				NegotiateResponse response = new NegotiateResponse(NegotiateStatus.Error, NegotiateResponse.StandardMessages.Error);

				// receive the negotiate request
				NegotiateRequest request = await connection.ReceiveJsonAsync<NegotiateRequest>();
				try {
					// check the key
					string? key = request.Key;
					if (string.IsNullOrEmpty(key) == false) {
						lock (this.instanceLocker) {
							channel = FindChannelNTS(key);
						}
					}
					if (channel == null) {
						response.Status = NegotiateStatus.InvalidKey;
						response.Message = NegotiateResponse.StandardMessages.InvalidKey;
						throw new InvalidOperationException(response.Message);
					}
				} catch {
					await connection.SendJsonAsync<NegotiateResponse>(response);
					await connection.CloseAsync();
					throw;
				}
			} catch {
				// TODO: logging
				connection.Dispose();
				return;		// end the task
			}

			// add the connection to the channel
			// The channel sends the negotiate response.
			// The ownership of the connection is given to the channel.
			TaskUtil.RunningTaskTable.MonitorTask(channel.RegisterConnectionAsync(connection));
		}

		public ValueTask<string> CommunicateTextAsync(string key, string request, int millisecondsTimeout = Timeout.Infinite, CancellationToken cancellationToken = default(CancellationToken)) {
			return GetChannel(key).ProcessTextAsync(request, millisecondsTimeout, cancellationToken);
		}

		public ValueTask<TResponse> CommunicateJsonAsync<TRequest, TResponse>(string key, TRequest request, int millisecondsTimeout = Timeout.Infinite, CancellationToken cancellationToken = default(CancellationToken)) {
			return GetChannel(key).ProcessJsonAsync<TRequest, TResponse>(request, millisecondsTimeout, cancellationToken);
		}

		#endregion


		#region methods - for derived classes

		// xxxNTS methods are NTS (Not Thread Safe).
		// Use them in a lock (this.instanceLocker) scope.

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

		protected Channel? FindChannelNTS(string key) {
			// check argument
			if (key == null) {
				throw new ArgumentNullException(nameof(key));
			}
			
			// check state
			Dictionary<string, Channel> channels = EnsureNotDisposedNTS();

			// get the specified channel
			Channel? channel;
			channels.TryGetValue(key, out channel);
			return channel;
		}

		protected Channel GetChannelNTS(string key) {
			// get the specified channel
			Channel? channel = FindChannelNTS(key);
			if (channel == null) {
				throw new InvalidOperationException("The channel of the key is not found.");
			}
			return channel;
		}

		protected Channel GetChannel(string key) {
			lock (this.instanceLocker) {
				return GetChannelNTS(key);
			}
		}

		#endregion


		#region overridables

		protected virtual Channel CreateChannel(IConfigurationSection config, ILogger? logger) {
			return new Channel(config, logger);
		}

		#endregion
	}
}
