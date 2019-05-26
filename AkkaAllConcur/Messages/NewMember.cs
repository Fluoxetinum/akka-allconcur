using System;
using System.Collections.Generic;
using System.Text;
using AkkaAllConcur.Configuration;

namespace AkkaAllConcur.Messages
{
    class NewMemberNotification
    {
        public Host NewMember { get; private set; }
        public NewMemberNotification(Host nm)
        {
            NewMember = nm;
        }
    }
}
