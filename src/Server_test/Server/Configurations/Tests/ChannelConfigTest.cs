using System;
using System.Diagnostics;
using Utf8Json;
using Xunit;

namespace IMKK.Server.Configurations.Tests {
	public class ChannelConfigTest {
		#region constructor

		public class Constructor {
			#region tests

			[Fact(DisplayName = "general")]
			public void general() {
				// arrange
				string key = "abc";
				int maxConnectionCount = 19;

				// act
				ChannelConfig target = new ChannelConfig(key, maxConnectionCount);

				// assert
				Assert.Equal(key, target.Key);
				Assert.Equal(maxConnectionCount, target.MaxConnectionCount);
			}

			[Fact(DisplayName = "default")]
			public void @default() {
				// arrange

				// act
				ChannelConfig target = new ChannelConfig();

				// assert
				Assert.Null(target.Key);
				Assert.Equal(ChannelConfig.DefaultMaxConnectionCount, target.MaxConnectionCount);
			}

			[Fact(DisplayName = "maxConnectionCount: omitted")]
			public void maxConnectionCount_omitted() {
				// arrange
				string key = "abc";

				// act
				ChannelConfig target = new ChannelConfig(key);

				// assert
				Assert.Equal(key, target.Key);
				Assert.Equal(ChannelConfig.DefaultMaxConnectionCount, target.MaxConnectionCount);
			}

			#endregion
		}

		#endregion


		#region deserialization

		public class Deserialization {
			#region tests

			[Fact(DisplayName = "general")]
			public void deserialize_general() {
				// arrange
				string key = "abc";
				int maxConnectionCount = 23;
				string json = $@"{{
					""key"":""{key}"",
					""maxConnectionCount"":{maxConnectionCount}
				}}";

				// act
				ChannelConfig target = JsonSerializer.Deserialize<ChannelConfig>(json);

				// assert
				Assert.Equal(key, target.Key);
				Assert.Equal(maxConnectionCount, target.MaxConnectionCount);
			}

			[Fact(DisplayName = "empty")]
			public void deserialize_empty() {
				// arrange
				string json = "{}";

				// act
				ChannelConfig target = JsonSerializer.Deserialize<ChannelConfig>(json);

				// assert
				Assert.Null(target.Key);
				Assert.Equal(ChannelConfig.DefaultMaxConnectionCount, target.MaxConnectionCount);
			}

			[Fact(DisplayName = "maxConnectionCount: omitted")]
			public void deserialize_maxConnectionCount_omitted() {
				// arrange
				string key = "def";
				string json = $@"{{
					""key"":""{key}""
				}}";

				// act
				ChannelConfig target = JsonSerializer.Deserialize<ChannelConfig>(json);

				// assert
				Assert.Equal(key, target.Key);
				Assert.Equal(ChannelConfig.DefaultMaxConnectionCount, target.MaxConnectionCount);
			}

			#endregion
		}

		#endregion


		#region equality

		public class Equality {
			#region tests

			[Fact(DisplayName = "same, general")]
			public void same_general() {
				// arrange
				ChannelConfig sample1 = new ChannelConfig("abc", 34);
				ChannelConfig sample2 = new ChannelConfig("abc", 34);
				Debug.Assert(object.ReferenceEquals(sample1, sample2) == false);

				// act
				bool actual1 = (sample1 == sample2);
				bool actual2 = (sample1 != sample2);
				bool actual3 = sample1.Equals(sample2);
				bool actual4 = (sample1.GetHashCode() == sample2.GetHashCode());

				// assert
				Assert.True(actual1);
				Assert.False(actual2);
				Assert.True(actual3);
				Assert.True(actual4);
			}

			[Fact(DisplayName = "same, null")]
			public void same_null() {
				// arrange
				ChannelConfig? sample1 = null;
				ChannelConfig? sample2 = null;

				// act
				bool actual1 = (sample1 == sample2);
				bool actual2 = (sample1 != sample2);

				// assert
				Assert.True(actual1);
				Assert.False(actual2);
			}

			[Fact(DisplayName = "same, key: null")]
			public void same_key_null() {
				// arrange
				ChannelConfig sample1 = new ChannelConfig(null, 3);
				ChannelConfig sample2 = new ChannelConfig(null, 3);
				Debug.Assert(object.ReferenceEquals(sample1, sample2) == false);

				// act
				bool actual1 = (sample1 == sample2);
				bool actual2 = (sample1 != sample2);
				bool actual3 = sample1.Equals(sample2);
				bool actual4 = (sample1.GetHashCode() == sample2.GetHashCode());

				// assert
				Assert.True(actual1);
				Assert.False(actual2);
				Assert.True(actual3);
				Assert.True(actual4);
			}

			[Fact(DisplayName = "different, key")]
			public void different_key() {
				// arrange
				ChannelConfig sample1 = new ChannelConfig("abc", 34);
				ChannelConfig sample2 = new ChannelConfig("def", 34);

				// act
				bool actual1 = (sample1 == sample2);
				bool actual2 = (sample1 != sample2);
				bool actual3 = sample1.Equals(sample2);

				// assert
				Assert.False(actual1);
				Assert.True(actual2);
				Assert.False(actual3);
			}

			[Fact(DisplayName = "different, maxConnectionCount")]
			public void different_maxConnectionCount() {
				// arrange
				ChannelConfig sample1 = new ChannelConfig("abc", 34);
				ChannelConfig sample2 = new ChannelConfig("def", 33);

				// act
				bool actual1 = (sample1 == sample2);
				bool actual2 = (sample1 != sample2);
				bool actual3 = sample1.Equals(sample2);

				// assert
				Assert.False(actual1);
				Assert.True(actual2);
				Assert.False(actual3);
			}

			[Fact(DisplayName = "different, null")]
			public void different_null() {
				// arrange
				ChannelConfig sample1 = new ChannelConfig("abc", 34);
				ChannelConfig? sample2 = null;

				// act
				bool actual1 = (sample1 == sample2);
				bool actual2 = (sample2 == sample1);
				bool actual3 = (sample1 != sample2);
				bool actual4 = (sample2 != sample1);
				bool actual5 = sample1.Equals(sample2);

				// assert
				Assert.False(actual1);
				Assert.False(actual2);
				Assert.True(actual3);
				Assert.True(actual4);
				Assert.False(actual5);
			}

			[Fact(DisplayName = "different, type")]
			public void different_type() {
				// arrange
				ChannelConfig sample1 = new ChannelConfig("abc", 34);
				object sample2 = new object();

				// act
				bool actual = sample1.Equals(sample2);

				// assert
				Assert.False(actual);
			}

			#endregion
		}

		#endregion
	}
}
