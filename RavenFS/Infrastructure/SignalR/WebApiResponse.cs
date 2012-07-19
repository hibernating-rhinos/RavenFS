using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using SignalR;

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

        public Task WriteAsync(ArraySegment<byte> data)
        {
        	return Task.Factory.StartNew(() => message.Content = new ByteArrayContent(data.Array));
        }

		public Task EndAsync(ArraySegment<byte> data)
		{
			message.Content = new ByteArrayContent(data.Array);
            return TaskEx.FromResult(true);
        }

        public bool IsClientConnected
        {
            get { return true; }
        }

        public string ContentType { get; set; }
    }
}