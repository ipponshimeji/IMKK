using System;
using System.Collections.Generic;
using Utf8Json;


namespace IMKK.Testing {
	public static class TestingUtil {
		#region utilities

		/// <summary>
		/// Converts an object to a Dictionary<string, object?> instance
		/// which represents JSON representation of the object.
		/// This method is used to test object to JSON convertion.
		/// Because text representation of a JSON object is not unique,
		/// converting a JSON text into a dictionary is often useful
		/// to compare its content.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="obj"></param>
		/// <returns></returns>
		public static Dictionary<string, object?> ToJsonObject<T>(T obj) {
			string json = JsonSerializer.ToJsonString<T>(obj);
			return JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
		}

		#endregion
	}
}
