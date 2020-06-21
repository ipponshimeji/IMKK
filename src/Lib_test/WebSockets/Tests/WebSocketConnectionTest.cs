using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Xunit;


namespace IMKK.WebSockets.Tests {
	public class WebSocketConnectionTest {
		#region utilities

		protected static ValueTask<WebSocketConnection> AcceptAsync(HttpListener listener) {
			return WebSocketConnection.AcceptAsync(listener);
		}

		protected static ValueTask<WebSocketConnection> ConnectAsync(HttpListener listener) {
			return WebSocketConnection.ConnectAsync(new Uri(WebSocketsTestUtil.GetUriForWebSocket(listener)));
		}

		#endregion


		#region tests

		[Fact(DisplayName = "basic")]
		public async void Basic() {
			byte[] simpleSample = WebSocketsTestUtil.GetSimpleSample();

			using (HttpListener listener = WebSocketsTestUtil.StartListening()) {
				async Task server() {
					using (WebSocketConnection connection = await AcceptAsync(listener)) {
						using (ReceiveMessageStream stream = await connection.ReceiveMessageAsync()) {
							List<byte> actual = WebSocketsTestUtil.ReceiveMessage(stream);

							Assert.Equal(WebSocketMessageType.Binary, stream.MessageType);
							Assert.Equal(simpleSample, actual);
						}

						try {
							using (ReceiveMessageStream stream = await connection.ReceiveMessageAsync()) {
							}
						} catch (EndOfStreamException) {
							// continue
						}
						await connection.CloseAsync();
					}
				}

				Task serverTask = Task.Run(server);
				try {
					using (WebSocketConnection connection = await ConnectAsync(listener)) {
						using (Stream stream = connection.SendMessage(WebSocketMessageType.Binary)) {
							stream.Write(simpleSample, 0, simpleSample.Length);
						}

						await connection.CloseAsync();
					}
				} finally {
					listener.Stop();
					serverTask.Sync();
				}
			}
		}

		#endregion
	}
}
