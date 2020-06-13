using System;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Utf8Json;


namespace IMKK.Lib {
	public static class WebSocketUtil {
		#region types

		public class MessageStream: Stream {
			#region data

			protected WebSocket? WebSocket { get; private set; }

			#endregion


			#region creation & disposal

			protected MessageStream(WebSocket webSocket) {
				// check argument
				if (webSocket == null) {
					throw new ArgumentNullException(nameof(webSocket));
				}

				// initialize member
				this.WebSocket = webSocket;
			}

			protected override void Dispose(bool disposing) {
				// dispose this class level
				// Do not close this.WebSocket.
				// This is not end of the connection but end of an message.
				this.WebSocket = null;

				// dispose the base class level
				base.Dispose(disposing);
			}

			#endregion


			#region methods

			protected WebSocket EnsureNotDisposed() {
				WebSocket? webSocket = this.WebSocket;
				if (webSocket == null) {
					throw new ObjectDisposedException(null);
				}

				return webSocket;
			}

			#endregion


			#region overrides

			public override bool CanRead => false;

			public override bool CanSeek => false;

			public override bool CanWrite => false;

			public override long Length => throw new NotSupportedException();

			public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

			public override void Flush() {
				throw new NotSupportedException();
			}

			public override int Read(byte[] buffer, int offset, int count) {
				throw new NotSupportedException();
			}

			public override long Seek(long offset, SeekOrigin origin) {
				throw new NotSupportedException();
			}

			public override void SetLength(long value) {
				throw new NotSupportedException();
			}

			public override void Write(byte[] buffer, int offset, int count) {
				throw new NotSupportedException();
			}

			#endregion
		}

		public sealed class ReceiveMessageStream: MessageStream {
			#region constants

			public const int MinBufferSize = 32;

			public const int MaxBufferSize = 16 * 1024 * 1024;

			public const int DefaultBufferSize = 1024;

			#endregion


			#region data

			private byte[] buffer;

			private int next = 0;

			private int limit = 0;

			private bool endOfMessage = false;

			public WebSocketMessageType MessageType { get; private set; } = WebSocketMessageType.Close;

			#endregion


			#region creation & disposal

			public ReceiveMessageStream(WebSocket webSocket, int bufferSize = DefaultBufferSize): base(webSocket) {
				// check argument
				if (bufferSize < MinBufferSize || MaxBufferSize < bufferSize) {
					throw new ArgumentOutOfRangeException(nameof(bufferSize));
				}

				// initialize member
				this.buffer = new byte[bufferSize];
			}

			protected override void Dispose(bool disposing) {
				// dispose this class level
				if (disposing) {
					// read to the end of the message
					WebSocket? webSocket = this.WebSocket;
					if (webSocket != null && webSocket.State == WebSocketState.Open) {
						bool endOfMessage = this.endOfMessage;
						while (endOfMessage == false) {
							ValueWebSocketReceiveResult result = webSocket.ReceiveAsync(new Memory<byte>(this.buffer), CancellationToken.None).Sync();
							endOfMessage = result.EndOfMessage;
						}
						this.endOfMessage = true;
					}
				}

				// dispose the base class level
				base.Dispose(disposing);
			}

			#endregion


			#region overrides

			public override bool CanRead => true;

			public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
				// check state
				WebSocket webSocket = EnsureNotDisposed();

				// update the internal buffer if there is no more data on it
				if (this.limit <= this.next) {
					if (this.endOfMessage) {
						// end of the message
						return 0;
					}
					ValueWebSocketReceiveResult result = await webSocket.ReceiveAsync(new Memory<byte>(this.buffer), cancellationToken);
					this.MessageType = result.MessageType;
					this.endOfMessage = result.EndOfMessage;
					if (result.MessageType == WebSocketMessageType.Close) {
						Debug.Assert(this.limit == 0);  // is this the first receiving?
						throw new EndOfStreamException();
					}					
					this.next = 0;
					this.limit = result.Count;
				}

