using System;
using System.Runtime.Serialization;


namespace IMKK.Communications {
	public class NegotiateRequest {
		#region types

		public static class DataNames {
			#region constants

			public const string Key = "key";

			#endregion
		}

		#endregion


		#region data

		[DataMember(Name = DataNames.Key)]
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
