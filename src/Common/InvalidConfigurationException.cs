using System;


namespace Imkk {
	public class InvalidConfigurationException: Exception {
		#region data

		public string? Path { get; private set; } = null;

		#endregion


		#region creation

		public InvalidConfigurationException(): base(GetActualMessage(null, null)) {
		}

		public InvalidConfigurationException(string? message, string? path, Exception? innerException) : base(GetActualMessage(message, path), innerException) {
			// check argument
			// message and path can be null

			// initialize member
			this.Path = path;
		}

		public InvalidConfigurationException(string? message, string? path) : this(message, path, null) {
		}


		static public string GetActualMessage(string? message, string? path) {
			// check argument
			if (message == null) {
				message = "The configuration value is invalid.";
			}

			return string.IsNullOrEmpty(path)? message: $"{message} (Path '{path}')";
		}

		// TODO: needs constructor for serialization?

		#endregion
	}
}
