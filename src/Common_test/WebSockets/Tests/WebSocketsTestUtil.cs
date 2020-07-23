using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using IMKK.Testing;


namespace IMKK.WebSockets.Tests {
	public static class WebSocketsTestUtil {
		#region constants

		public const int DefaultStepLength = 16;

		#endregion


		#region samples

		// The sample byte arrays are not provided as properties but by methods,
		// because the contents of byte[] type can be changed even if
		// it is defined as a readonly property. 

		public static byte[] GetSimpleSample() {
			return new byte[] { 0, 1, 2, 3, 4, 5, 6 };
		}

		public static byte[] GetLongSample() {
			return Enumerable.Range(0, 64).Select(n => (byte)n).ToArray();
		}

		public const string TextSample = "This is a text sample.";

		public static byte[] GetTextSampleBytes() {
			return Encoding.UTF8.GetBytes(TextSample);
		}

		#endregion


		#region utilities

		public static async ValueTask<List<byte>> ReadAllAsync(this ReceiveMessageStream stream, int stepLength = DefaultStepLength) {
			// check argument
			if (stream == null) {
				throw new ArgumentNullException(nameof(stream));
			}
			if (stepLength <= 0) {
				throw new ArgumentOutOfRangeException(nameof(stepLength));
			}

			// read whole message
			byte[] buf = new byte[stepLength];
			List<byte> message = new List<byte>();
			while (true) {
				int len = await stream.ReadAsync(buf, 0, stepLength);
				if (len == 0) {
					break;
				}
				message.AddRange(new ArraySegment<byte>(buf, 0, len));
			}

			return message;
		}

		public static List<byte> ReadAll(this ReceiveMessageStream stream, int stepLength = DefaultStepLength) {
			// check argument
			if (stream == null) {
				throw new ArgumentNullException(nameof(stream));
			}
			if (stepLength <= 0) {
				throw new ArgumentOutOfRangeException(nameof(stepLength));
			}

			// read whole message
			byte[] buf = new byte[stepLength];
			List<byte> message = new List<byte>();
			while (true) {
				int len = stream.Read(buf, 0, stepLength);
				if (len == 0) {
					break;
				}
				message.AddRange(new ArraySegment<byte>(buf, 0, len));
			}

			return message;
		}

		public static void WaitForServerTask(Task serverTask, Exception? clientException) {
			if (clientException == null) {
				// the following line may throw an server side exception
				serverTask.Sync();
			} else {
				try {
					serverTask.Sync();
				} catch (Exception serverException) {
					// select the exception to be thrown
					// Generally, give the priority to the client exception over the server exception.
					// But the server exception is an assertion error while the client exception is not,
					// select the assertion error. 
					if (serverException is Xunit.Sdk.XunitException && !(clientException is Xunit.Sdk.XunitException)) {
						throw;
					}
				}
			}
		}

		public static async ValueTask RunClientServerAsync(Func<HttpListener, Task> clientProc, Func<HttpListener, Task> serverProc) {
			// check arguments
			if (clientProc == null) {
				throw new ArgumentNullException(nameof(clientProc));
			}
			if (serverProc == null) {
				throw new ArgumentNullException(nameof(serverProc));
			}

			// run web client/server procedures
			using (HttpListener listener = WebSocketsUtil.StartListening()) {
				Exception? clientException = null;
				Task serverTask = Task.Run(async () => await serverProc(listener));
				try {
					await clientProc(listener);
				} catch (Exception exception) {
					clientException = exception;
					throw;
				} finally {
					listener.Stop();
					WaitForServerTask(serverTask, clientException);
				}
			}
		}

		public static ValueTask RunClientServerAsync(Func<WebSocket, Task> clientProc, Func<WebSocket, Task> serverProc) {
			// check arguments
			if (clientProc == null) {
				throw new ArgumentNullException(nameof(clientProc));
			}
			if (serverProc == null) {
				throw new ArgumentNullException(nameof(serverProc));
			}

			// run web client/server procedures
			return WebSocketsTestUtil.RunClientServerAsync(
				clientProc: async (HttpListener listener) => {
					using (ClientWebSocket webSocket = await listener.ConnectWebSocketAsync()) {
						await clientProc(webSocket);
					}
				},
				serverProc: async (HttpListener listener) => {
					using (WebSocket webSocket = await listener.AcceptWebSocketAsync()) {
						await serverProc(webSocket);
					}
				}
			);
		}

		#endregion
	}
}
