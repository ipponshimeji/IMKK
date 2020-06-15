using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;


namespace IMKK.WebSockets.Tests {
	public class WebSocketUtilTest {
		#region tests

		[Fact(DisplayName= "ReceiveJson/SendJson")]
		public async void JsonTest() {
			// resources
			object? sample_null = null;
			bool sample_boolean = true;
			double sample_number = 123.45;
			string sample_string = "sample";
			List<object> sample_array = new List<object> { true, false };
			Dictionary<string, object> sample_object = new Dictionary<string, object> { { "OK?", true } };

			object? actual_null = new object();
			bool actual_boolean = false;
			double actual_number = 0;
			string actual_string = string.Empty;
			List<object> actual_array = null!;
			Dictionary<string, object> actual_object = null!;

			using (HttpListener listener = WebSocketsTestUtil.StartListening()) {
				async Task server() {
					using (WebSocket webSocket = await listener.AcceptWebSocketAsync()) {
						// communicate JSON values
						actual_null = webSocket.ReceiveJson<object?>();
						webSocket.SendJson<bool>(sample_boolean);
						actual_number = await webSocket.ReceiveJsonAsync<double>();
						await webSocket.SendJsonAsync<string>(sample_string);
						actual_array = webSocket.ReceiveJson<List<object>>();
						webSocket.SendJson<Dictionary<string, object>>(sample_object);

						// closing
						Assert.Throws<EndOfStreamException>(() => {
							webSocket.ReceiveJson<object>();
						});
						await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
					}
				}

				Task serverTask = Task.Run(server);
				try {
					using (ClientWebSocket webSocket = await listener.ConnectWebSocketAsync()) {
						// communicate JSON values
						webSocket.SendJson<object?>(sample_null);
						actual_boolean = webSocket.ReceiveJson<bool>();
						await webSocket.SendJsonAsync<double>(sample_number);
						actual_string = await webSocket.ReceiveJsonAsync<string>();
						webSocket.SendJson<List<object>>(sample_array);
						actual_object = webSocket.ReceiveJson<Dictionary<string, object>>();

						// closing
						await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
						serverTask.Sync();
					}
				} finally {
					listener.Stop();
					serverTask.Sync();
				}

				// assert
				Assert.Null(actual_null);
				Assert.Equal(sample_boolean, actual_boolean);
				Assert.Equal(sample_number, actual_number);
				Assert.Equal(sample_string, actual_string);
				Assert.Equal(sample_array, actual_array);
				int count = actual_object.Count;
				Assert.Equal(1, count);
				Assert.Equal(true, actual_object["OK?"]);
			}
		}

		#endregion
	}
}
