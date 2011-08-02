using System;
using System.Collections.Specialized;
using System.IO;
using Newtonsoft.Json;

namespace RavenFS.Client
{
	public class NameValueCollectionJsonConverter : JsonConverter
	{
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			throw new NotImplementedException();
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var collection = new NameValueCollection();

			while (reader.Read())
			{
				if (reader.TokenType == JsonToken.EndObject)
					break;

				var key = (string)reader.Value;

				if (reader.Read() == false)
					throw new InvalidDataException("Expected PropertyName, got " + reader.TokenType);

				if(reader.TokenType ==JsonToken.StartArray)
				{
					var values = serializer.Deserialize<string[]>(reader);
					foreach (var value in values)
					{
						collection.Add(key, value);
					}
				}
				else
				{
					collection.Add(key, (string)reader.Value);
				}
			}

			return collection;
		}

		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof (NameValueCollection);
		}
	}
}