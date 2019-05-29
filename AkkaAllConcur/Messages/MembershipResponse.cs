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
        public ReadOnlyCollection<Host> Hosts { get; private set; }
        public int NextStage { get; private set; }
        public MembershipResponse(ReadOnlyCollection<Host> hosts, int r)
        {
            Hosts = hosts;
            NextStage = r;
        }
    }
}
