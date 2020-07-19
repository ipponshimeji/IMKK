using System;
using System.Collections.Generic;
using Utf8Json;
using Xunit;
using IMKK.Testing;
using System.Diagnostics;

namespace IMKK.Communication.Tests {
	public class NegotiateResponseTest {
		#region tests

		[Fact(DisplayName = "StatusValue")]
		public void StatusValue() {
			// arrange
			NegotiateResponse sample = new NegotiateResponse();

			// act and assert
			// Status and StatusValue should synchronize
			Assert.Equal((int)sample.Status, sample.StatusValue);
			Assert.Equal((int)NegotiateStatus.Undefined, sample.StatusValue);

			sample.Status = NegotiateStatus.Succeeded;
			Assert.Equal((int)sample.Status, sample.StatusValue);
			Assert.Equal((int)NegotiateStatus.Succeeded, sample.StatusValue);

			sample.StatusValue = (int)NegotiateStatus.InvalidKey;
			Assert.Equal((int)sample.Status, sample.StatusValue);
			Assert.Equal(NegotiateStatus.InvalidKey, sample.Status);
		}

		[Fact(DisplayName = "basic: from JSON")]
		public void basic_fromJson() {
			// arrange
			NegotiateStatus status = NegotiateStatus.Error;
			string message = "だめです";
			string sample = $"{{ \"status\": {(int)status}, \"message\": \"{message}\"}}";

			// act
			NegotiateResponse actual = JsonSerializer.Deserialize<NegotiateResponse>(sample);

			// assert
			Assert.Equal(status, actual.Status);
			Assert.Equal(message, actual.Message);
		}

		[Fact(DisplayName = "basic: to JSON")]
		public void basic_toJson() {
			// arrange
			NegotiateStatus status = NegotiateStatus.InvalidKey;
			string message = "NG";
			NegotiateResponse sample = new NegotiateResponse(status, message);

			// act
			Dictionary<string, object?> actual = TestingUtil.ToJsonObject<NegotiateResponse>(sample);

			// assert
			Assert.Equal(2, actual.Count);
			// Note that a number in JSON text is deserialized to a double.
			Assert.Equal((double)status, actual["status"]);
			Assert.Equal(message, actual["message"]);
		}

		[Fact(DisplayName = "status: none; from JSON")]
		public void status_none_fromJson() {
			// arrange
			string message = "OK?";
			string sample = $"{{\"message\": \"{message}\"}}";

			// act
			NegotiateResponse actual = JsonSerializer.Deserialize<NegotiateResponse>(sample);

			// assert
			Assert.Equal(NegotiateStatus.Undefined, actual.Status);
			Assert.Equal(message, actual.Message);
		}

		[Fact(DisplayName = "message: null; from JSON")]
		public void message_null_fromJson() {
			// arrange
			NegotiateStatus status = NegotiateStatus.Succeeded;
			string sample = $"{{ \"status\": {(int)status}, \"message\": null}}";

			// act
			NegotiateResponse actual = JsonSerializer.Deserialize<NegotiateResponse>(sample);

			// assert
			Assert.Equal(status, actual.Status);
			Assert.Null(actual.Message);
		}

		[Fact(DisplayName = "message: null; to JSON")]
		public void message_null_toJson() {
			// arrange
			NegotiateResponse sample = new NegotiateResponse();
			Debug.Assert(sample.Message == null);

			// act
			Dictionary<string, object?> actual = TestingUtil.ToJsonObject<NegotiateResponse>(sample);

			// assert
			// Note that null message is not serialized.
			Assert.Single(actual);
			// Note that a number in JSON text is deserialized to a double.
			Assert.Equal((double)NegotiateStatus.Undefined, actual["status"]);
		}

		[Fact(DisplayName = "message: none; from JSON")]
		public void message_none_fromJson() {
			// arrange
			NegotiateStatus status = NegotiateStatus.Error;
			string sample = $"{{\"status\": {(int)status}}}";

			// act
			NegotiateResponse actual = JsonSerializer.Deserialize<NegotiateResponse>(sample);

			// assert
			Assert.Equal(NegotiateStatus.Error, actual.Status);
			Assert.Null(actual.Message);
		}

		#endregion
	}
}
