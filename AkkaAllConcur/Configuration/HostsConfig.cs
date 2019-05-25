using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Akka.Actor;

namespace AkkaAllConcur.Configuration
{
    class HostsConfig
    {
        public List<Host> Hosts { get; private set; }

        public HostsConfig(List<Host> h)
        {
            Hosts = h;
        }
    }
}
