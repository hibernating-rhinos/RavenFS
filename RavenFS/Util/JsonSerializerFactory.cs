using Newtonsoft.Json;

namespace RavenFS.Util
{
	public static class JsonSerializerFactory
	{
		public static JsonSerializer Create()
		{
			return new JsonSerializer
			{
				Converters =
					{
						new NameValueCollectionJsonConverter()
					}
			};
		}
	}
}