using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RavenFS.Controllers
{
	using System.Web.Http;
	using Client;
	using NLog;
	using ConfigChange = Notifications.ConfigChange;
	using ConfigChangeAction = Notifications.ConfigChangeAction;
	using NameValueCollectionJsonConverter = Util.NameValueCollectionJsonConverter;

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

		public async Task<HttpResponseMessage> Put(string name)
		{
			var jsonSerializer = new JsonSerializer
			{
				Converters =
						{
							new NameValueCollectionJsonConverter()
						}
			};
			var contentStream = await Request.Content.ReadAsStreamAsync();

			var nameValueCollection =
				jsonSerializer.Deserialize<NameValueCollection>(new JsonTextReader(new StreamReader(contentStream)));

			var shouldRetry = false;
			var retries = 128;

			do
			{
				try
				{
					Storage.Batch(accessor => accessor.SetConfig(name, nameValueCollection));
				}
				catch (ConcurrencyException ce)
				{
					if (retries-- > 0)
					{
						shouldRetry = true;
						continue;
					}
					throw ConcurrencyResponseException(ce);
				}
			} while (shouldRetry);
			Publisher.Publish(new ConfigChange() {Name = name, Action = ConfigChangeAction.Set});

			log.Debug("Config '{0}' was inserted", name);

			return new HttpResponseMessage(HttpStatusCode.Created);
		}

		public HttpResponseMessage Delete(string name)
		{
			var shouldRetry = false;
			var retries = 128;

			do
			{
				try
				{
					Storage.Batch(accessor => accessor.DeleteConfig(name));
				}
				catch (ConcurrencyException ce)
				{
					if (retries-- > 0)
					{
						shouldRetry = true;
						continue;
					}
					throw ConcurrencyResponseException(ce);
				}
			} while (shouldRetry);

			Publisher.Publish(new ConfigChange() { Name = name, Action = ConfigChangeAction.Delete });

			log.Debug("Config '{0}' was deleted", name);
			return new HttpResponseMessage(HttpStatusCode.NoContent);
		}
	}
}