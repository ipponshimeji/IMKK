using System;
using System.Runtime.Serialization;


namespace IMKK.Server.Storage {
	public class ChannelInfo {
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

		public ChannelInfo() {
		}

		public ChannelInfo(string? key) {
			// check argument
			// key can be null

			// initialize member
			this.Key = key;
		}

		#endregion
	}
}
