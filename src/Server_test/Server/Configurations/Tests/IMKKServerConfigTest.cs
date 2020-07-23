using System;
using System.IO;
using Utf8Json;
using Xunit;

namespace IMKK.Server.Configurations.Tests {
	public class IMKKServerConfigTest {
		#region constructor

		public class Constructor {
			#region tests

			[Fact(DisplayName = "general")]
			public void general() {
				// arrange
				ChannelConfig[] channels = new ChannelConfig[] {
					new ChannelConfig("abc"),
					new ChannelConfig("def")
				};

				// act
				IMKKServerConfig target = new IMKKServerConfig(channels);

				// assert
				Assert.Equal(channels, target.Channels);
			}

			[Fact(DisplayName = "channels: null")]
			public void channels_null() {
				// arrange

				// act
				IMKKServerConfig target = new IMKKServerConfig(null);

				// assert
				Assert.Empty(target.Channels);
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
				ChannelConfig[] channels = new ChannelConfig[] {
					new ChannelConfig("abc"),
					new ChannelConfig("def")
				};
				string json = $@"{{
					""channels"":[
						{{""key"":""{channels[0].Key}"",""maxConnectionCount"":{channels[0].MaxConnectionCount}}},
						{{""key"":""{channels[1].Key}"",""maxConnectionCount"":{channels[1].MaxConnectionCount}}}
					]
				}}";

				// act
				IMKKServerConfig target = JsonSerializer.Deserialize<IMKKServerConfig>(json);

				// assert
				Assert.Equal(channels, target.Channels);
			}

			[Fact(DisplayName = "empty")]
			public void deserialize_empty() {
				// arrange
				string json = "{}";

				// act
				IMKKServerConfig target = JsonSerializer.Deserialize<IMKKServerConfig>(json);

				// assert
				Assert.Empty(target.Channels);
			}

			[Fact(DisplayName = "maxConnectionCount: omitted")]
			public void deserialize_maxConnectionCount_omitted() {
				// arrange
				ChannelConfig[] channels = new ChannelConfig[] {
					new ChannelConfig("abc"),
					new ChannelConfig("def")
				};
				string json = $@"{{
					""channels"":[
						{{""key"":""{channels[0].Key}""}},
						{{""key"":""{channels[1].Key}""}}
					]
				}}";

				// act
				IMKKServerConfig target = JsonSerializer.Deserialize<IMKKServerConfig>(json);

				// assert
				Assert.Equal(channels, target.Channels);
			}

			#endregion
		}

		#endregion


		#region CreateFromJson

		public class CreateFromJson {
			#region tests

			[Fact(DisplayName = "general")]
			public void general() {
				// arrange
				ChannelConfig[] channels = new ChannelConfig[] {
					new ChannelConfig("abc"),
					new ChannelConfig("def")
				};
				string json = $@"{{
					""channels"":[
						{{""key"":""{channels[0].Key}"",""maxConnectionCount"":{channels[0].MaxConnectionCount}}},
						{{""key"":""{channels[1].Key}"",""maxConnectionCount"":{channels[1].MaxConnectionCount}}}
					]
				}}";

				// act
				IMKKServerConfig target = IMKKServerConfig.CreateFromJson(json);

				// assert
				Assert.Equal(channels, target.Channels);
			}

			[Fact(DisplayName = "json: null")]
			public void json_null() {
				// arrange
				string json = null!;

				// act
				ArgumentNullException actual = Assert.Throws<ArgumentNullException>(() => {
					IMKKServerConfig.CreateFromJson(json);
				});

				// assert
				Assert.Equal("json", actual.ParamName);
			}

			#endregion
		}

		#endregion


		#region CreateFromJsonFile

		public class CreateFromJsonFile {
			#region tests

			[Fact(DisplayName = "general")]
			public void general() {
				string filePath = Path.GetTempFileName();
				try {
					// arrange
					ChannelConfig[] channels = new ChannelConfig[] {
						new ChannelConfig("abc"),
						new ChannelConfig("def")
					};
					string json = $@"{{
						""channels"":[
							{{""key"":""{channels[0].Key}"",""maxConnectionCount"":{channels[0].MaxConnectionCount}}},
							{{""key"":""{channels[1].Key}"",""maxConnectionCount"":{channels[1].MaxConnectionCount}}}
						]
					}}";
					File.WriteAllText(filePath, json);

					// act
					IMKKServerConfig target = IMKKServerConfig.CreateFromJsonFile(filePath);

					// assert
					Assert.Equal(channels, target.Channels);

				} finally {
					File.Delete(filePath);
				}
			}

			[Fact(DisplayName = "json: null")]
			public void json_null() {
				// arrange
				string filePath = null!;

				// act
				ArgumentNullException actual = Assert.Throws<ArgumentNullException>(() => {
					IMKKServerConfig.CreateFromJsonFile(filePath);
				});

				// assert
				Assert.Equal("filePath", actual.ParamName);
			}

			#endregion
		}

		#endregion
	}
}
