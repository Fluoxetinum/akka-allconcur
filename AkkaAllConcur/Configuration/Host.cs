using System;

namespace AkkaAllConcur.Configuration
{
    public struct HostInfo : IEquatable<HostInfo>, IComparable<HostInfo>
    {
        public string HostName { get; set; }
        public string Port { get; set; }
        public int ActorsNumber { get; set; }

        public int CompareTo(HostInfo other)
        {
            if (other.Equals(this)) return 0;
            return -1;
        }

        public bool Equals(HostInfo other)
        {
            if (other.HostName.Equals(HostName) &&
                other.Port.Equals(Port))
            {
                return true;
            }
            return false;
        }
    }
}
