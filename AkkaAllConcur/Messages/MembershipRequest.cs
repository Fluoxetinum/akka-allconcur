using System;
using System.Collections.Generic;
using System.Text;
using AkkaAllConcur.Configuration;

namespace AkkaAllConcur.Messages
{
    class MembershipRequest : IEquatable<MembershipRequest>, IComparable<MembershipRequest>
    {
        public int HostActorsNumber { get; private set; }
        public MembershipRequest(int n)
        {
            HostActorsNumber = n;
        }

        public bool Equals(MembershipRequest other)
        {
            throw new NotImplementedException();
        }

        public int CompareTo(MembershipRequest other)
        {
            throw new NotImplementedException();
        }
    }
}
