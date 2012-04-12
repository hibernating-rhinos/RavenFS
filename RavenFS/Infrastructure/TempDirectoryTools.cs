using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace RavenFS.Infrastructure
{
    public class TempDirectoryTools
    {
        public static string Create()
        {
            string tempDirectory;
            do
            {
                tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            } while (Directory.Exists(tempDirectory)); 
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }
    }
}