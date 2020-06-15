using System;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Utf8Json;


namespace IMKK.WebSockets {
	public static class WebSocketUtil {
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
