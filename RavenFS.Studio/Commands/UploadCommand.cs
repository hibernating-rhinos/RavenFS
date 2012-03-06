using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;
using FileInfo = System.IO.FileInfo;

namespace RavenFS.Studio.Commands
{
	public class UploadCommand : Command
	{
		public UploadCommand()
		{
		}

		public override void Execute(object parameter)
		{
		    var files = parameter as IList<FileInfo>;
            
            if (files == null)
            {
                var fileDialog = new OpenFileDialog()
                                     {
                                         Multiselect = true
                                     };
                var result = fileDialog.ShowDialog();
                if (result.HasValue && result.Value)
                {
                    files = fileDialog.Files.ToList();
                }
            }

            if (files != null)
            {
                foreach (var file in files)
                {
                    UploadFile(file);
                }
            }
		}

	    private static void UploadFile(FileInfo file)
	    {
	        var stream = file.OpenRead();
	        var fileSize = stream.Length;

	        var filename = file.Name;
	        var operation = new AsyncOperationModel()
	                            {
	                                Name = "Uploading " + filename,
	                            };

	        ApplicationModel.Current.Client.UploadAsync(
	            filename,
	            new NameValueCollection(),
	            stream,
	            (fileName, bytesUploaded) => operation.ProgressChanged(bytesUploaded, fileSize))
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
