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
	    private readonly Observable<string> currentFolder;

	    public UploadCommand(Observable<string> currentFolder)
		{
		    this.currentFolder = currentFolder;
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
                    QueueForUpload(file, currentFolder.Value);
                }
            }
		}

	    private static void QueueForUpload(FileInfo file, string folder)
	    {
	        var operation = new AsyncOperationModel()
	                            {
	                                Description = "Uploading " + file.Name + " to " + folder,
	                            };

            var stream = file.OpenRead();
            var fileSize = stream.Length;

	    	var fileName = file.Name;

			if (folder != "/")
				fileName = folder + "/" + file.Name;

	        ApplicationModel.Current.Client.UploadAsync(
	            fileName,
	            new NameValueCollection(),
	            stream,
	            (_, bytesUploaded) => operation.ProgressChanged(bytesUploaded, fileSize))
                .UpdateOperationWithOutcome(operation)
	            .ContinueOnUIThread(task => stream.Dispose());

	        ApplicationModel.Current.AsyncOperations.RegisterOperation(operation);
	    }
	}
}
