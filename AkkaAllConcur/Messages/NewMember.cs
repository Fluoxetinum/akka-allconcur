using System;
using System.Collections.Generic;
using System.Text;
using AkkaAllConcur.Configuration;

namespace AkkaAllConcur.Messages
{
    class NewMemberNotification
    {
        public HostInfo NewMember { get; private set; }
        public NewMemberNotification(HostInfo nm)
        {
            NewMember = nm;
        }
    }
}
