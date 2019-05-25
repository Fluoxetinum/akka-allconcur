using System;
using Akka.Actor;

namespace AkkaAllConcur.Messages
{
    class IndirectFailureNotification
    {
        public IActorRef ActorRef { get; private set; }
        public IndirectFailureNotification(IActorRef a)
        {
            ActorRef = a;
        }
    }
}
