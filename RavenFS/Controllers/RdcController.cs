using System;
using System.IO;
using System.Net;
using System.Net.Http;
using RavenFS.Client;

namespace RavenFS.Controllers
{
	using System.Web.Http;
	using NLog;
	using Storage;
	using Synchronization;
	using Synchronization.Rdc;
	using Synchronization.Rdc.Wrapper;
	using Synchronization.Rdc.Wrapper.Unmanaged;

	public class RdcController : RavenController
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		[AcceptVerbs("GET")]
		public HttpResponseMessage Signatures(string filename)
		{
			filename = Uri.UnescapeDataString(filename);

			log.Debug("Getting signatures of a file '{0}'", filename);

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
				log.Debug("Signature manifest for a file '{0}' was not found", filename);
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

				log.Debug("Signature manifest for a file '{0}' was downloaded. Signatures count was {1}", filename, signatureManifest.Signatures.Count);

				return Request.CreateResponse(HttpStatusCode.OK, signatureManifest);
			}
		}

	}
}