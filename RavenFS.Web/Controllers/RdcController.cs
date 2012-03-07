using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using RavenFS.Rdc;
using RavenFS.Rdc.Wrapper;
using RavenFS.Util;
using System.Linq;

namespace RavenFS.Web.Controllers
{
	public class RdcController : RavenController
	{
		public HttpResponseMessage GetFiles(string filename)
		{
			var resultContent = StorageStream.Reading(Storage, filename);

			return GetStream(filename, resultContent);
		}

		public HttpResponseMessage GetSignatures(string filename)
		{
			var localRdcManager = new LocalRdcManager(SignatureRepository, Storage, SigGenerator);
			var resultContent = localRdcManager.GetSignatureContentForReading(filename);
       
			return GetStream(filename, resultContent);
		}

		private HttpResponseMessage GetStream(string filename, Stream resultContent)
		{
			var response = new HttpResponseMessage
			{
				Content = new StreamContent(resultContent)
				{
					Headers =
						{
							ContentDisposition = new ContentDispositionHeaderValue("attachment")
							{
								FileName = filename
							}
						}
				}
			};
			if (Request.Headers.Range != null)
			{
				if(Request.Headers.Range.Ranges.Count != 1)
				{
					throw new InvalidOperationException("Can't handle multiple range values");
				}
				var range = Request.Headers.Range.Ranges.First();
				var from = range.From ?? 0;
				var to = range.To ?? resultContent.Length - 1;


				response.Content.Headers.ContentLength = (to - from + 1);
				
				response.Content.Headers.ContentRange = new ContentRangeHeaderValue(from, to, resultContent.Length);
			}
			else
			{
				response.Content.Headers.ContentLength = resultContent.Length;
			}            
			return response;
		}
	}
}