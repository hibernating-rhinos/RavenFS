using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using RavenFS.Infrastructure.Connections;

namespace RavenFS.Controllers
{
    public class ChangesController : RavenController
    {
        [AcceptVerbs("GET")]
        public HttpResponseMessage Events(string id)
        {
            var eventsTransport = new EventsTransport(id);
            RavenFileSystem.TransportState.Register(eventsTransport);

            return eventsTransport.GetResponse();
        }

        [AcceptVerbs("GET")]
        public HttpResponseMessage Config(string id, string command)
        {
            var response = new HttpResponseMessage();

            if (string.IsNullOrEmpty(id))
            {
                throw BadRequestException("id query string parameter is mandatory when using changes/config endpoint");
            }

            var connectionState = RavenFileSystem.TransportState.For(id);

            if (Match(command, "disconnect"))
            {
                RavenFileSystem.TransportState.Disconnect(id);
            }
            else
            {
                throw BadRequestException("command argument is mandatory");
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        private bool Match(string x, string y)
        {
            return string.Equals(x, y, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}