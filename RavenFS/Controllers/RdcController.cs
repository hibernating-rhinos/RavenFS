using System;
using System.IO;
using System.Net;
using System.Net.Http;
using RavenFS.Client;
using RavenFS.Rdc;
using RavenFS.Rdc.Wrapper;
using RavenFS.Rdc.Wrapper.Unmanaged;

namespace RavenFS.Controllers
{
	public class RdcController : RavenController
	{
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
		    long? fileLength = null;
			try
			{
                Storage.Batch(accessor => fileLength = accessor.GetFile(filename, 0, 0).TotalSize);
			}
			catch (FileNotFoundException)
			{
				return new HttpResponseMessage<SignatureManifest>(HttpStatusCode.NotFound);
			}

			var rdcManager = new LocalRdcManager(SignatureRepository, Storage, SigGenerator);
			var signatureManifest = rdcManager.GetSignatureManifest(new DataInfo {Name = filename});
		    signatureManifest.FileLength = fileLength ?? 0;
			return new HttpResponseMessage<SignatureManifest>(signatureManifest);
		}

	}
}