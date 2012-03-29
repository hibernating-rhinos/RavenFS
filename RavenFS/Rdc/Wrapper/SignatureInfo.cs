// //-----------------------------------------------------------------------
// // <copyright company="Hibernating Rhinos LTD">
// //     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// // </copyright>
// //-----------------------------------------------------------------------
using System;

namespace RavenFS.Rdc.Wrapper
{
	public class SignatureInfo
	{
		public SignatureInfo()
		{
			Name = Guid.NewGuid().ToString();
		}

		public SignatureInfo(string name)
		{
			Name = name;
		}

        public SignatureInfo(int level, string fileName)
        {
            Name = fileName + "." + level + ".sig";
        }

		public string Name { get; set; }

		public long Length { get; set; }
	}
}