using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using RavenFS.Infrastructure.SignalR;
using SignalR;
using SignalR.AspNetWebApi;
using SignalR.Hosting;
using SignalR.Infrastructure;

namespace RavenFS.Infrastructure
{
    public class ConnectionController<T> : ApiController where T : PersistentConnection
    {
        private T connection;

        public ConnectionController()
        {
        }

        protected IDependencyResolver Resolver
        {
            get
            {
                return ((RavenFileSystem) ControllerContext.Configuration.DependencyResolver.GetService(typeof (RavenFileSystem)))
                    .Publisher.SignalRDependencyResolver;
            }
        }

        protected override void Initialize(System.Web.Http.Controllers.HttpControllerContext controllerContext)
        {
            var factory = new PersistentConnectionFactory(Resolver);
        	connection = (T) factory.CreateInstance(typeof (T));
            connection.Initialize(Resolver);

            base.Initialize(controllerContext);
        }

        public override System.Threading.Tasks.Task<HttpResponseMessage> ExecuteAsync(System.Web.Http.Controllers.HttpControllerContext controllerContext, System.Threading.CancellationToken cancellationToken)
        {
            ControllerContext = controllerContext;

            var factory = new PersistentConnectionFactory(Resolver);
        	connection = (T) factory.CreateInstance(typeof (T));
            connection.Initialize(Resolver);

            var response = new HttpResponseMessage();
            var hostContext = new HostContext(new WebApiRequest(controllerContext.Request), new WebApiResponse(response), User);

            return connection.ProcessRequestAsync(hostContext).ContinueWith(t => response);
        }
    }
}