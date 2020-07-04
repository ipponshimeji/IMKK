using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;


namespace IMKK.WebSockets {
	public class SendMessageStream: MessageStream {
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
				// write end of the message
				try {
					WebSocket? webSocket = this.WebSocket;
					if (webSocket != null && webSocket.State == WebSocketState.Open) {
						webSocket.SendAsync(ReadOnlyMemory<byte>.Empty, this.MessageType, true, CancellationToken.None).Sync();
					}
				} catch {
					// continue
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
			WriteAsync(buffer, offset, count, CancellationToken.None).Sync();
		}

		#endregion
	}
}
