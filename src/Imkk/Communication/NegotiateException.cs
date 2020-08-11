using System;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Imkk.WebSockets;

namespace Imkk.Communication {
	public class NegotiateException: InvalidOperationException {
		#region data

		public readonly NegotiateResponse Response;

		#endregion


		#region creation

		public NegotiateException(NegotiateResponse response): base(GetMessage(response)) {
			// check argument
			if (response == null) {
				throw new ArgumentNullException(nameof(response));
			}

			// initialize members
			this.Response = response;
		}

		private static string GetMessage(NegotiateResponse response) {
			// check argument
			Debug.Assert(response != null);

			// get message
			string? message = response.Message;
			if (message == null) {
				// use standard message if the message is not specified
				message = NegotiateResponse.GetStandardMessage(response.Status);
			}

			return message;
		}

		#endregion
	}
}
