using System;
using System.Data.Common;
using System.Runtime.Serialization;
using Microsoft.VisualBasic.CompilerServices;
using Utf8Json;


namespace Imkk.Server.Configurations {
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

		[SerializationConstructor]
		public ChannelConfig() {
		}

		public ChannelConfig(string? key, int maxConnectionCount = DefaultMaxConnectionCount) {
			// check argument
			// key can be null

			// initialize member
			this.Key = key;
			this.MaxConnectionCount = maxConnectionCount;
		}

		#endregion


		#region operators

		public static bool operator == (ChannelConfig? x, ChannelConfig? y) {
			if (object.ReferenceEquals(x, null)) {
				return object.ReferenceEquals(y, null);
			} else {
				return (
					!object.ReferenceEquals(y, null) &&
					y.MaxConnectionCount == x.MaxConnectionCount &&
					string.CompareOrdinal(y.Key, x.Key) == 0
				);
			}
		}

		public static bool operator !=(ChannelConfig? x, ChannelConfig? y) {
			return ! (x == y);
		}

		#endregion


		#region overrides

		public override bool Equals(object? obj) {
			return this == (obj as ChannelConfig);
		}

		public override int GetHashCode() {
			return HashCode.Combine(this.Key, this.MaxConnectionCount);
		}

		#endregion
	}
}
