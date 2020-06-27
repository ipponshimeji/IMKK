using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;


namespace IMKK.WebSockets.Tests {
	public class MessageStreamTest {
		#region utilities

		protected static async ValueTask ExpectCloseAsync(WebSocket webSocket) {
			await Assert.ThrowsAsync<EndOfStreamException>(async () => {
				// receiving a close request cause an EndOfStreamException exception 
				using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket)) {
					try {
						await stream.ReadAllAsync();
					} catch (EndOfStreamException) {
						// assert
						Assert.Equal(WebSocketMessageType.Close, stream.MessageType);
						throw;
					}
				}
			});
			await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
		}

		protected static ValueTask RunClientServerAsync(Func<WebSocket, Task> clientProc, Func<WebSocket, Task> serverProc) {
			return WebSocketsTestUtil.RunClientServerAsync(clientProc, serverProc);
		}

		#endregion


		#region tests

		[Fact(DisplayName="MessageStream: basic communication and properties")]
		public async void MessageStream_basiccommunication() {
			// arrange
			byte[] simpleSample = WebSocketsTestUtil.GetSimpleSample();
			byte[] longSample = WebSocketsTestUtil.GetLongSample();
			Debug.Assert(ReceiveMessageStream.MinBufferSize < longSample.Length);
			const int refillTestBufLen = ReceiveMessageStream.MinBufferSize;
			byte[] textSample = WebSocketsTestUtil.GetTextSampleBytes();

			// act, assert
			await RunClientServerAsync(
				clientProc: async (WebSocket webSocket) => {
					// RT(1) client->server: binary, simple; ReadAsync/WriteAsync
					// This case do not cause buffer refill.
					using (SendMessageStream stream = new SendMessageStream(webSocket, WebSocketMessageType.Binary)) {
						// do not cause buffer refill.
						Debug.Assert(simpleSample.Length < ReceiveMessageStream.DefaultBufferSize);

						// send a message at once
						await stream.WriteAsync(simpleSample, 0, simpleSample.Length);
					}
					// do not close the web socket after the stream is disposed
					Assert.Equal(WebSocketState.Open, webSocket.State);

					// RT(1) server->client: binary, buffer refill; ReadAsync/WriteAsync
					// This case cause buffer refill.
					using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket, refillTestBufLen)) {
						// receive the whole message by multiple receive operations
						int stepLen = 18;   // read length at a time
						Debug.Assert(stepLen < refillTestBufLen);
						List<byte> actual = await stream.ReadAllAsync(stepLen);

						// assert
						Assert.Equal(refillTestBufLen, stream.BufferSize);
						Assert.Equal(WebSocketMessageType.Binary, stream.MessageType);
						Assert.Equal(longSample, actual);
					}

					// RT(2) client->server: text; Read/Write
					using (SendMessageStream stream = new SendMessageStream(webSocket, WebSocketMessageType.Text)) {
						// send the whole message synchronously
						stream.Write(textSample, 0, textSample.Length); // non-async version
					}

					// RT(2) server->client: empty
					using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket)) {
						// check properties
						Assert.True(stream.CanRead);
						Assert.False(stream.CanWrite);
						Assert.False(stream.CanSeek);

						// receive the whole message
						List<byte> actual = await stream.ReadAllAsync();

						// assert
						Assert.Equal(ReceiveMessageStream.DefaultBufferSize, stream.BufferSize);
						Assert.Equal(WebSocketMessageType.Binary, stream.MessageType);
						Assert.Empty(actual);
					}

					// close
					await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
				},
				serverProc: async (WebSocket webSocket) => {
					// RT(1) client->server: binary, simple; ReadAsync/WriteAsync
					// This case do not cause buffer refill.
					Debug.Assert(simpleSample.Length < ReceiveMessageStream.DefaultBufferSize);
					using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket)) {
						// read the whole message at once
						int stepLen = 64;
						Debug.Assert(simpleSample.Length < stepLen);
						List<byte> actual = await stream.ReadAllAsync(stepLen);

						// assert
						Assert.Equal(ReceiveMessageStream.DefaultBufferSize, stream.BufferSize);
						Assert.Equal(WebSocketMessageType.Binary, stream.MessageType);
						Assert.Equal(simpleSample, actual);
					}
					// do not close the web socket after the stream is disposed
					Assert.Equal(WebSocketState.Open, webSocket.State);

					// RT(1) server->client: binary, buffer refill; ReadAsync/WriteAsync
					// This case causes buffer refill.
					using (SendMessageStream stream = new SendMessageStream(webSocket, WebSocketMessageType.Binary)) {
						int len = 40;
						Debug.Assert(len < longSample.Length);
						// cause buffer refill in the client reader  
						Debug.Assert(refillTestBufLen < len);

						// send a message by multiple send operations
						await stream.WriteAsync(longSample, 0, len);
						await stream.WriteAsync(longSample, len, longSample.Length - len);
					}

					// RT(2) client->server: text; Read/Write
					using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket)) {
						// read the whole message synchronously
						List<byte> actual = stream.ReadAll();    // non-async version

						// assert
						Assert.Equal(ReceiveMessageStream.DefaultBufferSize, stream.BufferSize);
						Assert.Equal(WebSocketMessageType.Text, stream.MessageType);
						Assert.Equal(textSample, actual);
					}

					// RT(2) server->client: empty and properties
					using (SendMessageStream stream = new SendMessageStream(webSocket, WebSocketMessageType.Binary)) {
						// check properties
						Assert.False(stream.CanRead);
						Assert.True(stream.CanWrite);
						Assert.False(stream.CanSeek);

						// Flush() do nothing, but it should not throw any exception such as NotSupportedException
						stream.Flush();

						// send an empty message
					}

					// close
					await ExpectCloseAsync(webSocket);
				}
			);
		}

		[Fact(DisplayName="MessageStream: error cases")]
		public async void MessageStream_errors() {
			// act, assert
			await RunClientServerAsync(
				clientProc: async (WebSocket webSocket) => {
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
						// Length
						Assert.Throws<NotSupportedException>(() => { var val = stream.Length; });
						// Position (get; set)
						Assert.Throws<NotSupportedException>(() => { var val = stream.Position; });
						Assert.Throws<NotSupportedException>(() => { stream.Position = 0; });
						// SetLength()
						Assert.Throws<NotSupportedException>(() => { stream.SetLength(0); });
						// Seek()
						Assert.Throws<NotSupportedException>(() => { stream.Seek(0, SeekOrigin.Begin); });
						// Read()
						Assert.Throws<NotSupportedException>(() => { stream.Read(new byte[4], 0, 4); });

						// send an empty message
					}

					// closing
					await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
				},
				serverProc: async (WebSocket webSocket) => {
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
						Assert.Equal(ReceiveMessageStream.MaxBufferSize, stream.BufferSize);

						// unsupported operations
						// Length
						Assert.Throws<NotSupportedException>(() => { var val = stream.Length; });
						// Position (get, set)
						Assert.Throws<NotSupportedException>(() => { var val = stream.Position; });
						Assert.Throws<NotSupportedException>(() => { stream.Position = 0; });
						// SetLength()
						Assert.Throws<NotSupportedException>(() => { stream.SetLength(0); });
						// Seek()
						Assert.Throws<NotSupportedException>(() => { stream.Seek(0, SeekOrigin.Begin); });
						// Flush()
						Assert.Throws<NotSupportedException>(() => { stream.Flush(); });
						// Write()
						Assert.Throws<NotSupportedException>(() => { stream.Write(new byte[4], 0, 4); });

						List<byte> actual = await stream.ReadAllAsync();
						Assert.Empty(actual);
					}

					// close
					await Assert.ThrowsAsync<EndOfStreamException>(async () => {
						// bufferSize: the min value
						using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket, ReceiveMessageStream.MinBufferSize)) {
							Assert.Equal(ReceiveMessageStream.MinBufferSize, stream.BufferSize);

							try {
								await stream.ReadAllAsync();
							} catch (EndOfStreamException) {
								// assert
								Assert.Equal(WebSocketMessageType.Close, stream.MessageType);
								throw;
							}
						}
					});
					await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
				}
			);
		}

		[Fact(DisplayName = "MessageStream: unwrap AggregateException")]
		public async void MessageStream_unwrapAggregateException() {
			// act, assert
			await RunClientServerAsync(
				clientProc: async (WebSocket webSocket) => {
					// close
					await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
				},
				serverProc: async (WebSocket webSocket) => {
					// close
					// The thrown exception should not an AggregateException but an EndOfStreamException
					Assert.Throws<EndOfStreamException>(() => {
						using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket)) {
							// use non-async version to check whether the AggregateException is unwapped
							stream.Read(new byte[4], 0, 4);
						}
					});
					await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
				}
			);
		}

		#endregion
	}
}
