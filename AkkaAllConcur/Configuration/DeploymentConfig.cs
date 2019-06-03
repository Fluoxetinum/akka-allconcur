using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Akka.Actor;

namespace AkkaAllConcur.Configuration
{
    public class DeploymentConfig
    {
        public HostInfo ThisHost { get; private set; }
        public int ThisServerNumber { get; private set; }
        public HashSet<HostInfo> Hosts { get; private set; }
        public DeploymentConfig(HashSet<HostInfo> h, HostInfo th, int tsn)
        {
            Hosts = h;
            ThisHost = th;
            ThisServerNumber = tsn;
        }
    }
}
