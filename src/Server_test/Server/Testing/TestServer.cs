using System;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Imkk.Server.Configurations;
using Imkk.Communication;
using Imkk.Testing;
using Imkk.WebSockets;

namespace Imkk.Server.Testing {
	public class TestServer {
		#region data

		public const string SampleKey0 = "abcdefghijklmn";

		public const string SampleKey1 = "UVWXYZ012345";


		// use global RunnintTaskTable as the RunningTaskTable for this server.
		protected readonly RunningTaskTable RunningTaskTable = TaskUtil.RunningTaskTable;

		private readonly object instanceLocker = new object();

		private ImkkServer? imkkServer = null;

		private HttpListener? httpListener = null;

		private Task? listeningTask = null;

		#endregion


		#region creation

		public TestServer() {
		}

		#endregion


		#region methods

		public static IImkkServerConfig CreateSampleConfig() {
			return new ImkkServerConfig(
				new ChannelConfig[] {
					new ChannelConfig(SampleKey0),
					new ChannelConfig(SampleKey1)
				}
			);
		}


		public void Start(IImkkServerConfig? imkkConfig = null) {
			// check arguments
			if (imkkConfig == null) {
				imkkConfig = new ImkkServerConfig();
			}

			lock (this.instanceLocker) {
				// check state
				if (this.imkkServer != null) {
					throw new InvalidOperationException("The server has been started.");
				}

				// create a IMKK server and start listening
				Task task;
				HttpListener listener;
				ImkkServer server = CreateIMKKServer(imkkConfig);
				try {
					// start the server
					listener = WebSocketsUtil.StartListening();
					try {
						task = Listen(listener, server);
					} catch {
						listener.Close();
						throw;
					}
				} catch {
					server.Dispose();
					throw;
				}

				// update state
				this.imkkServer = server;
				this.httpListener = listener;
				this.listeningTask = task;
			}
		}

		public void Stop() {
			ImkkServer? imkkServer;
			HttpListener? listener;
			Task? listeningTask;

			lock (this.instanceLocker) {
				// check state
				if (this.imkkServer == null) {
					// already stopped
					return;
				}

				// old
				listeningTask = Interlocked.Exchange(ref this.listeningTask, null);
				listener = Interlocked.Exchange(ref this.httpListener, null);
				imkkServer = Interlocked.Exchange(ref this.imkkServer, null);
			}

			// stop the listener
			Debug.Assert(listeningTask != null);
			Debug.Assert(listener != null);
			Debug.Assert(imkkServer != null);
			try {
				listener.Stop();
				listeningTask.Wait();
			} finally {
				listener.Close();
				imkkServer.Dispose();
			}
		}

		public async ValueTask<WebSocketConnection> Connect() {
			// check state
			HttpListener? listener = this.httpListener;
			if (listener == null) {
				throw CreateNotServingException();
			}

			ClientWebSocket webSocket = await listener.ConnectWebSocketAsync();
			try {
				return new WebSocketConnection(webSocket);
			} catch {
				webSocket.Dispose();
				throw;
			}
		}

		public async ValueTask<WebSocketConnection> ConnectAndNegotiate(NegotiateRequest request) {
			// check argument
			if (request == null) {
				throw new ArgumentNullException(nameof(request));
			}

			// connect
			WebSocketConnection connection = await Connect();
			try {
				// negotiate
				await connection.SendJsonAsync<NegotiateRequest>(request);
				NegotiateResponse response = await connection.ReceiveJsonAsync<NegotiateResponse>();
				if (response.Status != NegotiateStatus.Succeeded) {
					throw new NegotiateException(response);
				}
			} catch {
				await connection.CloseAsync();
				connection.Dispose();
				throw;
			}

			return connection;
		}

		protected static InvalidOperationException CreateNotServingException() {
			return new InvalidOperationException("The server is not serving now.");
		}

		#endregion


		#region overridables

		protected virtual ImkkServer CreateIMKKServer(IImkkServerConfig config) {
			// check arguments
			Debug.Assert(config != null);

			return ImkkServer.Create(config);
		}

		protected virtual async Task Listen(HttpListener httpListener, ImkkServer imkkServer) {
			// check arguments
			if (httpListener == null) {
				throw new ArgumentNullException(nameof(httpListener));
			}
			if (imkkServer == null) {
				throw new ArgumentNullException(nameof(imkkServer));
			}

			do {
				try {
					WebSocket webSocket = await httpListener.AcceptWebSocketAsync();
					RunningTaskTable.MonitorTask(imkkServer.NegotiateAndAddConnectionAsync(webSocket));
				} catch (OperationCanceledException) {
					// the httpListener stopped listening
					break;
				} catch {
					// try to accept next 
				}
			} while (true);
		}

		#endregion
	}
}
