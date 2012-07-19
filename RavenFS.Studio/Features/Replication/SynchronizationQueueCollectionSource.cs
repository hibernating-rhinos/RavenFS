﻿using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Features.Replication
{
    public class SynchronizationQueueCollectionSource : CompositeVirtualCollectionSource<SynchronizationDetails>
    {
        public SynchronizationQueueCollectionSource() : base(new ActiveSynchronizationTasksCollectionSource(), new PendingSynchronizationTasksCollectionSource())
        {
        }
    }
}
