using System;
using System.Runtime.Serialization;


namespace Imkk.Communication {
	public class NegotiateRequest {
		#region types

		public static class PropertyNames {
			#region constants

			public const string Key = "key";

			#endregion
		}

		#endregion


		#region data

		[DataMember(Name = PropertyNames.Key)]
		public string? Key { get; set; } = null;

		#endregion


		#region creation

		public NegotiateRequest() {
		}

		public NegotiateRequest(string? key) {
			// check argument
			// key can be null

			// initialize member
			this.Key = key;
		}

		#endregion
	}
}
