using System;
using System.Runtime.Serialization;


namespace IMKK.Communication {
	public class NegotiateResponse {
		#region types

		public static class DataNames {
			#region constants

			public const string Status = "status";

			public const string Message = "message";

			#endregion
		}

		#endregion


		#region data

		[IgnoreDataMember]
		public NegotiateStatus Status { get; set; } = NegotiateStatus.Undefined;

		[DataMember(Name = DataNames.Message)]
		public string? Message { get; set; } = null;

		#endregion


		#region properties

		/// <summary>
		/// The adapter property for Status to serialize the value of Status in int.
		/// </summary>
		[DataMember(Name = DataNames.Status)]
		public int StatusValue {
			get {
				return (int)this.Status;
			}
			set {
				this.Status = (NegotiateStatus)value;
			}
		}

		#endregion


		#region creation

		public NegotiateResponse() {
		}

		public NegotiateResponse(NegotiateStatus status,  string? message) {
			// check argument
			// message can be null

			// initialize members
			this.Status = status;
			this.Message = message;
		}

		#endregion


		#region methods

		/// <summary>
		/// Notifies UTF8Json serializer whether Message should be serialized or not. 
		/// </summary>
		/// <returns></returns>
		public bool ShouldSerializeMessage() {
			return this.Message != null;
		}

		#endregion
	}
}
