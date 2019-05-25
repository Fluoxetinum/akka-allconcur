using System;
using System.Collections.ObjectModel;
using Akka.Actor;
using AkkaAllConcur.Configuration;

namespace AkkaAllConcur.Messages
{
    class InitServer
    {
        public ReadOnlyCollection<IActorRef> AllActors { get; private set; }
        public ProgramConfig Config { get; private set; }
        public InitServer(ReadOnlyCollection<IActorRef> a, ProgramConfig c)
        {
            AllActors = a;
            Config = c;
        }
    }
}
