using System.Collections.ObjectModel;
using Akka.Actor;

namespace AkkaAllConcur.Messages
{
    class LogGraph
    {
        public ReadOnlyDictionary<IActorRef, ReadOnlyCollection<IActorRef>> Graph { get; private set; }
        public LogGraph(ReadOnlyDictionary<IActorRef, ReadOnlyCollection<IActorRef>> g)
        {
            Graph = g;
        }
    }
}
