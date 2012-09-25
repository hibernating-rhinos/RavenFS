namespace RavenFS.Synchronization.Rdc.Wrapper
{
    using System;
    using System.Runtime.ConstrainedExecution;
    using System.Runtime.InteropServices;
    using System.Threading;

    using NLog;

    using RavenFS.Synchronization.Rdc.Wrapper.Unmanaged;

    public class RdcVersionChecker : CriticalFinalizerObject, IDisposable
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        private readonly ReaderWriterLockSlim _disposerLock = new ReaderWriterLockSlim();
        private bool _disposed;

        private readonly IRdcLibrary _rdcLibrary;

        public RdcVersionChecker()
        {
            try
            {
                _rdcLibrary = (IRdcLibrary)new RdcLibrary();
            }
            catch (InvalidCastException e)
            {
                throw new InvalidOperationException("This code must run in an MTA thread", e);
            }
            catch (COMException comException)
            {
                log.ErrorException("Remote Differential Compression feature is not installed", comException);
                throw new NotSupportedException("Remote Differential Compression feature is not installed", comException);
            }
        }

        public RdcVersion GetRdcVersion()
        {
            uint currentVersion, minimumCompatibileAppVersion;
            var hr = _rdcLibrary.GetRDCVersion(out currentVersion, out minimumCompatibileAppVersion);
            if (hr != 0)
            {
                throw new RdcException("Failed to get the rdc version", hr);
            }

            return new RdcVersion { CurrentVersion = currentVersion, MinimumCompatibleAppVersion = minimumCompatibileAppVersion };
        }

        public void Dispose()
        {
            _disposerLock.EnterWriteLock();
            try
            {
                if (_disposed)
                    return;
                GC.SuppressFinalize(this);
                DisposeInternal();
            }
            finally
            {
                _disposed = true;
                _disposerLock.ExitWriteLock();
            }
        }

        private void DisposeInternal()
        {
            if (_rdcLibrary != null)
            {
                Marshal.ReleaseComObject(_rdcLibrary);
            }
        }
    }
}