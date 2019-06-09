using System;

namespace AkkaAllConcur.Messages
{
    class Rbroadcast : IEquatable<Rbroadcast>, IComparable<Rbroadcast>
    {
        public Guid Id { get; private set; }
        public Abroadcast Message { get; private set; }
        public int Round { get; private set; }

        public Rbroadcast(Abroadcast m, int s)
        {
            Message = m;
            Round = s;
            Id = Guid.NewGuid();
        }

        public bool Equals(Rbroadcast other)
        {
            return Id.Equals(other.Id);
        }
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public int CompareTo(Rbroadcast other)
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
