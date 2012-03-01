using System.Windows.Controls;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Commands
{
	public class UploadCommand : Command
	{
		public UploadCommand()
		{
		}

		public override void Execute(object parameter)
		{
			var fileDialog = new OpenFileDialog();
			var result = fileDialog.ShowDialog();
			if (result != true)
				return;

			var stream = fileDialog.File.OpenRead();
			var fileSize = stream.Length;

		    var filename = fileDialog.File.Name;
		    var operation = new AsyncOperationModel()
		                        {
		                            Name = "Uploading " + filename,
		                        };

		    ApplicationModel.Current.Client.UploadAsync(
		        filename,
		        new NameValueCollection(),
		        stream,
		        (file, bytesUploaded) => operation.ProgressChanged(bytesUploaded,fileSize))
		        .ContinueWith(task =>
		                          {
		                              stream.Dispose();

                                      if (task.IsFaulted)
                                      {
                                          operation.Faulted(task.Exception.ExtractSingleInnerException());
                                      }
                                      else
                                      {
                                          operation.Completed();
                                      }
		                          });

            ApplicationModel.Current.AsyncOperations.RegisterOperation(operation);
		}
	}
}
