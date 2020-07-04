using System;
using System.Collections.Generic;
using Utf8Json;
using Xunit;
using IMKK.Testing;
using System.Diagnostics;

namespace IMKK.Communication.Test {
	public class NegotiateRequestTest {
		#region tests

		[Fact(DisplayName = "basic: from JSON")]
		public void basic_fromJson() {
			// arrange
			string key = "abcdef";
			string sample = $"{{ \"key\": \"{key}\"}}";

			// act
			NegotiateRequest actual = JsonSerializer.Deserialize<NegotiateRequest>(sample);

			// assert
			Assert.Equal(key, actual.Key);
		}

		[Fact(DisplayName = "basic: to JSON")]
		public void basic_toJson() {
			// arrange
			string key = "あいうえお";
			NegotiateRequest sample = new NegotiateRequest(key);

			// act
			Dictionary<string, object?> actual = TestingUtil.ToJsonObject<NegotiateRequest>(sample);

			// assert
			Assert.Single(actual);
			Assert.Equal(key, actual["key"]);
		}

		[Fact(DisplayName = "key: null; from JSON")]
		public void key_null_fromJson() {
			// arrange
			string? key = null;
			string sample = $"{{ \"key\": null}}";

			// act
			NegotiateRequest actual = JsonSerializer.Deserialize<NegotiateRequest>(sample);

			// assert
			Assert.Equal(key, actual.Key);
		}

		[Fact(DisplayName = "key: null; to JSON")]
		public void key_null_toJson() {
			// arrange
			NegotiateRequest sample = new NegotiateRequest();
			Debug.Assert(sample.Key == null);

			// act
			Dictionary<string, object?> actual = TestingUtil.ToJsonObject<NegotiateRequest>(sample);

			// assert
			Assert.Single(actual);
			Assert.Null(actual["key"]);
		}

		[Fact(DisplayName = "key: none; from JSON")]
		public void key_none_fromJson() {
			// arrange
			string sample = $"{{}}";

			// act
			NegotiateRequest actual = JsonSerializer.Deserialize<NegotiateRequest>(sample);

			// assert
			Assert.Null(actual.Key);
		}

		#endregion
	}
}
