using System;
using System.Collections.Generic;


namespace Imkk.Server.Configurations {
	public interface IImkkServerConfig {
		IEnumerable<ChannelConfig> Channels { get; }
	}
}
