using System;

namespace AkkaAllConcur.Messages
{
    class BroadcastReliably : IEquatable<BroadcastReliably>, IComparable<BroadcastReliably>
    {
        public Guid Id { get; private set; }
        public BroadcastAtomically Message { get; private set; }
        public int Stage { get; private set; }

        public BroadcastReliably(BroadcastAtomically m, int s)
        {
            Message = m;
            Stage = s;
            Id = Guid.NewGuid();
        }

        public bool Equals(BroadcastReliably other)
        {
            return Id.Equals(other.Id);
        }
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public int CompareTo(BroadcastReliably other)
        {
            if (Message.Value == null && other.Message.Value == null)
            {
                return 0;
            }
            else if (Message.Value == null)
            {
                return int.MaxValue;
            }
            else if (other.Message.Value == null)
            {
                return int.MinValue;
            }
            else
            {
                return Id.CompareTo(other.Id);
            }
        }

    }
}
