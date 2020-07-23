using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;


namespace IMKK.Testing {
	public static class WebSocketsUtil {
		#region data

		private static readonly Random random = new Random();

		#endregion


		#region utilities

		public static HttpListener StartListening() {
			// select an unused port and start listening at the port
			HttpListener? listener = null;

			// try to listen at the candidate port
			// It selects a candidate port from the range for dynamic or private ports,
			// that is from 49152 to 65535.
			// This method is used to select the candidate port randomly.
			// Another way, trying to bind port by for loop
			//	 for (int port = 41952; port < 65535; ++port) ...
			// takes time to handle "the port is used" errors
			// because the ports near 41952 tend to be used.
			const int maxRetry = 200;
			HashSet<int> usedPorts = new HashSet<int>(maxRetry / 4);
			for (int i = 0; i < maxRetry; ++i) {
				// select a candidate from the range for dynamic or private ports
				// (that is, from 49152 to 65535)
				int port = random.Next(41952, 65535);
				if (usedPorts.Contains(port)) {
					continue;
				}

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
				// There is small possibility
				// that this error occurs even though there are available ports.
				// The possibility increase when the available ports are very few.
				// Retry the test in such case.
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

		#endregion
	}
}
