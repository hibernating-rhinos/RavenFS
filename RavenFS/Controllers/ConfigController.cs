using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RavenFS.Notifications;
using RavenFS.Util;

namespace RavenFS.Controllers
{
	using NLog;

	public class ConfigController : RavenController
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		public string[] Get()
		{
			string[] names = null;
			Storage.Batch(accessor =>
			{
				names = accessor.GetConfigNames(Paging.Start, Paging.PageSize).ToArray();
			});
			return names;
		}

		public HttpResponseMessage Get(string name)
		{
			try
			{
				NameValueCollection nameValueCollection = null;
				Storage.Batch(accessor =>
				{
					nameValueCollection = accessor.GetConfig(name);
				});
				return Request.CreateResponse(HttpStatusCode.OK, nameValueCollection);
			}
			catch (FileNotFoundException)
			{
				return Request.CreateResponse(HttpStatusCode.NotFound);
			}

		}

		public Task Put(string name)
		{
			var jsonSerializer = new JsonSerializer
			{
				Converters =
						{
							new NameValueCollectionJsonConverter()
						}
			};
			return Request.Content.ReadAsStreamAsync()
				.ContinueWith(task =>
				{
					var nameValueCollection = jsonSerializer.Deserialize<NameValueCollection>(new JsonTextReader(new StreamReader(task.Result)));
					Storage.Batch(accessor => accessor.SetConfig(name, nameValueCollection));
					Publisher.Publish(new ConfigChange() { Name = name, Action = ConfigChangeAction.Set });

					log.Debug("Config '{0}' was inserted", name);
				});

		}

		public HttpResponseMessage Delete(string name)
		{
			Storage.Batch(accessor => accessor.DeleteConfig(name));
			Publisher.Publish(new ConfigChange() { Name = name, Action = ConfigChangeAction.Delete });

			log.Debug("Config '{0}' was deleted", name);
			return new HttpResponseMessage(HttpStatusCode.NoContent);
		}
	}
}