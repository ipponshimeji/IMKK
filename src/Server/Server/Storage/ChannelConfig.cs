using System;
using System.Runtime.Serialization;


namespace IMKK.Server.Storage {
	public class ChannelConfig {
		#region types

		public static class PropertyNames {
			#region constants

			public const string Key = "key";

			public const string MaxConnectionCount = "maxConnectionCount";

			#endregion
		}

		#endregion


		#region constants

		public const int DefaultMaxConnectionCount = 8;

		#endregion


		#region data

		[DataMember(Name = PropertyNames.Key)]
		public string? Key { get; set; } = null;

		[DataMember(Name = PropertyNames.MaxConnectionCount)]
		public int MaxConnectionCount { get; set; } = DefaultMaxConnectionCount;

		#endregion


		#region creation

		public ChannelConfig() {
		}

		public ChannelConfig(string? key, int maxConnectionCount = DefaultMaxConnectionCount) {
			// check argument
			// key can be null

			// initialize member
			this.Key = key;
			this.MaxConnectionCount = MaxConnectionCount;
		}

		#endregion
	}
}
