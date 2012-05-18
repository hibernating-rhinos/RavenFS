using System;
using System.IO;
using System.Net;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RavenFS.Infrastructure
{
	using System.Net.Http;

	public class JsonNetFormatter : MediaTypeFormatter
	{
		private readonly JsonSerializerSettings _jsonSerializerSettings;

		public JsonNetFormatter(JsonSerializerSettings jsonSerializerSettings)
		{
			_jsonSerializerSettings = jsonSerializerSettings ?? new JsonSerializerSettings();

			// Fill out the mediatype and encoding we support 
			SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/json"));
			SupportedEncodings.Add(new UTF8Encoding(false, true));
		}

		public override bool CanReadType(Type type)
		{
			return true;
		}

		public override bool CanWriteType(Type type)
		{
			return true;
		}

		public override Task<object> ReadFromStreamAsync(Type type, Stream stream, HttpContent content, IFormatterLogger formatterLogger)
		{
			// Create a serializer 
			JsonSerializer serializer = JsonSerializer.Create(_jsonSerializerSettings);

			// Create task reading the content 
			return Task.Factory.StartNew(() =>
			{
				using (StreamReader streamReader = new StreamReader(stream, SupportedEncodings[0]))
				{
					using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
					{
						return serializer.Deserialize(jsonTextReader, type);
					}
				}
			});
		}

		public override Task WriteToStreamAsync(Type type, object value, Stream stream, HttpContent content, TransportContext transportContext)
		{
			// Create a serializer 
			JsonSerializer serializer = JsonSerializer.Create(_jsonSerializerSettings);

			// Create task writing the serialized content 
			return Task.Factory.StartNew(() =>
			{
				using (StreamWriter streamWriter = new StreamWriter(stream, SupportedEncodings[0]))
				{
					using (JsonTextWriter jsonTextWriter = new JsonTextWriter(streamWriter))
					{
						serializer.Serialize(jsonTextWriter, value);
					}
				}
			});
		}
	}
}