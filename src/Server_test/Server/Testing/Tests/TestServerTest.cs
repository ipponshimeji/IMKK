using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Imkk.Server.Testing.Tests {
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


			#region creation

			public SampleTestServer(IImkkServerRunningContext runningContext) : base(runningContext) {
			}

			#endregion


			#region overrides

			protected override Task Listen(HttpListener httpListener, ImkkServer imkkServer) {
				Interlocked.Increment(ref this.listeningCount);
				try {
					return base.Listen(httpListener, imkkServer);
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
			SampleTestServer server = new SampleTestServer(ImkkServerRunningContext.Default);

			// act
			server.Start();
			server.Stop();

			// assert
			Assert.Equal(0, server.ListeningCount);
		}

		#endregion
	}
}
