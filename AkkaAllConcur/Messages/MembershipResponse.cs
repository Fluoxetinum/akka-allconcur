using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using Akka.Actor;
using AkkaAllConcur.Configuration;

namespace AkkaAllConcur.Messages
{
    class MembershipResponse
    {
        public int LastIndex { get; private set; }
        public ReadOnlyCollection<Host> Hosts { get; private set; }
        public MembershipResponse(ReadOnlyCollection<Host> hosts, int i)
        {
            Hosts = hosts;
            LastIndex = i;
        }
    }
}
