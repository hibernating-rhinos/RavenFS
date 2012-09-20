using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Infrastructure.Input;
using RavenFS.Studio.Models;
using FileInfo = System.IO.FileInfo;

namespace RavenFS.Studio.Commands
{
	public class UploadCommand : Command
	{
	    private readonly Observable<string> currentFolder;

	    private const double Kb = 1024;
        private const double Mb = Kb * 1024;
        private const double Gb = Mb * 1024;

	    public UploadCommand(Observable<string> currentFolder)
		{
		    this.currentFolder = currentFolder;
		}

	    public override void Execute(object parameter)
		{
		    var files = parameter as IList<FileInfo>;
            
            if (files == null)
            {
                files = GetFilesFromFileDialog();
            }

            if (files != null)
            {
                foreach (var file in files)
                {
                    QueueForUpload(file, currentFolder.Value);
                }
            }
		}

	    private static IList<FileInfo> GetFilesFromFileDialog()
	    {
	        var fileDialog = new OpenFileDialog()
	                             {
	                                 Multiselect = true
	                             };
	        
            var result = TryShowDialog(fileDialog);

	        var files = result.HasValue && result.Value ? fileDialog.Files.ToList() : null;

	        return files;
	    }

        private static bool? TryShowDialog(OpenFileDialog fileDialog)
        {
            try
            {
                return fileDialog.ShowDialog();
            }
            catch (InvalidOperationException)
            {
                AskUser.AlertUser("Upload", "Oops! It looks like you selected too many files. Please try again.");
                return false;
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
	            (_, bytesUploaded) => operation.ProgressChanged(bytesUploaded, fileSize, GetProgressText(bytesUploaded, fileSize)))
                .UpdateOperationWithOutcome(operation)
	            .ContinueOnUIThread(task => stream.Dispose());

	        ApplicationModel.Current.AsyncOperations.RegisterOperation(operation);
	    }

	    private static string GetProgressText(long bytesUploaded, long fileSize)
	    {
	        return string.Format("{0} of {1} uploaded", GetNaturalSize(bytesUploaded), GetNaturalSize(fileSize));
	    }

        private static string GetNaturalSize(long bytes)
        {
            if (bytes > Gb)
            {
                return string.Format(CultureInfo.CurrentCulture, "{0:F2} Gb", bytes/Gb);
            }
            else if (bytes > Mb)
            {
                return string.Format(CultureInfo.CurrentCulture, "{0:F2} Mb", bytes / Mb);
            }
            else if (bytes > Kb)
            {
                return string.Format(CultureInfo.CurrentCulture, "{0:F2} Kb", bytes / Kb);
            }
            else
            {
                return string.Format(CultureInfo.CurrentCulture, "{0} bytes", bytes);
            }
        }
	}
}
