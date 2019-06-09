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
        public ReadOnlyCollection<HostInfo> Hosts { get; private set; }
        public int ServerNumber { get; private set; }
        public HostInfo ServerHost { get; private set; }
        public int Round { get; private set; }
        public InitServer(ReadOnlyCollection<IActorRef> a, AllConcurConfig ac, ReadOnlyCollection<HostInfo> h, int sn, HostInfo sh, int r)
        {
            AllActors = a;
            AlgortihmConfig = ac;
            Hosts = h;
            ServerNumber = sn;
            ServerHost = sh;
            Round = r;
        }
    }
}
