using System;
using System.Collections.Specialized;
using Newtonsoft.Json;

namespace RavenFS.Util
{
	public class NameValueCollectionJsonConverter : JsonConverter
	{
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			var collection = (NameValueCollection) value;

			writer.WriteStartObject();

			foreach (var key in collection.AllKeys)
			{
				writer.WritePropertyName(key);

				var values = collection.GetValues(key);
				if(values == null)
				{
					writer.WriteNull();
					continue;
				}
				if(values.Length == 1)
				{
					writer.WriteValue(values[0]);
				}
				else
				{
					writer.WriteStartArray();

					foreach (var item in values)
					{
						writer.WriteValue(item);
					}

					writer.WriteEndArray();
				}

			}
			writer.WriteEndObject();
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			throw new NotImplementedException();
		}

		public override bool CanConvert(Type objectType)
		{
			return objectType.IsSubclassOf(typeof(NameValueCollection));
		}
	}
}