using System;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using IMKK.Server.Configurations;
using IMKK.Testing;


namespace IMKK.Server.Testing {
	public class TestServer {
		#region data

		// use global RunnintTaskTable as the RunningTaskTable for this server.
		protected readonly RunningTaskTable RunningTaskTable = TaskUtil.RunningTaskTable;

		private readonly object instanceLocker = new object();

		private IMKKServer? imkkServer = null;

		private HttpListener? httpListener = null;

		private Task? listeningTask = null;

		#endregion


		#region creation

		public TestServer() {
		}

		#endregion


		#region methods

		public void Start(IIMKKServerConfig? imkkConfig = null) {
			// check arguments
			if (imkkConfig == null) {
				imkkConfig = new IMKKServerConfig();
			}

			lock (this.instanceLocker) {
				// check state
				if (this.imkkServer != null) {
					throw new InvalidOperationException("The server has been started.");
				}

				// create a IMKK server and start listening
				Task task;
				HttpListener listener;
				IMKKServer server = CreateIMKKServer(imkkConfig);
				try {
					// start the server
					listener = WebSocketsUtil.StartListening();
					try {
						listener.Start();
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
			IMKKServer? imkkServer;
			HttpListener? listener;
			Task? listeningTask;

			lock (this.instanceLocker) {
				// check state
				if (this.imkkServer == null) {
					// already stopped
					return;
				}
				Debug.Assert(this.httpListener != null);
				Debug.Assert(this.listeningTask != null);

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

		#endregion


		#region overridables

		protected virtual IMKKServer CreateIMKKServer(IIMKKServerConfig config) {
			// check arguments
			Debug.Assert(config != null);

			return IMKKServer.Create(config);
		}

		protected virtual async Task Listen(HttpListener httpListener, IMKKServer imkkServer) {
			// check arguments
			if (httpListener == null) {
				throw new ArgumentNullException(nameof(httpListener));
			}
			if (imkkServer == null) {
				throw new ArgumentNullException(nameof(imkkServer));
			}

			while (true) {
				try {
					WebSocket webSocket = await httpListener.AcceptWebSocketAsync();
					RunningTaskTable.MonitorTask(imkkServer.NegotiateAndAddConnectionAsync(webSocket));
				} catch (OperationCanceledException) {
					// the httpListener stopped listening
					break;
				} catch {
					// try to accept next 
				}
			}
		}

		#endregion
	}
}
