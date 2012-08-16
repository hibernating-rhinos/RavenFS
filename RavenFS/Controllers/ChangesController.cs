using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using RavenFS.Infrastructure.Connections;

namespace RavenFS.Controllers
{
    public class ChangesController : RavenController
    {
        public HttpResponseMessage Events(string id)
        {
            var eventsTransport = new EventsTransport(id);
            RavenFileSystem.TransportState.Register(eventsTransport);

            return eventsTransport.GetResponse();
        }
    }
}