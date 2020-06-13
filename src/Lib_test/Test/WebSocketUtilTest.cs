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
using IMKK.Lib;
using SendMessageStream = IMKK.Lib.WebSocketUtil.SendMessageStream;
using ReceiveMessageStream = IMKK.Lib.WebSocketUtil.ReceiveMessageStream;


namespace IMKK.Lib.Test {
	public class WebSocketUtilTest {
		#region data

		private static readonly Random random = new Random();

		#endregion


		#region utilities

		protected static HttpListener StartListening() {
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

		protected static async ValueTask<WebSocket> AcceptWebSocketAsync(HttpListener listener) {
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

		protected static async ValueTask<ClientWebSocket> ConnectWebSocketAsync(HttpListener listener) {
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

		protected static string GetUriForWebSocket(HttpListener listener) {
			// check argument
			if (listener == null) {
				throw new ArgumentNullException(nameof(listener));
			}

			// an easy implementation
			// It is enough for test.
			return listener.Prefixes.First().Replace("http:", "ws:");
		}

		protected static async ValueTask<List<byte>> ReceiveMessageAsync(ReceiveMessageStream stream, int stepLen) {
			// check argument
			if (stream == null) {
				throw new ArgumentNullException(nameof(stream));
			}
			if (stepLen <= 0) {
				throw new ArgumentOutOfRangeException(nameof(stepLen));
			}

			// read whole message
			byte[] buf = new byte[stepLen];
			List<byte> message = new List<byte>();
			while (true) {
				int len = await stream.ReadAsync(buf, 0, stepLen);
				if (len == 0) {
					break;
				}
				message.AddRange(new ArraySegment<byte>(buf, 0, len));
			}

			return message;
		}

		protected static List<byte> ReceiveMessage(ReceiveMessageStream stream, int stepLen) {
			// check argument
			if (stream == null) {
				throw new ArgumentNullException(nameof(stream));
			}
			if (stepLen <= 0) {
				throw new ArgumentOutOfRangeException(nameof(stepLen));
			}

			// read whole message
			byte[] buf = new byte[stepLen];
			List<byte> message = new List<byte>();
			while (true) {
				int len = stream.Read(buf, 0, stepLen);
				if (len == 0) {
					break;
				}
				message.AddRange(new ArraySegment<byte>(buf, 0, len));
			}

			return message;
		}

		#endregion


		#region tests

		[Fact(DisplayName="MessageStream: communication")]
		public async void MessageStream_communication() {
			// resources
			byte[] simpleSample = new byte[] { 0, 1, 2, 3, 4, 5, 6 };
			byte[] longSample = Enumerable.Range(0, 64).Select(n => (byte)n).ToArray();
			Debug.Assert(ReceiveMessageStream.MinBufferSize < longSample.Length);
			const int refillTestBufLen = ReceiveMessageStream.MinBufferSize;
			byte[] textSample = Encoding.UTF8.GetBytes("This is a text sample");

			using (HttpListener listener = StartListening()) {
				async Task server() {
					using (WebSocket webSocket = await AcceptWebSocketAsync(listener)) {
						// binary, simple
						// This case do not cause buffer refill.
						Debug.Assert(simpleSample.Length < ReceiveMessageStream.DefaultBufferSize);
						using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket)) {
							// read the whole message at once
							int stepLen = 64;
							Debug.Assert(simpleSample.Length < stepLen);
							List<byte> actual = await ReceiveMessageAsync(stream, stepLen);

							// assert
							Assert.Equal(WebSocketMessageType.Binary, stream.MessageType);
							Assert.Equal(simpleSample, actual);
						}
						// do not close the web socket after the stream is disposed
						Assert.Equal(WebSocketState.Open, webSocket.State);

						// binary, buffer refill
						// This case causes buffer refill.
						using (WebSocketUtil.SendMessageStream stream = new WebSocketUtil.SendMessageStream(webSocket, WebSocketMessageType.Binary)) {
							int len = 40;
							Debug.Assert(len < longSample.Length);
							// cause buffer refill in the client reader  
							Debug.Assert(refillTestBufLen < len);

							// send a message by multiple send operations
							await stream.WriteAsync(longSample, 0, len);
							await stream.WriteAsync(longSample, len, longSample.Length - len);
						}

						// text
						using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket)) {
							// read the whole message synchronously
							int stepLen = 64;
							List<byte> actual = ReceiveMessage(stream, stepLen);

							// assert
							Assert.Equal(WebSocketMessageType.Text, stream.MessageType);
							Assert.Equal(textSample, actual);
						}

						// empty and properties
						using (WebSocketUtil.SendMessageStream stream = new WebSocketUtil.SendMessageStream(webSocket, WebSocketMessageType.Binary)) {
							// check properties
							Assert.False(stream.CanRead);
							Assert.True(stream.CanWrite);
							Assert.False(stream.CanSeek);

							// Flush() do nothing, but it should not throw any exception such as NotSupportedException
							stream.Flush();

							// send an empty message
						}

						// closing
						await Assert.ThrowsAsync<EndOfStreamException>(async () => {
							using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket)) {
								await ReceiveMessageAsync(stream, 8);

								// assert
								Assert.Equal(WebSocketMessageType.Close, stream.MessageType);
							}
						});
						await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
					}
				}

