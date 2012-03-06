using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Threading;
using RavenFS.Client;
using RavenFS.Rdc.Wrapper;

namespace Tryouts
{
	class Program
	{
		[STAThread]
		static void Main(string[] args)
		{
			IRdcLibrary x = (IRdcLibrary)new RdcLibrary();
		}
		
	}
}
