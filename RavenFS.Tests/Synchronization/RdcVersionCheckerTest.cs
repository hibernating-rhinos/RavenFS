﻿namespace RavenFS.Tests.Synchronization
{
    using RavenFS.Synchronization.Rdc.Wrapper;

    using Xunit;

    public class RdcVersionCheckerTest
    {
        [MtaFact]
        public void Ctor_and_dispose()
        {
            using (var tested = new RdcVersionChecker())
            {
                Assert.NotNull(tested);
            }
        } 

        [MtaFact]
        public void Should_have_nontrivial_version()
        {
            using (var tested = new RdcVersionChecker())
            {
                var result = tested.GetRdcVersion();
                Assert.True(result.CurrentVersion > 0);
                Assert.True(result.MinimumCompatibleAppVersion > 0);
            }
        }
    }
}