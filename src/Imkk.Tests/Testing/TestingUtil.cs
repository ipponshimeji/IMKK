using System;
using System.Collections.Generic;
using System.Linq;
using Utf8Json;


namespace Imkk.Testing {
	public static class TestingUtil {
		#region constants

		public const string NullLabel = "(null)";

		public const string NonNullLabel = "(non-null)";

		#endregion


		#region utilities

		public static IEnumerable<object[]> ToTestData<T>(this IEnumerable<T> data) where T: class {
			// check argument
			if (data == null) {
				throw new ArgumentNullException(nameof(data));
			}

			return data.Select(datum => new object[] { datum }).ToArray();
		}

		public static string GetDisplayText(object? value) {
			if (value == null) {
				return NullLabel;
			} else {
				string? valueText = value.ToString();
				return valueText ?? string.Empty;
			}
		}

		public static string GetDisplayText(string? value, bool quote = true) {
			if (value == null) {
				return NullLabel;
			} else {
				return quote? string.Concat("\"", value, "\""): value;
			}
		}

		public static string GetNullOrNonNullText(object? value) {
			return (value == null)? NullLabel: NonNullLabel;
		}

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
