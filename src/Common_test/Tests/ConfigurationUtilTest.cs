using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Imkk.Testing;
using Xunit;


namespace Imkk.Tests {
	public class ConfigurationUtilTest {
		#region types

		public class CreateSample {
			#region data

			public readonly string? BasePath;

			public readonly string? Key;

			public readonly Exception? InnerException;

			public readonly string? ExpectedMessage;

			#endregion


			#region constructor

			public CreateSample(string? basePath, string? key, Exception? innerException, string? expectedMessage) {
				// initialize members
				this.BasePath = basePath;
				this.Key = key;
				this.InnerException = innerException;
				this.ExpectedMessage = expectedMessage;
			}

			#endregion


			#region overrides

			public override string ToString() {
				string basePath = TestingUtil.GetDisplayText(this.BasePath, quote: true);
				string key = TestingUtil.GetDisplayText(this.Key, quote: true);
				string innerException = TestingUtil.GetNullOrNonNullText(this.InnerException);

				return $"{{basePath: {basePath}, key: {key}, innerException: {innerException}}}";
			}

			#endregion
		}

		#endregion


		#region samples

		public static IEnumerable<object[]> GetCreateSamples(string message) {
			return new CreateSample[] {
				new CreateSample("a:b:c", "d", null, $"{message} (Path 'a:b:c:d')"),
				new CreateSample("a:b:c", "d", new ApplicationException(), $"{message} (Path 'a:b:c:d')"),
				new CreateSample(null, null, null, message)
			}.ToTestData();
		}

		#endregion


		#region CombinePath

		public class CombinePath {
			#region types

			public class Sample {
				#region data

				public readonly string? BasePath;

				public readonly string? Key;

				public readonly string? Expected;

				#endregion


				#region constructor

				public Sample(string? basePath, string? key, string? expected) {
					// initialize members
					this.BasePath = basePath;
					this.Key = key;
					this.Expected = expected;
				}

				#endregion


				#region overrides

				public override string ToString() {
					string basePath = TestingUtil.GetDisplayText(this.BasePath, quote: true);
					string key = TestingUtil.GetDisplayText(this.Key, quote: true);

					return $"{{basePath: {basePath}, key: {key}}}";
				}

				#endregion
			}

			#endregion


			#region samples

			public static IEnumerable<object[]> GetSamples() {
				return new Sample[] {
					new Sample(null, "d", "d"),
					new Sample(null, null, ""),
					new Sample(null, "", ""),
					new Sample("", "d", "d"),
					new Sample("", null, ""),
					new Sample("", "", ""),
					new Sample("a:b:c", null, "a:b:c"),
					new Sample("a:b:c", "", "a:b:c"),
					new Sample("a:b:c", "d", "a:b:c:d")
				}.ToTestData();
			}

			#endregion


			#region tests

			[Theory]
			[MemberData(nameof(GetSamples))]
			public void Test(Sample sample) {
				// check argument
				Debug.Assert(sample != null);

				// arrange

				// act
				string? actual = ConfigurationUtil.CombinePath(sample.BasePath, sample.Key);

				// assert
				Assert.Equal(sample.Expected, actual);
			}

			#endregion
		}

		#endregion


		#region CreateMissingConfigurationException

		public class CreateMissingConfigurationException {
			#region constants

			public const string Message = "The indispensable configuration is missing.";

			#endregion


			#region utilities

			public static IEnumerable<object[]> GetSamples() {
				return GetCreateSamples(Message);
			}

			#endregion


			#region tests

			[Theory]
			[MemberData(nameof(GetSamples))]
			public void Test(CreateSample sample) {
				// check argument
				Debug.Assert(sample != null);

				// arrange
				string? expectedPath = ConfigurationUtil.CombinePath(sample.BasePath, sample.Key);

				// act
				InvalidConfigurationException? actual = ConfigurationUtil.CreateMissingConfigurationException(sample.BasePath, sample.Key, sample.InnerException);

				// assert
				Assert.Equal(sample.ExpectedMessage, actual.Message);
				Assert.Equal(sample.InnerException, actual.InnerException);
				Assert.Equal(expectedPath, actual.Path);
			}

			#endregion
		}

		#endregion


		#region CreateMissingOrEmptyConfigurationException

		public class CreateMissingOrEmptyConfigurationException {
			#region constants

			public const string Message = "The indispensable configuration is missing or empty.";

			#endregion


			#region utilities

			public static IEnumerable<object[]> GetSamples() {
				return GetCreateSamples(Message);
			}

			#endregion


			#region tests

			[Theory]
			[MemberData(nameof(GetSamples))]
			public void Test(CreateSample sample) {
				// check argument
				Debug.Assert(sample != null);

				// arrange
				string? expectedPath = ConfigurationUtil.CombinePath(sample.BasePath, sample.Key);

				// act
				InvalidConfigurationException? actual = ConfigurationUtil.CreateMissingOrEmptyConfigurationException(sample.BasePath, sample.Key, sample.InnerException);

				// assert
				Assert.Equal(sample.ExpectedMessage, actual.Message);
				Assert.Equal(sample.InnerException, actual.InnerException);
				Assert.Equal(expectedPath, actual.Path);
			}

			#endregion
		}

		#endregion


		#region CreateOutOfRangeValueException

		public class CreateOutOfRangeValueException {
			#region constants

			public const string Message = "The configuration value is out of range.";

			#endregion


			#region utilities

			public static IEnumerable<object[]> GetSamples() {
				return GetCreateSamples(Message);
			}

			#endregion


			#region tests

			[Theory]
			[MemberData(nameof(GetSamples))]
			public void Test(CreateSample sample) {
				// check argument
				Debug.Assert(sample != null);

				// arrange
				string? expectedPath = ConfigurationUtil.CombinePath(sample.BasePath, sample.Key);

				// act
				InvalidConfigurationException? actual = ConfigurationUtil.CreateOutOfRangeValueException(sample.BasePath, sample.Key, sample.InnerException);

				// assert
				Assert.Equal(sample.ExpectedMessage, actual.Message);
				Assert.Equal(sample.InnerException, actual.InnerException);
				Assert.Equal(expectedPath, actual.Path);
			}

			#endregion
		}

		#endregion
	}
}
