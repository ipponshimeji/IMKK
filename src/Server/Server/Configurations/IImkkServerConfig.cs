using System;
using System.Collections.Generic;


namespace IMKK.Server.Configurations {
	public interface IImkkServerConfig {
		IEnumerable<ChannelConfig> Channels { get; }
	}
}
