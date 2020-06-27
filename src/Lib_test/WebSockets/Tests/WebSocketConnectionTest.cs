using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
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

		protected static async ValueTask ExpectCloseAsync(WebSocketConnection connection) {
			await Assert.ThrowsAsync<EndOfStreamException>(async () => {
				// receiving a close request cause an EndOfStreamException exception 
				using (ReceiveMessageStream stream = await connection.ReceiveMessageAsync()) {
				}
			});
			await connection.CloseAsync();
		}


		protected static ValueTask RunClientServerAsync(Func<WebSocketConnection, Task> clientProc, Func<WebSocketConnection, Task> serverProc) {
			// check arguments
			if (clientProc == null) {
				throw new ArgumentNullException(nameof(clientProc));
			}
			if (serverProc == null) {
				throw new ArgumentNullException(nameof(serverProc));
			}

			return WebSocketsTestUtil.RunClientServerAsync(
				clientProc: async listener => {
					using (WebSocketConnection connection = await ConnectAsync(listener)) {
						await clientProc(connection);
					}
				},
				serverProc: async listener => {
					using (WebSocketConnection connection = await AcceptAsync(listener)) {
						await serverProc(connection);
					}
				}
			);
		}

		#endregion


		#region constructor

		public class Constructor {
			#region tests

			[Fact(DisplayName = "webSocket: null")]
			public void webSocket_null() {
				// act
				ArgumentNullException actual = Assert.Throws<ArgumentNullException>(() => {
					using (WebSocketConnection target = new WebSocketConnection(null!)) {
					}
				});

				// assert
				Assert.Equal("webSocket", actual.ParamName);
			}

			[Fact(DisplayName = "receiveBufferSize")]
			public async void receiveBufferSize() {
				await WebSocketsTestUtil.RunClientServerAsync(
					// act, assert
					clientProc: async webSocket => {
						// receiveBufferSize: under
						int receiveBufferSize = ReceiveMessageStream.MinBufferSize - 1;
						ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => {
							using (WebSocketConnection connection = new WebSocketConnection(webSocket, receiveBufferSize)) {
							}
						});
						Assert.Equal("receiveBufferSize", exception.ParamName);

						// receiveBufferSize: min
						receiveBufferSize = ReceiveMessageStream.MinBufferSize;
						using (WebSocketConnection connection = new WebSocketConnection(webSocket, receiveBufferSize)) {
							await connection.CloseAsync();
							try {
								using (ReceiveMessageStream stream = await connection.ReceiveMessageAsync()) {
									Assert.Equal(ReceiveMessageStream.MinBufferSize, stream.BufferSize);
								}
							} catch (EndOfStreamException) {
								// continue
							}
						}
					},
					serverProc: async webSocket => {
						// receiveBufferSize: over
						int receiveBufferSize = ReceiveMessageStream.MaxBufferSize + 1;
						ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => {
							using (WebSocketConnection connection = new WebSocketConnection(webSocket, receiveBufferSize)) {
							}
						});
						Assert.Equal("receiveBufferSize", exception.ParamName);

						// receiveBufferSize: max
						receiveBufferSize = ReceiveMessageStream.MaxBufferSize;
						using (WebSocketConnection connection = new WebSocketConnection(webSocket, receiveBufferSize)) {
							// close
							await ExpectCloseAsync(connection);
						}
					}
				);
			}

			#endregion
		}

		#endregion


		#region communication

		public class Communication {
			#region tests

			[Fact(DisplayName = "basic communication")]
			public async void Basic() {
				// arrange
				byte[] simpleSample = WebSocketsTestUtil.GetSimpleSample();
				byte[] longSample = WebSocketsTestUtil.GetLongSample();
				byte[] textSample = WebSocketsTestUtil.GetTextSampleBytes();

				// act, assert
				await RunClientServerAsync(
					clientProc: async connection => {
						// RT(1) client->server
						using (Stream stream = connection.SendMessage(WebSocketMessageType.Binary)) {
							await stream.WriteAsync(simpleSample, 0, simpleSample.Length);
						}

						// RT(1) server->client
						using (ReceiveMessageStream stream = await connection.ReceiveMessageAsync()) {
							List<byte> actual = await stream.ReadAllAsync();

							Assert.Equal(WebSocketMessageType.Binary, stream.MessageType);
							Assert.Equal(longSample, actual);
						}

						// RT(2) client->server
						using (Stream stream = connection.SendMessage(WebSocketMessageType.Text)) {
							await stream.WriteAsync(textSample, 0, textSample.Length);
						}

						// RT(2) server->client
						using (ReceiveMessageStream stream = await connection.ReceiveMessageAsync()) {
							List<byte> actual = await stream.ReadAllAsync();

							Assert.Equal(WebSocketMessageType.Binary, stream.MessageType);
							Assert.Empty(actual);
						}

						await connection.CloseAsync();
					},
					serverProc: async connection => {
						// RT(1) client->server
						using (ReceiveMessageStream stream = await connection.ReceiveMessageAsync()) {
							List<byte> actual = await stream.ReadAllAsync();

							Assert.Equal(WebSocketMessageType.Binary, stream.MessageType);
							Assert.Equal(simpleSample, actual);
						}

						// RT(1) server->client
						using (Stream stream = connection.SendMessage(WebSocketMessageType.Binary)) {
							await stream.WriteAsync(longSample, 0, longSample.Length);
						}

						// RT(2) client->server
						using (ReceiveMessageStream stream = await connection.ReceiveMessageAsync()) {
							List<byte> actual = await stream.ReadAllAsync();

							Assert.Equal(WebSocketMessageType.Text, stream.MessageType);
							Assert.Equal(textSample, actual);
						}

						// RT(2) server->client
						using (Stream stream = connection.SendMessage(WebSocketMessageType.Binary)) {
							// send empty message
						}

						// close
						await ExpectCloseAsync(connection);
					}
				);
			}

			[Fact(DisplayName = "text communication")]
			public async void Text() {
				// arrange
				string asciiSample = "ABCED";
				string nonAsciiSample = "‚ ‚¢‚¤‚¦‚¨";
				string encodingSample = "”L";	// U+732B
				byte[] encodingSampleBytes = new byte[] { 0xE7, 0x8C, 0xAB };

				// act, assert
				await RunClientServerAsync(
					clientProc: async connection => {
						// RT(1) client->server: SendTextMessage and ReceiveTextMessageAsync
						using (StreamWriter writer = connection.SendTextMessage()) {
							writer.Write(asciiSample);
						}

						// RT(1) server->client: SendText and ReceiveTextAsync
						{
							string actual = await connection.ReceiveTextAsync();
							Assert.Equal(actual, nonAsciiSample);
						}

						// RT(2) client->server: SendTextMessage encoding
						using (StreamWriter writer = connection.SendTextMessage()) {
							writer.Write(encodingSample);
						}

						// RT(2) server->client: SendTextAsync encoding
						using (ReceiveMessageStream stream = await connection.ReceiveMessageAsync()) {
							List<byte> actual = await stream.ReadAllAsync();

							Assert.Equal(WebSocketMessageType.Text, stream.MessageType);
							Assert.Equal(encodingSampleBytes, actual);
						}

						await connection.CloseAsync();
					},
					serverProc: async connection => {
						// RT(1) client->server: SendTextMessage and ReceiveTextMessageAsync
						using (StreamReader reader = await connection.ReceiveTextMessageAsync()) {
							String actual = reader.ReadToEnd();

							Assert.Equal(asciiSample, actual);
						}

						// RT(1) server->client: SendTextAsync and ReceiveTextAsync
						await connection.SendTextAsync(nonAsciiSample);

						// RT(2) client->server: SendTextMessage encoding
						using (ReceiveMessageStream stream = await connection.ReceiveMessageAsync()) {
							List<byte> actual = await stream.ReadAllAsync();

							Assert.Equal(WebSocketMessageType.Text, stream.MessageType);
							Assert.Equal(encodingSampleBytes, actual);
						}
						WebSocketState state = connection.State;

						// RT(2) server->client: SendTextAsync encoding
						await connection.SendTextAsync(encodingSample);

						// close
						await ExpectCloseAsync(connection);
					}
				);
			}

			[Fact(DisplayName = "json communication")]
			public async void Json() {
				// arrange
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

				// act
				await RunClientServerAsync(
					clientProc: async connection => {
						// communicate JSON values
						await connection.SendJsonAsync<object?>(sample_null);
						actual_boolean = await connection.ReceiveJsonAsync<bool>();
						await connection.SendJsonAsync<double>(sample_number);
						actual_string = await connection.ReceiveJsonAsync<string>();
						await connection.SendJsonAsync<List<object>>(sample_array);
						actual_object = await connection.ReceiveJsonAsync<Dictionary<string, object>>();

						await connection.CloseAsync();
					},
					serverProc: async connection => {
						// communicate JSON values
						actual_null = await connection.ReceiveJsonAsync<object?>();
						await connection.SendJsonAsync<bool>(sample_boolean);
						actual_number = await connection.ReceiveJsonAsync<double>();
						await connection.SendJsonAsync<string>(sample_string);
						actual_array = await connection.ReceiveJsonAsync<List<object>>();
						await connection.SendJsonAsync<Dictionary<string, object>>(sample_object);

						// close
						await ExpectCloseAsync(connection);
					}
				);

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

			#endregion
		}

		#endregion
	}
}
