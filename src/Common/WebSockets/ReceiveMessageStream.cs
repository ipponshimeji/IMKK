using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;


namespace Imkk.WebSockets {
	public class ReceiveMessageStream: MessageStream {
		#region constants

		public const int MinBufferSize = 32;

		public const int MaxBufferSize = 1 * 1024 * 1024;	// 1M

		public const int DefaultBufferSize = 1024;

		#endregion


		#region data

		private byte[] buffer;

		private int next = 0;

		private int limit = 0;

		protected bool EndOfMessage { get; private set; } = false;

		public WebSocketMessageType? MessageType { get; private set; } = null;

		#endregion


		#region properties

		public int BufferSize => (this.buffer == null ? 0: this.buffer.Length);

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
					bool endOfMessage = this.EndOfMessage;
					while (endOfMessage == false) {
						ValueWebSocketReceiveResult result = webSocket.ReceiveAsync(new Memory<byte>(this.buffer), CancellationToken.None).Sync();
						endOfMessage = result.EndOfMessage;
					}
					this.EndOfMessage = true;
				}
			}

			// dispose the base class level
			base.Dispose(disposing);
		}

		#endregion


		#region overrides

		public override bool CanRead => true;

		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
			// update the internal buffer if there is no more data on it
			if (this.limit <= this.next) {
				await ReceiveAsync(cancellationToken);
			}

			// copy data from the internal buffer
			int len = Math.Min(count, this.limit - this.next);
			if (0 < len) {
				new ReadOnlyMemory<byte>(this.buffer, this.next, len).CopyTo(new Memory<byte>(buffer, offset, count));
				this.next += len;
			} else {
				Debug.Assert(this.EndOfMessage);
			}

			return len;
		}

		public override int Read(byte[] buffer, int offset, int count) {
			return ReadAsync(buffer, offset, count, CancellationToken.None).Sync();
		}

		#endregion


		#region overridables

		public virtual async ValueTask ReceiveAsync(CancellationToken cancellationToken) {
			// check state
			WebSocket webSocket = EnsureNotDisposed();
			if (this.EndOfMessage) {
				// end of the message
				return;
			}
			if (this.next < this.limit) {
				throw new InvalidOperationException("Unread data remain on the buffer.");
			}

			// receive the next data from the web socket
			ValueWebSocketReceiveResult result = await webSocket.ReceiveAsync(new Memory<byte>(this.buffer), cancellationToken);
			this.MessageType = result.MessageType;
			if (result.MessageType == WebSocketMessageType.Close) {
				Debug.Assert(this.limit == 0);  // is this the first receiving?
				Debug.Assert(result.EndOfMessage);
				this.EndOfMessage = true;
				throw new EndOfStreamException();
			}
			this.EndOfMessage = result.EndOfMessage;
			this.next = 0;
			this.limit = result.Count;
		}

		public virtual async ValueTask<byte[]> ReadWholeMessage(CancellationToken cancellationToken) {
			List<byte> bytes = new List<byte>();

			do {
				// update the internal buffer if there is no more data on it
				if (this.limit <= this.next) {
					await ReceiveAsync(cancellationToken);
				}

				// add data on the buffer to the bytes list
				int len = this.limit - this.next;
				if (0 < len) {
					bytes.AddRange(new ArraySegment<byte>(this.buffer, this.next, len));
					this.next += len;
				}
			} while (this.EndOfMessage == false);

			return bytes.ToArray();
		}

		#endregion
	}
}
