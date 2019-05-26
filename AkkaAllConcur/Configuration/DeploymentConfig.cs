using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Akka.Actor;

namespace AkkaAllConcur.Configuration
{
    public class DeploymentConfig
    {
        public Host ThisHost { get; private set; }
        public int ThisServerNumber { get; private set; }
        public HashSet<Host> Hosts { get; private set; }
        public DeploymentConfig(HashSet<Host> h, Host th, int tsn)
        {
            Hosts = h;
            ThisHost = th;
            ThisServerNumber = tsn;
        }
    }
}
