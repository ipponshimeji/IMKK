using System;
using System.Collections.Generic;


namespace IMKK.Server.Storage {
	public interface IIMKKStorage {
		IEnumerable<ChannelConfig> Channels { get; }
	}
}
