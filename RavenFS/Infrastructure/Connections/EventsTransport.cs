// -----------------------------------------------------------------------
//  <copyright file="EventsTransport.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Newtonsoft.Json;

namespace RavenFS.Infrastructure.Connections
{
	public class EventsTransport
	{
		private readonly Timer heartbeat;

		private readonly Logger log = LogManager.GetCurrentClassLogger();
		
		public string Id { get; private set; }
		public bool Connected { get; set; }

		public event Action Disconnected = delegate { };

		private Task InitTask;
        private TaskCompletionSource<Stream> streamAvailableTcs;

		public EventsTransport(string id)
		{
			Connected = true;
		    Id = id;
			if (string.IsNullOrEmpty(Id))
				throw new ArgumentException("Id is mandatory");

			heartbeat = new Timer(Heartbeat);

		    streamAvailableTcs = new TaskCompletionSource<Stream>();
		}

		public HttpResponseMessage GetResponse()
		{
		    var response = new HttpResponseMessage();
            response.Content = new PushStreamContent(HandleStreamAvailable, "text/event-stream");

			InitTask = SendAsync(new { Type = "Initialized" });
			Thread.MemoryBarrier();
			heartbeat.Change(TimeSpan.Zero, TimeSpan.FromSeconds(5));

			return response;

		}

	    private void HandleStreamAvailable(Stream stream, HttpContent content, TransportContext context)
	    {
	        streamAvailableTcs.SetResult(stream);
	    }

	    private void Heartbeat(object _)
		{
			SendAsync(new { Type = "Heartbeat" });
		}

		public async Task SendAsync(object data)
		{
		    var content = "data: " + JsonConvert.SerializeObject(data, Formatting.None) + "\r\n\r\n";

		    await WriteContentToStreamAsync(content);
		}

	    private async Task WriteContentToStreamAsync(string content)
	    {
	        if (InitTask != null) // may be the very first time? 
	            await InitTask;

	        var stream = await streamAvailableTcs.Task;

	        var buffer = Encoding.UTF8.GetBytes(content);
            try
            {
                await stream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                log.DebugException("Error when using events transport", ex);
                Connected = false;
                Disconnected();

                CloseTransport(stream);
            }
	    }

	    private void CloseTransport(Stream stream)
	    {
	        try
	        {
	            using (stream)
	            {
	            }
	        }
	        catch (Exception closeEx)
	        {
	            log.DebugException("Could not close transport", closeEx);
	        }
	    }

	    public Task SendManyAsync(IEnumerable<object> data)
		{
			var sb = new StringBuilder();

			foreach (var o in data)
			{
				sb.Append("data: ")
					.Append(JsonConvert.SerializeObject(o))
					.Append("\r\n\r\n");
			}
	        var content = sb.ToString();

	        return WriteContentToStreamAsync(content);
		}

		public async void Disconnect()
		{
			if (heartbeat != null)
				heartbeat.Dispose();
			
			Connected = false;
			Disconnected();
			CloseTransport(await streamAvailableTcs.Task);
		}
	}
}
