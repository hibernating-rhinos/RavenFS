using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Util;
using SignalR.Hosting;

namespace RavenFS.Infrastructure.SignalR
{
    public class WebApiResponse : IResponse
    {
        private readonly HttpResponseMessage message;
        private MemoryStream memoryStream;

        public WebApiResponse(HttpResponseMessage message)
        {
            this.message = message;
            this.memoryStream = new MemoryStream();
            message.Content = new StreamContent(this.memoryStream);
        }

        public Task WriteAsync(string data)
        {
            throw new NotImplementedException();
        }

        public Task EndAsync(string data)
        {
            message.Content = new StringContent(data, Encoding.UTF8, ContentType);
            return TaskEx.FromResult(true);
        }

        public bool IsClientConnected
        {
            get { return true; }
        }

        public string ContentType { get; set; }
    }
}