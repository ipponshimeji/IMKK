using System;
using System.Collections.Generic;


namespace IMKK.Server.Configurations {
	public interface IIMKKServerConfig {
		IEnumerable<ChannelConfig> Channels { get; }
	}
}
