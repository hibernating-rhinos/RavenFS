using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RavenFS.Rdc.Utils.IO
{
    public static class TestUtils
    {
        public static string GetMD5Hash(this Stream stream)
        {            
            MD5 md5 = new MD5CryptoServiceProvider();
            var retVal = md5.ComputeHash(stream);

            StringBuilder sb = new StringBuilder();
            for (var i = 0; i < retVal.Length; i++)
            {
                sb.Append(retVal[i].ToString("x2"));
            }
            return sb.ToString();
        }

        public static string GetMD5HashFromFile(string path)
        {
            using (var file = File.OpenRead(path))
            {
                return GetMD5Hash(file);
            }
        }
    }
}
