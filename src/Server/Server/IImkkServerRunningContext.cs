using System;
using Microsoft.Extensions.Configuration;
using Imkk.ObjectModel;

namespace Imkk.Server {
	public interface IImkkServerRunningContext: IRunningContext {
		ImkkServer CreateImkkServer(IConfigurationSection config);

		ImkkServer CreateImkkServer(IConfiguration config, string key) {
			// check arguments
			if (config == null) {
				throw new ArgumentNullException(nameof(config));
			}
			if (key == null) {
				throw new ArgumentNullException(nameof(key));
			}

			return CreateImkkServer(config.GetSection(key));
		}


		Channel CreateChannel(IConfigurationSection config);
	}
}
