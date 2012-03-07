using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using RavenFS.Client;
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

		public RdcStats GetStats()
		{
			return new RdcStats
			{
				Version = (int) Msrdc.Version
			};
		}

		public HttpResponseMessage<SignatureManifest> GetManifest(string filename)
		{
			try
			{
				Storage.Batch(accessor => accessor.GetFile(filename, 0, 0));
			}
			catch (FileNotFoundException)
			{
				return new HttpResponseMessage<SignatureManifest>(HttpStatusCode.NotFound);
			}

			var rdcManager = new LocalRdcManager(SignatureRepository, Storage, SigGenerator);
			var signatureManifest = rdcManager.GetSignatureManifest(new DataInfo {Name = filename});
			return new HttpResponseMessage<SignatureManifest>(signatureManifest);
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