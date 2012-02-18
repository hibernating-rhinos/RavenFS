using System;
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Web;
using NLog;
using RavenFS.Search;
using RavenFS.Util;
using Rdc.Wrapper;

namespace RavenFS.Infrastructure
{
	public class RouterHandlerFactory : IHttpHandlerFactory, IDisposable
	{
		private static readonly Logger logger = LogManager.GetCurrentClassLogger();
		private readonly CompositionContainer compositionContainer;
		private readonly BufferPool globalBufferPool = new BufferPool(1024 * 1024 * 1024, 65 * 1024);


		[ImportMany]
		public Lazy<AbstractAsyncHandler, HandlerMetadata>[] Handlers { get; set; }

		private readonly static Storage.TransactionalStorage storage;
		private static readonly Search.IndexStorage search;
	    private static SigGenerator sigGenerator;
	    private static NeedListGenerator needListGenerator;
        private static ISignatureRepository signatureRepository;
		
        static RouterHandlerFactory()
		{
			storage = new Storage.TransactionalStorage("Data.ravenfs", new NameValueCollection());
			search = new IndexStorage("Index.ravenfs", new NameValueCollection());
            signatureRepository = new SimpleSignatureRepository(Path.GetTempPath());
            sigGenerator = new SigGenerator(signatureRepository);
            needListGenerator = new NeedListGenerator(signatureRepository);
			storage.Initialize();
			search.Initialize();

			AppDomain.CurrentDomain.ProcessExit += (sender, args) =>
			{
				storage.Dispose();
				search.Dispose();
                sigGenerator.Dispose();
                needListGenerator.Dispose();
			};
		}

		public RouterHandlerFactory()
		{
			compositionContainer = new CompositionContainer(new AssemblyCatalog(typeof(RouterHandlerFactory).Assembly));
			compositionContainer.SatisfyImportsOnce(this);

			foreach (var handler in Handlers)
			{
				handler.Value.Initialize(globalBufferPool, handler.Metadata.Url, storage, search, sigGenerator, needListGenerator, signatureRepository);
			}
		}

		/// <summary>
		/// Returns an instance of a class that implements the <see cref="T:System.Web.IHttpHandler"/> interface.
		/// </summary>
		/// <returns>
		/// A new <see cref="T:System.Web.IHttpHandler"/> object that processes the request.
		/// </returns>
		/// <param name="context">An instance of the <see cref="T:System.Web.HttpContext"/> class that provides references to intrinsic server objects (for example, Request, Response, Session, and Server) used to service HTTP requests. </param><param name="requestType">The HTTP data transfer method (GET or POST) that the client uses. </param><param name="url">The <see cref="P:System.Web.HttpRequest.RawUrl"/> of the requested resource. </param><param name="pathTranslated">The <see cref="P:System.Web.HttpRequest.PhysicalApplicationPath"/> to the requested resource. </param>
		public IHttpHandler GetHandler(HttpContext context, string requestType, string url, string pathTranslated)
		{

			var result = Handlers
				.Where(handler => handler.Metadata.Matches(requestType, url))
				.FirstOrDefault();

			logger.Debug("{0} {1} -> {2}", requestType, url, ((object)result ?? "Unhandled"));

			if(result == null)
			{
				throw new InvalidOperationException("Could not find a handler for request: " + requestType + " " + url);
			}

			return result.Value;
		}

		/// <summary>
		/// Enables a factory to reuse an existing handler instance.
		/// </summary>
		/// <param name="handler">The <see cref="T:System.Web.IHttpHandler"/> object to reuse. </param>
		public void ReleaseHandler(IHttpHandler handler)
		{
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public void Dispose()
		{
			globalBufferPool.Dispose();
		}
	}
}