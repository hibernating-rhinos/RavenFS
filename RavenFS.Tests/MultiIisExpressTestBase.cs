using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using RavenFS.Client;
using RavenFS.Tests.Infrastructure;
using RavenFS.Tests.Tools;

namespace RavenFS.Tests
{
    public abstract class MultiIisExpressTestBase : IDisposable
    {
        public static readonly int[] Ports = { 8085, 8086 };

        protected IList<IisExpressDriver> IisExpresses = new List<IisExpressDriver>();

        protected IList<WebClient> WebClients = new List<WebClient>();


        protected MultiIisExpressTestBase()
        {
            foreach (var item in Ports)
            {
                var iisExpress = new IisExpressDriver();
                iisExpress.Start(IisDeploymentUtil.DeployWebProjectToTestDirectory(item), item);
                var webClient = new WebClient
                {
                    BaseAddress = iisExpress.Url
                };
                IisExpresses.Add(iisExpress);
                WebClients.Add(webClient);
            }
        }

        protected RavenFileSystemClient NewClient(int index)
        {
            return new RavenFileSystemClient(IisExpresses[index].Url);
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (IisExpresses != null)
            {
                foreach (var item in IisExpresses)
                {
                    item.Dispose();
                }
            }
            IisExpresses = null;
        }

        #endregion
    }
}
