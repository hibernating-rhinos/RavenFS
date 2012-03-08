using System;
using System.IO;
using System.Net;
using System.Net.Http;
using RavenFS.Client;
using RavenFS.Rdc;
using RavenFS.Rdc.Wrapper;
using RavenFS.Util;

namespace RavenFS.Web.Controllers
{
	public class RdcController : RavenController
	{
		public HttpResponseMessage Files(string filename)
		{
			filename = Uri.UnescapeDataString(filename);
			var resultContent = StorageStream.Reading(Storage, filename);

			return StreamResult(filename, resultContent);
		}

		public HttpResponseMessage Signatures(string filename)
		{
			filename = Uri.UnescapeDataString(filename);
			var localRdcManager = new LocalRdcManager(SignatureRepository, Storage, SigGenerator);
			var resultContent = localRdcManager.GetSignatureContentForReading(filename);
       
			return StreamResult(filename, resultContent);
		}

		public RdcStats Stats()
		{
			return new RdcStats
			{
				Version = (int) Msrdc.Version
			};
		}

		public HttpResponseMessage<SignatureManifest> Manifest(string filename)
		{
			filename = Uri.UnescapeDataString(filename);
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

	}
}