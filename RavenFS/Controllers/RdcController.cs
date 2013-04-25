using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using NLog;
using RavenFS.Client;
using RavenFS.Storage;
using RavenFS.Synchronization;
using RavenFS.Synchronization.Rdc;
using RavenFS.Synchronization.Rdc.Wrapper;

namespace RavenFS.Controllers
{
	public class RdcController : RavenController
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		[AcceptVerbs("GET")]
		public HttpResponseMessage Signatures(string filename)
		{
			filename = Uri.UnescapeDataString(filename);

			log.Debug("Got signatures of a file '{0}' request", filename);

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
			using (var rdcVersionChecker = new RdcVersionChecker())
			{
				var rdcVersion = rdcVersionChecker.GetRdcVersion();
				return new RdcStats
					       {
						       CurrentVersion = rdcVersion.CurrentVersion,
						       MinimumCompatibileAppVersion = rdcVersion.MinimumCompatibleAppVersion
					       };
			}
		}

		[AcceptVerbs("GET")]
		public async Task<HttpResponseMessage> Manifest(string filename)
		{
			filename = Uri.UnescapeDataString(filename);
			FileAndPages fileAndPages = null;
			try
			{
				Storage.Batch(accessor => fileAndPages = accessor.GetFile(filename, 0, 0));
			}
			catch (FileNotFoundException)
			{
				log.Debug("Signature manifest for a file '{0}' was not found", filename);
				return Request.CreateResponse(HttpStatusCode.NotFound);
			}

			long? fileLength = fileAndPages.TotalSize;

			using (var signatureRepository = new StorageSignatureRepository(Storage, filename))
			{
				var rdcManager = new LocalRdcManager(signatureRepository, Storage, SigGenerator);
				var signatureManifest =
					await rdcManager.GetSignatureManifestAsync(new DataInfo
						                                           {
							                                           Name = filename,
							                                           CreatedAt =
								                                           Convert.ToDateTime(fileAndPages.Metadata["Last-Modified"])
								                                                  .ToUniversalTime()
						                                           });
				signatureManifest.FileLength = fileLength ?? 0;

				log.Debug("Signature manifest for a file '{0}' was downloaded. Signatures count was {1}", filename,
				          signatureManifest.Signatures.Count);

				return Request.CreateResponse(HttpStatusCode.OK, signatureManifest);
			}
		}
	}
}