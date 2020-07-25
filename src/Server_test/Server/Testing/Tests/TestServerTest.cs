using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace IMKK.Server.Testing.Tests {
	public class TestServerTest {
		#region types

		public class SampleTestServer: TestServer {
			#region data

			private int listeningCount = 0;

			#endregion


			#region properties

			public int ListeningCount {
				get {
					return this.listeningCount;
				}
			}

			#endregion


			#region overrides

			protected override Task Listen(HttpListener httpListener, IMKKServer immkServer) {
				Interlocked.Increment(ref this.listeningCount);
				try {
					return base.Listen(httpListener, immkServer);
				} finally {
					Interlocked.Decrement(ref this.listeningCount);
				}
			}

			#endregion
		}

		#endregion


		#region tests

		[Fact(DisplayName = "simple start & stop")]
		public void simple_start_stop() {
			// arrange
			SampleTestServer server = new SampleTestServer();

			// act
			server.Start();
			server.Stop();

			// assert
			Assert.Equal(0, server.ListeningCount);
		}

		#endregion
	}
}
