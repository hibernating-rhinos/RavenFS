using System;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RavenFS.Extensions;
using RavenFS.Infrastructure;
using RavenFS.Notifications;
using RavenFS.Search;
using RavenFS.Storage;
using RavenFS.Util;

namespace RavenFS
{
	using System.Linq;
	using System.Web;
	using System.Web.Http;
	using System.Web.Http.SelfHost;
	using NLog;
	using Synchronization;
	using Synchronization.Conflictuality;
	using Synchronization.Rdc.Wrapper;

	public class RavenFileSystem : IDisposable
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		private readonly string path;
		private readonly TransactionalStorage storage;
		private readonly IndexStorage search;
		private readonly SigGenerator sigGenerator;
		private readonly NotificationPublisher notificationPublisher;
		private readonly HistoryUpdater historyUpdater;
		private readonly FileLockManager fileLockManager;
		private readonly SynchronizationTask synchronizationTask;
		private readonly ConflictActifactManager conflictActifactManager;
		private readonly ConflictDetector conflictDetector;
		private readonly ConflictResolver conflictResolver;
		private Uri baseAddress;

		public TransactionalStorage Storage
		{
			get { return storage; }
		}

		public IndexStorage Search
		{
			get { return search; }
		}

		public BufferPool BufferPool { get; private set; }

		public RavenFileSystem(string path = @"~\Data")
		{
			this.path = path.ToFullPath();
			storage = new TransactionalStorage(this.path, new NameValueCollection());
			search = new IndexStorage(this.path, new NameValueCollection());
			sigGenerator = new SigGenerator();
			var replicationHiLo = new ReplicationHiLo(storage);
			var sequenceActions = new SequenceActions(storage);
			notificationPublisher = new NotificationPublisher();
			fileLockManager = new FileLockManager();
			storage.Initialize();
			search.Initialize();
			var uuidGenerator = new UuidGenerator(sequenceActions);
			historyUpdater = new HistoryUpdater(storage, replicationHiLo, uuidGenerator);
			BufferPool = new BufferPool(1024 * 1024 * 1024, 65 * 1024);
			conflictActifactManager = new ConflictActifactManager(storage);
			conflictDetector = new ConflictDetector();
			conflictResolver = new ConflictResolver();
			synchronizationTask = new SynchronizationTask(this, storage, sigGenerator, notificationPublisher);

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

		public SigGenerator SigGenerator
		{
			get { return sigGenerator; }
		}

		public NotificationPublisher Publisher
		{
			get { return notificationPublisher; }
		}

		public HistoryUpdater HistoryUpdater
		{
			get { return historyUpdater; }
		}

		public FileLockManager FileLockManager
		{
			get { return fileLockManager; }
		}

		public SynchronizationTask SynchronizationTask
		{
			get { return synchronizationTask; }
		}

		public ConflictActifactManager ConflictActifactManager
		{
			get { return conflictActifactManager; }
		}

		public ConflictDetector ConflictDetector
		{
			get { return conflictDetector; }
		}

		public ConflictResolver ConflictResolver
		{
			get { return conflictResolver; }
		}

		public string ServerUrl
		{
			get
			{
				if (HttpContext.Current != null)// running in IIS, let us figure out how
				{
					var url = HttpContext.Current.Request.Url;
					return new UriBuilder(url)
					{
						Path = HttpContext.Current.Request.ApplicationPath,
						Query = ""
					}.Uri.ToString();
				}

				return baseAddress.ToString();
			}
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		public void Start(HttpConfiguration config)
		{
			var selfHost = config as HttpSelfHostConfiguration;

			if (selfHost != null)
			{
				baseAddress = selfHost.BaseAddress;
			}

			config.DependencyResolver = new DelegateDependencyResolver(type =>
			{
				if (type == typeof(RavenFileSystem))
					return this;
				return null;
			}, type =>
			{
				if (type == typeof(RavenFileSystem))
					return new[] { this };
				return Enumerable.Empty<object>();
			});

			// we don't like XML, let us remove support for it.
			config.Formatters.XmlFormatter.SupportedMediaTypes.Clear();

			config.Formatters.JsonFormatter.SerializerSettings.Converters.Add(new IsoDateTimeConverter());
			// the default json parser can't handle NameValueCollection
			config.Formatters.JsonFormatter.SerializerSettings.Converters.Add(new NameValueCollectionJsonConverter());
			
			config.Routes.MapHttpRoute(
				name: "ClientAccessPolicy.xml",
				routeTemplate: "ClientAccessPolicy.xml",
				defaults: new { controller = "static", action = "ClientAccessPolicy" });

			config.Routes.MapHttpRoute(
			name: "favicon.ico",
			routeTemplate: "favicon.ico",
			defaults: new { controller = "static", action = "FavIcon" });

			config.Routes.MapHttpRoute(
				name: "RavenFS.Studio.xap",
				routeTemplate: "RavenFS.Studio.xap",
				defaults: new { controller = "static", action = "RavenStudioXap" });

			config.Routes.MapHttpRoute(
				name: "Empty",
				routeTemplate: "",
				defaults: new { controller = "static", action = "Root" });


			config.Routes.MapHttpRoute(
				name: "rdc",
				routeTemplate: "rdc/{action}/{*filename}",
				defaults: new { controller = "rdc", filename = RouteParameter.Optional }
				);

			config.Routes.MapHttpRoute(
				name: "synchronizationWithFile",
				routeTemplate: "synchronization/{action}/{*filename}",
				defaults: new { controller = "synchronization", filename = RouteParameter.Optional }
				);

			config.Routes.MapHttpRoute(
				name: "folders",
				routeTemplate: "folders/{action}/{*directory}",
				defaults: new { controller = "folders", directory = RouteParameter.Optional }
				);

			config.Routes.MapHttpRoute(
				name: "search",
				routeTemplate: "search/{action}",
				defaults: new { controller = "search", action = "get" }
				);

			config.Routes.MapHttpRoute(
				name: "logs",
				routeTemplate: "search/{action}/{*type}",
				defaults: new { controller = "logs", action = "get", type = RouteParameter.Optional }
				);

			config.Routes.MapHttpRoute(
				name: "Default",
				routeTemplate: "{controller}/{*name}",
				defaults: new { controller = "files", name = RouteParameter.Optional }
				);

			config.Routes.MapHttpRoute(
				"Notifications",
				routeTemplate: "notifications/{*path}",
				defaults: new { controller = "notifications" });

		}
	}
}