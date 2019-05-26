using System;
using System.Collections.ObjectModel;
using Akka.Actor;
using AkkaAllConcur.Configuration;

namespace AkkaAllConcur.Messages
{
    class InitServer
    {
        public ReadOnlyCollection<IActorRef> AllActors { get; private set; }
        public AllConcurConfig AlgortihmConfig { get; private set; }
        public ReadOnlyCollection<Host> Hosts { get; private set; }
        public int ServerNumber { get; private set; }
        public Host ServerHost { get; private set; }
        public InitServer(ReadOnlyCollection<IActorRef> a, AllConcurConfig ac, ReadOnlyCollection<Host> h, int sn, Host sh)
        {
            AllActors = a;
            AlgortihmConfig = ac;
            Hosts = h;
            ServerNumber = sn;
            ServerHost = sh;
        }
    }
}
