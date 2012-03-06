using System;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.CompilerServices;
using System.Web.Http;
using System.Web.Http.ModelBinding;
using System.Web.Http.Routing;
using System.Web.Routing;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RavenFS.Rdc.Wrapper;
using RavenFS.Search;
using RavenFS.Storage;
using RavenFS.Util;
using RavenFS.Web.Infrastructure;
using RavenFS.Web.Infrastructure.Workarounds;

namespace RavenFS.Web
{
	public class RavenFileSystem : IDisposable
	{
		public static RavenFileSystem Instance { get; private set; }

		private readonly TransactionalStorage storage;
		private readonly IndexStorage search;
		private readonly SimpleSignatureRepository signatureRepository;
		private readonly SigGenerator sigGenerator;

		public TransactionalStorage Storage
		{
			get { return storage; }
		}

		public IndexStorage Search
		{
			get { return search; }
		}

		public BufferPool BufferPool { get; private set; }

		public RavenFileSystem()
		{
			storage = new TransactionalStorage("Data.ravenfs", new NameValueCollection());
			search = new IndexStorage("Index.ravenfs", new NameValueCollection());
			signatureRepository = new SimpleSignatureRepository(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "localrepo"));
			sigGenerator = new SigGenerator(signatureRepository);
			storage.Initialize();
			search.Initialize();
			BufferPool = new BufferPool(1024 * 1024 * 1024, 65 * 1024);

			AppDomain.CurrentDomain.ProcessExit += ShouldDispose;
			AppDomain.CurrentDomain.DomainUnload += ShouldDispose;
		}

		private void ShouldDispose(object sender, EventArgs eventArgs)
		{
			Dispose();
		}

		public void Dispose()
		{
			AppDomain.CurrentDomain.ProcessExit -= ShouldDispose;
			AppDomain.CurrentDomain.DomainUnload -= ShouldDispose;
			
			storage.Dispose();
			search.Dispose();
			sigGenerator.Dispose();
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		public static void Stop()
		{
			using(Instance)
			{
				Instance = null;
			}
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		public static void Start(HttpConfiguration config)
		{
			if(Instance != null)
			{
				throw new InvalidOperationException("Already setup");
			}

			// we don't like XML, let us remove support for it.
			config.Formatters.XmlFormatter.SupportedMediaTypes.Clear();

			// Workaround for an issue with paraemters and body not mixing properly
			config.ServiceResolver.SetService(typeof(IRequestContentReadPolicy), new ReadAsSingleObjectPolicy());

			// the default json parser can't handle NameValueCollection
			var serializerSettings = new JsonSerializerSettings();
			serializerSettings.Converters.Add(new IsoDateTimeConverter());
			var indexOfJson = config.Formatters.IndexOf(config.Formatters.JsonFormatter);
			config.Formatters[indexOfJson] = new JsonNetFormatter(serializerSettings); 
 

			config.Routes.MapHttpRoute(
				name: "Files",
				routeTemplate: "files/{*filename}",
				defaults: new { controller = "files", filename = RouteParameter.Optional }
				);

			config.Routes.MapHttpRoute(
				name: "Api Default",
				routeTemplate: "api/{controller}"
				);

			config.Routes.MapHttpRoute(
				name: "Root",
				routeTemplate: "",
				defaults: new {controller = "files"});

			Instance = new RavenFileSystem();
		}
	}
}