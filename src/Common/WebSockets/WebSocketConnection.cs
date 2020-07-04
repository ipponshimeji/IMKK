using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utf8Json;


namespace IMKK.WebSockets {
	public class WebSocketConnection: IDisposable {
		#region types

		private sealed class InternalReceiveMessageStream: ReceiveMessageStream {
			#region data

			public readonly WebSocketConnection Owner;

			#endregion


			#region creation & disposal

			public InternalReceiveMessageStream(WebSocketConnection owner, WebSocket webSocket, int bufferSize = DefaultBufferSize) : base(webSocket, bufferSize) {
				// check argument
				if (owner == null) {
					throw new ArgumentNullException(nameof(owner));
				}

				// initialize member
				this.Owner = owner;
			}

			#endregion


			#region overrides

			public override async ValueTask ReceiveAsync(CancellationToken cancellationToken) {
				// check state
				bool endOfMessage = this.EndOfMessage;

				// call implementation of the base class
				try {
					await base.ReceiveAsync(cancellationToken);
				} finally {
					// notify the owner that the whole message has been received,
					// if the end of message is received.
					if (endOfMessage == false && this.EndOfMessage) {
						this.Owner.OnReceiveMessageCompleted(this, this.MessageType == WebSocketMessageType.Close);
					}
				}
			}

			#endregion
		}

		public sealed class InternalSendMessageStream: SendMessageStream {
			#region data

			public readonly WebSocketConnection Owner;

			#endregion


			#region creation & disposal

			public InternalSendMessageStream(WebSocketConnection owner, WebSocket webSocket, WebSocketMessageType messageType) : base(webSocket, messageType) {
				// check argument
				if (owner == null) {
					throw new ArgumentNullException(nameof(owner));
				}

				// initialize member
				this.Owner = owner;
			}


			protected override void Dispose(bool disposing) {
				try {
					// dispose the base class level
					// the end of message is sent in this call
					base.Dispose(disposing);
				} finally {
					// notify the owner that the whole message has been sent
					this.Owner.OnSendMessageCompleted(this);
				}
			}

			#endregion
		}

		#endregion


		#region data

		// UTF8 without BOM
		public static readonly Encoding TextMessageEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);


		private object instanceLocker = new object();

		private WebSocket? webSocket = null;

		public readonly int ReceiveBufferSize;

		/// <summary>
		/// The send stream which is provided by SendMessage() method and
		/// being used to send a message currently.
		/// </summary>
		private InternalSendMessageStream? currentSendStream = null;

		private Task<InternalReceiveMessageStream>? receiveTask = null;

		/// <summary>
		/// The receive stream which is provided by ReceiveMessageAsync() method and
		/// being used to receive a message currently.
		/// </summary>
		private InternalReceiveMessageStream? currentReceiveStream = null;

		#endregion


		#region properties

		public WebSocketState State => (webSocket == null ? WebSocketState.None : webSocket.State);

		public bool Receiving => (this.receiveTask != null);

		#endregion


		#region event

		// TODO MessageReceiving, ConnectionClosed

		#endregion


		#region creation & disposal

		public WebSocketConnection(WebSocket webSocket, int receiveBufferSize = ReceiveMessageStream.DefaultBufferSize) {
			// check argument
			if (webSocket == null) {
				throw new ArgumentNullException(nameof(webSocket));
			}
			if (receiveBufferSize < ReceiveMessageStream.MinBufferSize || ReceiveMessageStream.MaxBufferSize < receiveBufferSize) {
				throw new ArgumentOutOfRangeException(nameof(receiveBufferSize));
			}

			// initialize members
			this.webSocket = webSocket;
			this.ReceiveBufferSize = receiveBufferSize;
		}