				Task serverTask = Task.Run(server);
				try {
					using (ClientWebSocket webSocket = await ConnectWebSocketAsync(listener)) {
						// binary, simple
						// This case do not cause buffer refill.
						using (SendMessageStream stream = new WebSocketUtil.SendMessageStream(webSocket, WebSocketMessageType.Binary)) {
							// do not cause buffer refill.
							Debug.Assert(simpleSample.Length < ReceiveMessageStream.DefaultBufferSize);

							// send a message at once
							await stream.WriteAsync(simpleSample, 0, simpleSample.Length);
						}
						// do not close the web socket after the stream is disposed
						Assert.Equal(WebSocketState.Open, webSocket.State);

						// binary, buffer refill
						// This case cause buffer refill.
						using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket, refillTestBufLen)) {
							// receive the whole message by multiple receive operations
							int stepLen = 18;   // read length at a time
							Debug.Assert(stepLen < refillTestBufLen);
							List<byte> actual = await ReceiveMessageAsync(stream, stepLen);

							// assert
							Assert.Equal(WebSocketMessageType.Binary, stream.MessageType);
							Assert.Equal(longSample, actual);
						}

						// text
						using (SendMessageStream stream = new WebSocketUtil.SendMessageStream(webSocket, WebSocketMessageType.Text)) {
							// send the whole message synchronously
							stream.Write(textSample, 0, textSample.Length);	// non-async version
						}