				// copy data from the internal buffer
				int len = Math.Min(count, this.limit - this.next);
				if (0 < len) {
					new ReadOnlyMemory<byte>(this.buffer, this.next, len).CopyTo(new Memory<byte>(buffer, offset, count));
					this.next += len;
				} else {
					Debug.Assert(this.endOfMessage);
				}

				return len;
			}

			public override int Read(byte[] buffer, int offset, int count) {
				try {
					return ReadAsync(buffer, offset, count, CancellationToken.None).Sync();
				} catch (AggregateException exception) {
					Exception? innerException = exception.InnerException;
					if (innerException != null) {
						throw innerException;
					} else {
						throw;
					}
				}
			}

			#endregion
		}

		public sealed class SendMessageStream: MessageStream {
			#region data

			public WebSocketMessageType MessageType { get; private set; }

			#endregion


			#region creation & disposal

			public SendMessageStream(WebSocket webSocket, WebSocketMessageType messageType) : base(webSocket) {
				// initialize member
				this.MessageType = messageType;
			}

			protected override void Dispose(bool disposing) {
				// dispose this class level
				if (disposing) {
					// write end of the message marker
					WebSocket? webSocket = this.WebSocket;
					if (webSocket != null && webSocket.State == WebSocketState.Open) {
						webSocket.SendAsync(ReadOnlyMemory<byte>.Empty, this.MessageType, true, CancellationToken.None).Sync();
					}
				}

				// dispose the base class level
				base.Dispose(disposing);
			}

			#endregion


			#region overrides

			public override bool CanWrite => true;

			public override void Flush() {
				// do nothing
			}

			public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
				// check state
				WebSocket webSocket = EnsureNotDisposed();

				// send the data
				await webSocket.SendAsync(new ReadOnlyMemory<byte>(buffer, offset, count), this.MessageType, false, cancellationToken);
			}

			public override void Write(byte[] buffer, int offset, int count) {
				try {
					WriteAsync(buffer, offset, count, CancellationToken.None).Sync();
				} catch (AggregateException exception) {
					Exception? innerException = exception.InnerException;
					if (innerException != null) {
						throw innerException;
					} else {
						throw;
					}
				}
			}

			#endregion
		}

		#endregion


		#region methods

		public static T ReceiveJson<T>(this WebSocket webSocket) {
			// check argument
			if (webSocket == null) {
				throw new ArgumentNullException(nameof(webSocket));
			}

			// read a JSON value from the web socket
			using (Stream stream = new ReceiveMessageStream(webSocket)) {
				return JsonSerializer.Deserialize<T>(stream);
			}
		}

		public static async ValueTask<T> ReceiveJsonAsync<T>(this WebSocket webSocket) {
			// check argument
			if (webSocket == null) {
				throw new ArgumentNullException(nameof(webSocket));
			}

			// read a JSON value from the web socket
			using (Stream stream = new ReceiveMessageStream(webSocket)) {
				return await JsonSerializer.DeserializeAsync<T>(stream);
			}
		}

		public static void SendJson<T>(this WebSocket webSocket, T value) {
			// check argument
			if (webSocket == null) {
				throw new ArgumentNullException(nameof(webSocket));
			}

			// read a JSON value from the web socket
			using (Stream stream = new SendMessageStream(webSocket, WebSocketMessageType.Text)) {
				JsonSerializer.Serialize<T>(stream, value);
			}
		}

		public static async ValueTask SendJsonAsync<T>(this WebSocket webSocket, T value) {
			// check argument
			if (webSocket == null) {
				throw new ArgumentNullException(nameof(webSocket));
			}

			// read a JSON value from the web socket
			using (Stream stream = new SendMessageStream(webSocket, WebSocketMessageType.Text)) {
				await JsonSerializer.SerializeAsync<T>(stream, value);
			}
		}

		#endregion
	}
}