		public virtual void Dispose() {
			// dispose resources
			InternalSendMessageStream? sendStream;
			InternalReceiveMessageStream? receiveStream;
			WebSocket? ws;

			lock (this.instanceLocker) {
				sendStream = Interlocked.Exchange(ref this.currentSendStream, null);
				receiveStream = Interlocked.Exchange(ref this.currentReceiveStream, null);
				ws = Interlocked.Exchange(ref this.webSocket, null);
			}

			if (sendStream != null) {
				sendStream.Dispose();
			}
			if (receiveStream != null) {
				receiveStream.Dispose();
			}
			if (ws != null) {
				// launch the disposing operation on other thread because WebSocket.CloseAsync() is async
				TaskUtil.RunningTaskTable.MonitorTask(async () => {
					try {
						if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived) {
							await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
						}
						ws.Dispose();
					} catch {
						// TODO: log
					}
				});
			}
		}


		public static async ValueTask<WebSocketConnection> AcceptAsync(HttpListener listener, string? subProtocol = null, TimeSpan? keepAliveInterval = null, bool start = true) {
			// check argument
			if (listener == null) {
				throw new ArgumentNullException(nameof(listener));
			}

			// accept a request
			HttpListenerContext context = await listener.GetContextAsync();
			if (context.Request.IsWebSocketRequest == false) {
				context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
				context.Response.Close();
				throw new NotSupportedException("Not a web socket request.");
			}

			// create a WebSocketConnection object
			TimeSpan actualKeepAliveInterval = keepAliveInterval ?? WebSocket.DefaultKeepAliveInterval;
			WebSocketConnection connection = new WebSocketConnection((await context.AcceptWebSocketAsync(subProtocol, actualKeepAliveInterval)).WebSocket);
			if (start) {
				connection.StartReceiving();
			}

			return connection;
		}

		public static async ValueTask<WebSocketConnection> ConnectAsync(Uri uri, CancellationToken cancellationToken = default(CancellationToken), int receiveBufferSize = ReceiveMessageStream.DefaultBufferSize, bool start = true) {
			// check argument
			if (uri == null) {
				throw new ArgumentNullException(nameof(uri));
			}

			// connect to the listener
			ClientWebSocket ws = new ClientWebSocket();
			try {
				await ws.ConnectAsync(uri, cancellationToken);
				WebSocketConnection connection = new WebSocketConnection(ws, receiveBufferSize);
				if (start) {
					connection.StartReceiving();
				}
				return connection;
			} catch {
				((IDisposable)ws).Dispose();
				throw;
			}
		}

		#endregion


		#region methods

		protected WebSocket EnsureNotDisposed() {
			WebSocket? webSocket = this.webSocket;
			if (webSocket == null) {
				throw new ObjectDisposedException(null);
			}

			return webSocket;
		}

		public void StartReceiving() {
			// check state
			WebSocket webSocket = EnsureNotDisposed();
			if (webSocket.State != WebSocketState.Open) {
				throw new InvalidOperationException();
			}

			// start the receive task
			StartReceiveTask();
		}

		public async Task CloseAsync(CancellationToken cancellationToken = default(CancellationToken)) {
			// check state
			WebSocket ws = EnsureNotDisposed();

			// close the WebSocket if it is alive
			if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived) {
				await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken);
			}
		}

		public SendMessageStream SendMessage(WebSocketMessageType messageType) {
			lock (this.instanceLocker) {
				// check status
				if (this.currentSendStream != null) {
					throw new InvalidOperationException("Currently another message is being sent on this connection.");
				}

				// create a stream to send a message
				WebSocket ws = EnsureNotDisposed();
				// TODO: user instance cache
				InternalSendMessageStream stream = new InternalSendMessageStream(this, ws, messageType);
				this.currentSendStream = stream;
				return stream;
			}
		}

		public async Task<ReceiveMessageStream> ReceiveMessageAsync() {
			static InvalidOperationException createConnectionClosedException() {
				return new InvalidOperationException("The connection has been closed.");
			}

			// check state
			Task<InternalReceiveMessageStream>? receiveTask;
			lock (this.instanceLocker) {
				receiveTask = this.receiveTask;
			}
			if (receiveTask == null) {
				throw createConnectionClosedException();
			}

			// wait for receiving a message
			InternalReceiveMessageStream stream = await receiveTask;
			lock (this.instanceLocker) {
				if (this.currentReceiveStream != null) {
					throw new InvalidOperationException("Currently another message is being received on this connection.");
				}
				if (this.receiveTask == null) {
					throw createConnectionClosedException();
				}
				this.currentReceiveStream = stream;
			}

			return stream;
		}

		public StreamWriter SendTextMessage() {
			return new StreamWriter(SendMessage(WebSocketMessageType.Text), TextMessageEncoding);
		}

		public async ValueTask<StreamReader> ReceiveTextMessageAsync() {
			return new StreamReader(await ReceiveMessageAsync(), TextMessageEncoding);
		}

		public async ValueTask SendTextAsync(string? text) {
			using (StreamWriter writer = SendTextMessage()) {
				await writer.WriteAsync(text);
			}
		}

		public async ValueTask<string> ReceiveTextAsync() {
			using (StreamReader reader = await ReceiveTextMessageAsync()) {
				return await reader.ReadToEndAsync();
			}
		}

		public async ValueTask SendJsonAsync<T>(T value) {
			using (SendMessageStream stream = SendMessage(WebSocketMessageType.Text)) {
				await JsonSerializer.SerializeAsync<T>(stream, value);
			}
		}

		public async ValueTask<T> ReceiveJsonAsync<T>() {
			using (ReceiveMessageStream stream = await ReceiveMessageAsync()) {
				return await JsonSerializer.DeserializeAsync<T>(stream);
			}
		}

		#endregion


		#region privates

		private void StartReceiveTask() {
			InternalReceiveMessageStream receiveMessage() {
				// check state
				WebSocket webSocket = EnsureNotDisposed();
				if (webSocket.State != WebSocketState.Open) {
					throw new InvalidOperationException();
				}

				// create a ReceiveMessageStream to read message
				InternalReceiveMessageStream stream = new InternalReceiveMessageStream(this, webSocket, this.ReceiveBufferSize);
				try {
					stream.ReceiveAsync(CancellationToken.None).Sync();
					return stream;
				} catch (EndOfStreamException) {
					// the peer sent a close request 
					try {
						// TODO: should dispose the currentSendStream if it exists?
						webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).Sync();
					} catch {
						// continue
					} finally {
						stream.Dispose();
					}
					throw;
				}
			}

			// start the task to receive a message
			Task<InternalReceiveMessageStream> task;
			lock (this.instanceLocker) {
				// check state
				if (this.receiveTask != null) {
					throw new InvalidOperationException("This object is already waiting for message.");
				}

				// create a task to wait for message
				task = new Task<InternalReceiveMessageStream>(receiveMessage);
				this.receiveTask = task;
			}
			// Note that the task must be started after it is registered to this.receiveTask.
			// this.receiveTask is referenced from OnReceiveMessageMessageCompleted() via receiveMessage().
			task.Start();
		}

		#endregion


		#region privates - for SendMessageStream and ReceiveMessageStream

		private void OnSendMessageCompleted(InternalSendMessageStream stream) {
			// check argument
			Debug.Assert(stream != null);

			// update state
			lock (this.instanceLocker) {
				if (stream == this.currentSendStream) {
					this.currentSendStream = null;
				}
			}
		}

		private void OnReceiveMessageCompleted(InternalReceiveMessageStream stream, bool closing) {
			// check argument
			Debug.Assert(stream != null);

			// update state
			lock (this.instanceLocker) {
				if (stream == this.currentReceiveStream) {
					this.currentReceiveStream = null;
					this.receiveTask = null;
					if (closing == false) {
						StartReceiveTask();
						Debug.Assert(this.receiveTask != null);
					}
				}
			}
		}

		#endregion
	}
}
