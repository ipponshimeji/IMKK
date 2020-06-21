using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;


namespace IMKK.WebSockets.Tests {
	public class MessageStreamTest {
		#region tests

		[Fact(DisplayName="MessageStream: basic communication and properties")]
		public async void MessageStream_basiccommunication() {
			// resources
			byte[] simpleSample = WebSocketsTestUtil.GetSimpleSample();
			byte[] longSample = WebSocketsTestUtil.GetLongSample();
			Debug.Assert(ReceiveMessageStream.MinBufferSize < longSample.Length);
			const int refillTestBufLen = ReceiveMessageStream.MinBufferSize;
			byte[] textSample = WebSocketsTestUtil.GetTextSampleBytes();

			using (HttpListener listener = WebSocketsTestUtil.StartListening()) {
				async Task server() {
					using (WebSocket webSocket = await listener.AcceptWebSocketAsync()) {
						// binary, simple
						// This case do not cause buffer refill.
						Debug.Assert(simpleSample.Length < ReceiveMessageStream.DefaultBufferSize);
						using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket)) {
							// read the whole message at once
							int stepLen = 64;
							Debug.Assert(simpleSample.Length < stepLen);
							List<byte> actual = await stream.ReceiveMessageAsync(stepLen);

							// assert
							Assert.Equal(WebSocketMessageType.Binary, stream.MessageType);
							Assert.Equal(simpleSample, actual);
						}
						// do not close the web socket after the stream is disposed
						Assert.Equal(WebSocketState.Open, webSocket.State);

						// binary, buffer refill
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

						// text
						using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket)) {
							// read the whole message synchronously
							List<byte> actual = stream.ReceiveMessage();	// non-async version

							// assert
							Assert.Equal(WebSocketMessageType.Text, stream.MessageType);
							Assert.Equal(textSample, actual);
						}

						// empty and properties
						using (SendMessageStream stream = new SendMessageStream(webSocket, WebSocketMessageType.Binary)) {
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
								try {
									await stream.ReceiveMessageAsync();
								} catch (EndOfStreamException) {
									// assert
									Assert.Equal(WebSocketMessageType.Close, stream.MessageType);
									throw;
								}
							}
						});
						await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
					}
				}

				Task serverTask = Task.Run(server);
				try {
					using (ClientWebSocket webSocket = await listener.ConnectWebSocketAsync()) {
						// binary, simple
						// This case do not cause buffer refill.
						using (SendMessageStream stream = new SendMessageStream(webSocket, WebSocketMessageType.Binary)) {
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
							List<byte> actual = await stream.ReceiveMessageAsync(stepLen);

							// assert
							Assert.Equal(WebSocketMessageType.Binary, stream.MessageType);
							Assert.Equal(longSample, actual);
						}

						// text
						using (SendMessageStream stream = new SendMessageStream(webSocket, WebSocketMessageType.Text)) {
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
							List<byte> actual = await stream.ReceiveMessageAsync();

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
		public async void MessageStream_errors() {
			using (HttpListener listener = WebSocketsTestUtil.StartListening()) {
				async Task server() {
					using (WebSocket webSocket = await listener.AcceptWebSocketAsync()) {
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

							List<byte> actual = await stream.ReceiveMessageAsync();
							Assert.Empty(actual);
						}

						// closing
						await Assert.ThrowsAsync<EndOfStreamException>(async () => {
							// bufferSize: the min value
							using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket, ReceiveMessageStream.MinBufferSize)) {
								try {
									await stream.ReceiveMessageAsync();
								} catch (EndOfStreamException) {
									// assert
									Assert.Equal(WebSocketMessageType.Close, stream.MessageType);
									throw;
								}
							}
						});
						await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
					}
				}

				Task serverTask = Task.Run(server);
				try {
					using (ClientWebSocket webSocket = await listener.ConnectWebSocketAsync()) {
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
					}
				} finally {
					listener.Stop();
					serverTask.Sync();
				}
			}
		}

		[Fact(DisplayName = "MessageStream: unwrap AggregateException")]
		public async void MessageStream_unwrapAggregateException() {
			using (HttpListener listener = WebSocketsTestUtil.StartListening()) {
				async Task server() {
					using (WebSocket webSocket = await listener.AcceptWebSocketAsync()) {
						// closing
						// The thrown exception should not an AggregateException but an EndOfStreamException
						Assert.Throws<EndOfStreamException>(() => {
							using (ReceiveMessageStream stream = new ReceiveMessageStream(webSocket)) {
								// use non-async version to check whether the AggregateException is unwapped
								stream.Read(new byte[4], 0, 4);
							}
						});
						await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
					}
				}

				Task serverTask = Task.Run(server);
				try {
					using (ClientWebSocket webSocket = await listener.ConnectWebSocketAsync()) {
						// closing
						await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
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
