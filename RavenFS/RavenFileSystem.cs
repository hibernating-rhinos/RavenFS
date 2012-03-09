using System;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Web.Http;
using System.Web.Http.ModelBinding;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RavenFS.Extensions;
using RavenFS.Infrastructure;
using RavenFS.Infrastructure.Workarounds;
using RavenFS.Rdc.Wrapper;
using RavenFS.Search;
using RavenFS.Storage;
using RavenFS.Util;

namespace RavenFS
{
	public class RavenFileSystem : IDisposable
	{
		private readonly string path;
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

		public RavenFileSystem(string path = "~")
		{
			this.path = path.ToFullPath();
			storage = new TransactionalStorage(this.path, new NameValueCollection());
			search = new IndexStorage(this.path, new NameValueCollection());
			signatureRepository = new SimpleSignatureRepository(this.path);
			sigGenerator = new SigGenerator(signatureRepository);
			storage.Initialize();
			search.Initialize();
			BufferPool = new BufferPool(1024 * 1024 * 1024, 65 * 1024);

			AppDomain.CurrentDomain.ProcessExit += ShouldDispose;
			AppDomain.CurrentDomain.DomainUnload += ShouldDispose;
		}

		public string Path
		{
			get { return path; }
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

		public SimpleSignatureRepository SignatureRepository
		{
			get { return signatureRepository; }
		}

		public SigGenerator SigGenerator
		{
			get { return sigGenerator; }
		}


		[MethodImpl(MethodImplOptions.Synchronized)]
		public void Start(HttpConfiguration config)
		{
			config.ServiceResolver.SetResolver(type =>
			{
				if(type == typeof(RavenFileSystem))
					return this;
				return null;
			}, type =>
			{
				if (type == typeof(RavenFileSystem))
					return new[] {this};
				return Enumerable.Empty<object>();
			});

			// we don't like XML, let us remove support for it.
			config.Formatters.XmlFormatter.SupportedMediaTypes.Clear();

			// Workaround for an issue with paraemters and body not mixing properly
			config.ServiceResolver.SetService(typeof(IRequestContentReadPolicy), new ReadAsSingleObjectPolicy());

			// the default json parser can't handle NameValueCollection
			var serializerSettings = new JsonSerializerSettings();
			serializerSettings.Converters.Add(new IsoDateTimeConverter());
			serializerSettings.Converters.Add(new NameValueCollectionJsonConverter());
			var indexOfJson = config.Formatters.IndexOf(config.Formatters.JsonFormatter);
			config.Formatters[indexOfJson] = new JsonNetFormatter(serializerSettings); 
 

			config.Routes.MapHttpRoute(
				name: "rdc",
				routeTemplate: "rdc/{action}/{*filename}",
				defaults: new { controller = "rdc", filename = RouteParameter.Optional }
				);


			config.Routes.MapHttpRoute(
				name: "folders",
				routeTemplate: "folders/{ation}/{*folder}",
				defaults: new { controller = "folders", folder = RouteParameter.Optional }
				);

			config.Routes.MapHttpRoute(
				name: "Default",
				routeTemplate: "{controller}/{*name}",
				defaults: new { controller = "files", name = RouteParameter.Optional }
				);

		}
	}
}