using System;
using System.Collections.Generic;
using System.Text;
using AkkaAllConcur.Configuration;

namespace AkkaAllConcur.Messages
{
    class MembershipRequest
    {
        public HostInfo NewHost { get; private set; }
        public MembershipRequest(HostInfo nh)
        {
            NewHost = nh;
        }
    }
}
