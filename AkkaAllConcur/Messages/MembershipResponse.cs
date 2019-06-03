using System.Collections.ObjectModel;
using AkkaAllConcur.Configuration;

namespace AkkaAllConcur.Messages
{
    class MembershipResponse
    {
        public ReadOnlyCollection<HostInfo> Hosts { get; private set; }
        public int NextStage { get; private set; }
        public MembershipResponse(ReadOnlyCollection<HostInfo> hosts, int r)
        {
            Hosts = hosts;
            NextStage = r;
        }
    }
}
