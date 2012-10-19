using System;
using System.IO;
using RavenFS.DocsCompiler.Output;

namespace RavenFS.DocsCompiler.Runner
{
	class Program
	{
		static void Main(string[] args)
		{
			const string basePath = @"C:\workspaces\HIRS\RavenFS\Docs\";

			IDocsOutput output = new HtmlDocsOutput
									{
										OutputPath = Path.Combine(basePath, "html-compiled"),
										PageTemplate = File.ReadAllText(Path.Combine(basePath, @"Tools\html-template.html")),
										RootUrl = "http://RavenFS.net/docs/",
									};

			try
			{
				Compiler.CompileFolder(output, basePath, "Home");
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
		}
	}
}
