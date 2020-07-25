using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Utf8Json;


namespace IMKK.Server.Configurations {
	public class IMKKServerConfig: IIMKKServerConfig {
		#region types

		public static class PropertyNames {
			#region constants

			public const string Channels = "channels";

			#endregion
		}

		#endregion


		#region data

		private readonly IEnumerable<ChannelConfig> channels;

		#endregion


		#region properties

		[DataMember(Name = PropertyNames.Channels)]
		public IEnumerable<ChannelConfig> Channels {
			get {
				return this.channels;
			}
		}

		#endregion


		#region creation

		public IMKKServerConfig(IEnumerable<ChannelConfig>? channels = null) {
			// check argument
			if (channels == null) {
				channels = Array.Empty<ChannelConfig>();
			}

			// initialize members
			this.channels = channels;
		}

		public static IMKKServerConfig CreateFromJson(string json) {
			// check argument
			if (json == null) {
				throw new ArgumentNullException(nameof(json));
			}

			return JsonSerializer.Deserialize<IMKKServerConfig>(json);
		}

		public static IMKKServerConfig CreateFromJsonFile(string filePath) {
			// check argument
			if (filePath == null) {
				throw new ArgumentNullException(nameof(filePath));
			}

			using (Stream stream = File.OpenRead(filePath)) {
				return JsonSerializer.Deserialize<IMKKServerConfig>(stream);
			}
		}

		#endregion
	}
}
