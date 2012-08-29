using System;
using System.Net;
using System.Threading.Tasks;

namespace RavenFS.Client.Connections
{
    public static class HttpWebRequestExtensions
    {
        public static Task<IObservable<string>> ServerPullAsync(this HttpWebRequest webRequest, int retries = 0)
        {
#if SILVERLIGHT
            webRequest.AllowReadStreamBuffering = false;
			webRequest.AllowWriteStreamBuffering = false;
#endif
            return webRequest.GetResponseAsync()
                .ContinueWith(task =>
                    {
                        var stream = task.Result.GetResponseStreamWithHttpDecompression();
                        var observableLineStream = new ObservableLineStream(stream, () =>
                            {
                                webRequest.Abort();
                                task.Result.Close();
                            });
                        observableLineStream.Start();
                        return (IObservable<string>) observableLineStream;
                    });
        }
    }
}
