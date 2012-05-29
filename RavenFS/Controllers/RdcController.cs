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
	using System.Web.Http;
	using Storage;

	public class RdcController : RavenController
	{
		[AcceptVerbs("GET")]
		public HttpResponseMessage Signatures(string filename)
		{
			filename = Uri.UnescapeDataString(filename);

			using (var signatureRepository = new StorageSignatureRepository(Storage, filename))
			{
				var localRdcManager = new LocalRdcManager(signatureRepository, Storage, SigGenerator);
				var resultContent = localRdcManager.GetSignatureContentForReading(filename);
				return StreamResult(filename, resultContent);
			}
		}

		[AcceptVerbs("GET")]
		public RdcStats Stats()
		{
			return new RdcStats
			{
				Version = (int)Msrdc.Version
			};
		}

		[AcceptVerbs("GET")]
		public HttpResponseMessage Manifest(string filename)
		{
			filename = Uri.UnescapeDataString(filename);
			FileAndPages fileAndPages = null;
			long? fileLength = null;
			try
			{
				Storage.Batch(accessor => fileAndPages = accessor.GetFile(filename, 0, 0));
			}
			catch (FileNotFoundException)
			{
				return Request.CreateResponse(HttpStatusCode.NotFound);
			}

			fileLength = fileAndPages.TotalSize;

			using (var signatureRepository = new StorageSignatureRepository(Storage, filename))
			{
				var rdcManager = new LocalRdcManager(signatureRepository, Storage, SigGenerator);
				var signatureManifest =
					rdcManager.GetSignatureManifest(new DataInfo
					                                	{
					                                		Name = filename,
					                                		CreatedAt =
					                                			Convert.ToDateTime(fileAndPages.Metadata["Last-Modified"]).ToUniversalTime()
					                                	});
				signatureManifest.FileLength = fileLength ?? 0;
				return Request.CreateResponse(HttpStatusCode.OK, signatureManifest);
			}
		}

	}
}