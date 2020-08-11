using System;
using System.Collections.Generic;
using System.Diagnostics;
using Imkk.Testing;
using Xunit;


namespace Imkk.Tests {
	public class InvalidConfigurationExceptionTest {
		#region constants

		public const string DefaultMessage = "The configuration value is invalid.";

		#endregion


		#region constructor

		public class Constructor {
			#region types

			public class Sample {
				#region data

				public readonly string? Message;

				public readonly string? Path;

				public readonly Exception? InnerException;

				public readonly string? ExpectedMessage;

				#endregion


				#region constructor

				public Sample(string? message, string? path, Exception? innerException, string? expectedMessage) {
					// initialize members
					this.Message = message;
					this.Path = path;
					this.InnerException = innerException;
					this.ExpectedMessage = expectedMessage;
				}

				#endregion


				#region overrides

				public override string ToString() {
					string message = TestingUtil.GetDisplayText(this.Message, quote: true);
					string path = TestingUtil.GetDisplayText(this.Path, quote: true);
					string innerException = TestingUtil.GetNullOrNonNullText(this.InnerException);

					return $"{{message: {message}, path: {path}, innerException: {innerException}}}";
				}

				#endregion
			}

			#endregion


			#region samples

			public static IEnumerable<object[]> GetSamples() {
				return new Sample[] {
					new Sample("error", "a:b:c", null, "error (Path 'a:b:c')"),
					new Sample(null, "a:b:c", null, $"{DefaultMessage} (Path 'a:b:c')"),
					new Sample("", "a:b:c", null, " (Path 'a:b:c')"),
					new Sample("error", null, null, "error"),
					new Sample("error", "", null, "error"),
					new Sample(null, null, null, DefaultMessage),
					new Sample("error", "a:b:c", new ApplicationException(), "error (Path 'a:b:c')")
				}.ToTestData();
			}

			#endregion


			#region tests

			[Fact(DisplayName = "ctor()")]
			public void noparam() {
				// arrange

				// act
				InvalidConfigurationException target = new InvalidConfigurationException();

				// assert
				Assert.Equal(DefaultMessage, target.Message);
				Assert.Null(target.InnerException);
				Assert.Null(target.Path);
			}

			[Theory(DisplayName = "ctor(string, string, Exception)")]
			[MemberData(nameof(GetSamples))]
			public void string_string_Exception(Sample sample) {
				// check argument
				Debug.Assert(sample != null);

				// arrange

				// act
				InvalidConfigurationException actual = new InvalidConfigurationException(sample.Message, sample.Path, sample.InnerException);

				// assert
				Assert.Equal(sample.ExpectedMessage, actual.Message);
				Assert.Equal(sample.InnerException, actual.InnerException);
				Assert.Equal(sample.Path, actual.Path);
			}

			[Theory(DisplayName = "ctor(string, string)")]
			[MemberData(nameof(GetSamples))]
			public void string_string(Sample sample) {
				// check argument
				Debug.Assert(sample != null);

				// arrange

				// act
				InvalidConfigurationException actual = new InvalidConfigurationException(sample.Message, sample.Path);

				// assert
				Assert.Equal(sample.ExpectedMessage, actual.Message);
				Assert.Null(actual.InnerException);
				Assert.Equal(sample.Path, actual.Path);
			}

			#endregion
		}

		#endregion
	}
}
