using System;


namespace Imkk {
	public static class ConfigurationUtil {
		#region methods

		public static string? CombinePath(string? basePath, string? key) {
			if (string.IsNullOrEmpty(key)) {
				return (basePath == null)? string.Empty: basePath;
			} else {
				if (string.IsNullOrEmpty(basePath)) {
					return key;
				} else {
					return string.Concat(basePath, ":", key);
				}
			}
		}

		public static InvalidConfigurationException CreateMissingConfigurationException(string? basePath, string? key, Exception? innerException = null) {
			return new InvalidConfigurationException("The indispensable configuration is missing.", CombinePath(basePath, key), innerException);
		}

		public static InvalidConfigurationException CreateMissingOrEmptyConfigurationException(string? basePath, string? key, Exception? innerException = null) {
			return new InvalidConfigurationException("The indispensable configuration is missing or empty.", CombinePath(basePath, key), innerException);
		}

		public static InvalidConfigurationException CreateOutOfRangeValueException(string? basePath, string? key, Exception? innerException = null) {
			return new InvalidConfigurationException("The configuration value is out of range.", CombinePath(basePath, key), innerException);
		}

		#endregion
	}
}
