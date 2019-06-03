using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using Akka.Actor;

namespace AkkaAllConcur.Messages
{
    class LogTrackGraph
    {
        public ReadOnlyDictionary<IActorRef, ReadOnlyDictionary<IActorRef, ReadOnlyCollection<IActorRef>>> Graphs { get; private set; }

        public LogTrackGraph(ReadOnlyDictionary<IActorRef, ReadOnlyDictionary<IActorRef, ReadOnlyCollection<IActorRef>>> g)
        {
            Graphs = g;
        }
    }
}
