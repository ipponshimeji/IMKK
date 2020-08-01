using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using IMKK.WebSockets;

namespace IMKK.Communication {
	public class NegotiateResponse {
		#region types

		public static class PropertyNames {
			#region constants

			public const string Status = "status";

			public const string Message = "message";

			#endregion
		}

		public static class StandardMessages {
			#region constants

			public const string Succeeded = "Succeeded.";

			public const string Error = "Internal error.";

			public const string InvalidKey = "The specified key is invalid.";

			public const string TooManyConnection = "The channel for the specified key has too many connection.";

			#endregion
		}

		#endregion


		#region data

		[IgnoreDataMember]
		public NegotiateStatus Status { get; set; } = NegotiateStatus.Undefined;

		[DataMember(Name = PropertyNames.Message)]
		public string? Message { get; set; } = null;

		#endregion


		#region properties

		/// <summary>
		/// The adapter property for Status to serialize the value of Status in int.
		/// </summary>
		[DataMember(Name = PropertyNames.Status)]
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

		public NegotiateResponse(NegotiateStatus status,  string? message) {
			// check argument
			// message can be null

			// initialize members
			this.Status = status;
			this.Message = message;
		}

		public NegotiateResponse() : this(NegotiateStatus.Undefined, null) {
		}

		#endregion


		#region methods

		public static string GetStandardMessage(NegotiateStatus status) {
			switch (status) {
				case NegotiateStatus.Succeeded:
					return StandardMessages.Succeeded;
				case NegotiateStatus.Error:
					return StandardMessages.Error;
				case NegotiateStatus.InvalidKey:
					return StandardMessages.InvalidKey;
				case NegotiateStatus.TooManyConnection:
					return StandardMessages.TooManyConnection;
				default:
					// return general error
					return StandardMessages.Error;
			}
		}

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
