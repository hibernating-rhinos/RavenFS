using System;
using RavenFS.DocsCompiler.Model;

namespace RavenFS.DocsCompiler.Output
{
	public interface IDocsOutput : IDisposable
	{
		string RootUrl { get; set; }

		string ImagesPath { get; set; }

		void SaveDocItem(Document doc);

		void SaveImage(Folder ofFolder, string fullFilePath);

		void GenerateToc(IDocumentationItem rootItem);
	}
}
