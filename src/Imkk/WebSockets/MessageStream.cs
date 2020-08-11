using System;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;


namespace Imkk.WebSockets {
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
			// check state
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
}
