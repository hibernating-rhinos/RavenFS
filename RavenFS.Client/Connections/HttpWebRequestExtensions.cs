using System;
using System.Net;
using System.Threading.Tasks;

namespace RavenFS.Client.Connections
{
    public static class HttpWebRequestExtensions
    {
	    public static async Task<IObservable<string>> ServerPullAsync(this HttpWebRequest webRequest, int retries = 0)
	    {
#if SILVERLIGHT
            webRequest.AllowReadStreamBuffering = false;
			webRequest.AllowWriteStreamBuffering = false;
#endif
		    var task = await webRequest.GetResponseAsync();

		    var stream = task.GetResponseStreamWithHttpDecompression();
		    var observableLineStream = new ObservableLineStream(stream, () =>
			    {
				    webRequest.Abort();
				    task.Close();
			    });
		    observableLineStream.Start();
		    return (IObservable<string>) observableLineStream;
	    }
    }
}