						// empty
						using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket)) {
							// check properties
							Assert.True(stream.CanRead);
							Assert.False(stream.CanWrite);
							Assert.False(stream.CanSeek);

							// receive the whole message
							List<byte> actual = await ReceiveMessageAsync(stream, 8);

							// assert
							Assert.Equal(WebSocketMessageType.Binary, stream.MessageType);
							Assert.Empty(actual);
						}

						// closing
						await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
					}
				} finally {
					listener.Stop();
					serverTask.Sync();
				}
			}
		}

		[Fact(DisplayName="MessageStream: error cases")]
		public async void MessageStream_error() {
			using (HttpListener listener = StartListening()) {
				async Task server() {
					using (WebSocket webSocket = await AcceptWebSocketAsync(listener)) {
						// invalid arguments for constructor
						// webSocket: null
						ArgumentException exception = Assert.Throws<ArgumentNullException>(() => {
							using (ReceiveMessageStream stream = new ReceiveMessageStream(null!)) {
							}
						});
						Assert.Equal("webSocket", exception.ParamName);

						// bufferSize: under the min value
						exception = Assert.Throws<ArgumentOutOfRangeException>(() => {
							using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket, ReceiveMessageStream.MinBufferSize - 1)) {
							}
						});
						Assert.Equal("bufferSize", exception.ParamName);

						// bufferSize: over the max value
						exception = Assert.Throws<ArgumentOutOfRangeException>(() => {
							using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket, ReceiveMessageStream.MaxBufferSize + 1)) {
							}
						});
						Assert.Equal("bufferSize", exception.ParamName);

						// unsupported operations
						// bufferSize: the max value
						using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket, ReceiveMessageStream.MaxBufferSize)) {
							// unsupported operations
							Assert.Throws<NotSupportedException>(() => { var val = stream.Length; });
							Assert.Throws<NotSupportedException>(() => { var val = stream.Position; });
							Assert.Throws<NotSupportedException>(() => { stream.Position = 0; });
							Assert.Throws<NotSupportedException>(() => { stream.SetLength(0); });
							Assert.Throws<NotSupportedException>(() => { stream.Seek(0, SeekOrigin.Begin); });
							Assert.Throws<NotSupportedException>(() => { stream.Flush(); });
							Assert.Throws<NotSupportedException>(() => { stream.Write(new byte[4], 0, 4); });

							List<byte> actual = await ReceiveMessageAsync(stream, 4);
							Assert.Empty(actual);
						}

						// closing
						await Assert.ThrowsAsync<EndOfStreamException>(async () => {
							// bufferSize: the min value
							using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket, ReceiveMessageStream.MinBufferSize)) {
								await ReceiveMessageAsync(stream, 8);

								// assert
								Assert.Equal(WebSocketMessageType.Close, stream.MessageType);
							}
						});
						await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
					}
				}

				Task serverTask = Task.Run(server);
				try {
					using (ClientWebSocket webSocket = await ConnectWebSocketAsync(listener)) {
						// invalid arguments for constructor
						// webSocket: null
						ArgumentException exception = Assert.Throws<ArgumentNullException>(() => {
							using (SendMessageStream stream = new SendMessageStream(null!, WebSocketMessageType.Binary)) {
							}
						});
						Assert.Equal("webSocket", exception.ParamName);

						// unsupported operations
						using (SendMessageStream stream = new SendMessageStream(webSocket, WebSocketMessageType.Binary)) {
							// unsupported operations
							Assert.Throws<NotSupportedException>(() => { var val = stream.Length; });
							Assert.Throws<NotSupportedException>(() => { var val = stream.Position; });
							Assert.Throws<NotSupportedException>(() => { stream.Position = 0; });
							Assert.Throws<NotSupportedException>(() => { stream.SetLength(0); });
							Assert.Throws<NotSupportedException>(() => { stream.Seek(0, SeekOrigin.Begin); });
							Assert.Throws<NotSupportedException>(() => { stream.Read(new byte[4], 0, 4); });

							// send an empty message
						}

						// closing
						await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
					}
				} finally {
					listener.Stop();
					serverTask.Sync();
				}
			}
		}

		[Fact(DisplayName = "MessageStream: unwrap AggregateException")]
		public async void MessageStream_unwrapAggregateException() {
			using (HttpListener listener = StartListening()) {
				async Task server() {
					using (WebSocket webSocket = await AcceptWebSocketAsync(listener)) {
						// closing
						// The thrown exception should not an AggregateException but an EndOfStreamException
						Assert.Throws<EndOfStreamException>(() => {
							using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket)) {
								stream.Read(new byte[4], 0, 4);
							}
						});
						await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
					}
				}

				Task serverTask = Task.Run(server);
				try {
					using (ClientWebSocket webSocket = await ConnectWebSocketAsync(listener)) {
						// closing
						await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
					}
				} finally {
					listener.Stop();
					serverTask.Sync();
				}
			}
		}


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

			using (HttpListener listener = StartListening()) {
				async Task server() {
					using (WebSocket webSocket = await AcceptWebSocketAsync(listener)) {
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
					using (ClientWebSocket webSocket = await ConnectWebSocketAsync(listener)) {
						// communicate JSON values
						webSocket.SendJson<object?>(sample_null);
						actual_boolean = webSocket.ReceiveJson<bool>();
						await webSocket.SendJsonAsync<double>(sample_number);
						actual_string = await webSocket.ReceiveJsonAsync<string>();
						webSocket.SendJson<List<object>>(sample_array);
						actual_object = webSocket.ReceiveJson<Dictionary<string, object>>();

						// closing
						await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
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
