using System;
using Akka.Actor;

namespace AkkaAllConcur
{
    public class VertexPair : IEquatable<VertexPair>
    {
        public IActorRef V1 { get; private set; }
        public IActorRef V2 { get; private set; }
        public VertexPair(IActorRef v1, IActorRef v2)
        {
            V1 = v1;
            V2 = v2;
        }
        public bool Equals(VertexPair other)
        {
            if (V1.Equals(other.V1) && V2.Equals(other.V2))
            {
                return true;
            }
            return false;
        }
        public override int GetHashCode()
        {
            return V1.GetHashCode() + V2.GetHashCode();
        }
    }
}
