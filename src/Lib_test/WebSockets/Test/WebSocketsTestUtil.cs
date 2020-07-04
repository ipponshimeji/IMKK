using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace IMKK.WebSockets.Test {
	public static class WebSocketsTestUtil {
		#region constants

		public const int DefaultStepLength = 16;

		#endregion


		#region data

		private static readonly Random random = new Random();

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

		public static HttpListener StartListening() {
			// select an unused port and start listening at the port
			HttpListener? listener = null;

			// define an internal method to select a candidate port
			// It selects a candidate from the range for dynamic or private ports,
			// that is from 49152 to 65535.
			// This method is used to select the candidate port randomly.
			// Another way, trying to bind port by for loop
			//	 for (int port = 41952; port < 65535; ++port) ...
			// takes time to handle "the port is used" errors
			// because the ports near 41952 tend to be used.
			const int maxRetry = 100;
			HashSet<int> usedPorts = new HashSet<int>(maxRetry / 4);
			int getNextCandidatePort() {
				int port;
				do {
					// select a candidate from the range for dynamic or private ports
					// (that is, from 49152 to 65535)
					port = random.Next(41952, 65535);
				} while (usedPorts.Contains(port));
				return port;
			}

			// try to listen at the candidate port
			for (int i = 0; i < maxRetry; ++i) {
				// get a port to be tried
				int port = getNextCandidatePort();

				listener = new HttpListener();
				listener.Prefixes.Add($"http://localhost:{port}/");
				try {
					listener.Start();
					break;	// OK
				} catch (HttpListenerException) {
					// The port is used
					// TODO: filter by appropriate error code (32?)
					listener.Close();
					listener = null;
					usedPorts.Add(port);
					continue;
				}
			}
			if (listener == null) {
				throw new ApplicationException("No available port.");
			}

			return listener;
		}

		public static async ValueTask<WebSocket> AcceptWebSocketAsync(this HttpListener listener) {
			// check argument
			if (listener == null) {
				throw new ArgumentNullException(nameof(listener));
			}

			// accept a request
			HttpListenerContext context = listener.GetContext();
			if (context.Request.IsWebSocketRequest == false) {
				context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
				context.Response.Close();
				throw new ApplicationException("Expected: a WebSocket request, Actual: not a WebSocket request");
			}

			return (await context.AcceptWebSocketAsync(null)).WebSocket;
		}

		public static async ValueTask<ClientWebSocket> ConnectWebSocketAsync(this HttpListener listener) {
			// check argument
			if (listener == null) {
				throw new ArgumentNullException(nameof(listener));
			}

			// connect to the listener
			string uri = GetUriForWebSocket(listener);
			ClientWebSocket ws = new ClientWebSocket();
			try {
				await ws.ConnectAsync(new Uri(uri), CancellationToken.None);
			} catch {
				((IDisposable)ws).Dispose();
				throw;
			}

			return ws;
		}

		public static string GetUriForWebSocket(HttpListener listener) {
			// check argument
			if (listener == null) {
				throw new ArgumentNullException(nameof(listener));
			}

			// an easy implementation
			// It is enough for test.
			return listener.Prefixes.First().Replace("http:", "ws:");
		}

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
			using (HttpListener listener = WebSocketsTestUtil.StartListening()) {
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
