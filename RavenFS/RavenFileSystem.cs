using System;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Converters;
using RavenFS.Extensions;
using RavenFS.Infrastructure;
using RavenFS.Infrastructure.Connections;
using RavenFS.Notifications;
using RavenFS.Search;
using RavenFS.Storage;
using RavenFS.Util;

namespace RavenFS
{
	using System.Linq;
	using System.Web.Http;
	using System.Web.Http.Hosting;
	using Config;
	using Synchronization;
	using Synchronization.Conflictuality;
	using Synchronization.Rdc.Wrapper;

	public class RavenFileSystem : IDisposable
	{
		private readonly InMemoryConfiguration systemConfiguration;
		private readonly TransactionalStorage storage;
		private readonly IndexStorage search;
		private readonly SigGenerator sigGenerator;
		private readonly NotificationPublisher notificationPublisher;
		private readonly Historian historian;
		private readonly FileLockManager fileLockManager;
		private readonly SynchronizationTask synchronizationTask;
		private readonly StorageOperationsTask storageOperationsTask;
		private readonly ConflictArtifactManager conflictArtifactManager;
		private readonly ConflictDetector conflictDetector;
		private readonly ConflictResolver conflictResolver;
	    private readonly TransportState transportState;

		public TransactionalStorage Storage
		{
			get { return storage; }
		}

		public IndexStorage Search
		{
			get { return search; }
		}

		public BufferPool BufferPool { get; private set; }

		public RavenFileSystem(InMemoryConfiguration systemConfiguration)
		{
			this.systemConfiguration = systemConfiguration;

			storage = new TransactionalStorage(systemConfiguration.DataDirectory, systemConfiguration.Settings);
			search = new IndexStorage(systemConfiguration.IndexStoragePath, systemConfiguration.Settings);
			sigGenerator = new SigGenerator();
			var replicationHiLo = new ReplicationHiLo(storage);
			var sequenceActions = new SequenceActions(storage);
		    transportState = new TransportState();
		    notificationPublisher = new NotificationPublisher(transportState);
		    fileLockManager = new FileLockManager();
			storage.Initialize();
			search.Initialize();
			var uuidGenerator = new UuidGenerator(sequenceActions);
			historian = new Historian(storage, replicationHiLo, uuidGenerator);
			BufferPool = new BufferPool(1024 * 1024 * 1024, 65 * 1024);
			conflictArtifactManager = new ConflictArtifactManager(storage,search);
			conflictDetector = new ConflictDetector();
			conflictResolver = new ConflictResolver();
			synchronizationTask = new SynchronizationTask(storage, sigGenerator, notificationPublisher);
			storageOperationsTask = new StorageOperationsTask(storage, search, notificationPublisher);

			AppDomain.CurrentDomain.ProcessExit += ShouldDispose;
			AppDomain.CurrentDomain.DomainUnload += ShouldDispose;
		}

		public InMemoryConfiguration Configuration
		{
			get { return systemConfiguration; }
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
			BufferPool.Dispose();
		}

		public SigGenerator SigGenerator
		{
			get { return sigGenerator; }
		}

		public NotificationPublisher Publisher
		{
			get { return notificationPublisher; }
		}

		public Historian Historian
		{
			get { return historian; }
		}

		public FileLockManager FileLockManager
		{
			get { return fileLockManager; }
		}

		public SynchronizationTask SynchronizationTask
		{
			get { return synchronizationTask; }
		}

		public StorageOperationsTask StorageOperationsTask
		{
			get { return storageOperationsTask; }
		}

		public ConflictArtifactManager ConflictArtifactManager
		{
			get { return conflictArtifactManager; }
		}

		public ConflictDetector ConflictDetector
		{
			get { return conflictDetector; }
		}

		public ConflictResolver ConflictResolver
		{
			get { return conflictResolver; }
		}

	    public TransportState TransportState
	    {
	        get { return transportState; }
	    }

	    [MethodImpl(MethodImplOptions.Synchronized)]
		public void Start(HttpConfiguration config)
		{
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

			config.Services.Replace(typeof(IHostBufferPolicySelector), new NoBufferPolicySelector());

			config.MessageHandlers.Add(new CachePreventingHandler());

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
				name: "Id",
				routeTemplate: "id",
				defaults: new { controller = "static", action = "Id" });

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
				name: "synchronization",
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

		    StorageOperationsTask.ResumeFileRenamingAsync();
		}
	}
}