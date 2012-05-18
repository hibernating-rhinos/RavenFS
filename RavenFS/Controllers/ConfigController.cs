using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using RavenFS.Notifications;
using RavenFS.Util;
using System.Linq;

namespace RavenFS.Controllers
{
	public class ConfigController : RavenController
	{
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
				});

		}

		public HttpResponseMessage Delete(string name)
		{
			Storage.Batch(accessor => accessor.DeleteConfig(name));
            Publisher.Publish(new ConfigChange() { Name = name, Action = ConfigChangeAction.Delete});
			return new HttpResponseMessage(HttpStatusCode.NoContent);
		}
	}
}