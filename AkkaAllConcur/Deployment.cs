using System;
using System.Collections.Generic;
using Akka.Actor;
using AkkaAllConcur.Configuration;

namespace AkkaAllConcur
{
    public static class Deployment
    {
        public static List<IActorRef> ReachActors(ICollection<Host> allHosts, ActorSystem system)
        {
            List<IActorRef> actors = new List<IActorRef>();

            foreach (var h in allHosts)
            {
                int count = h.ActorsNumber;

                for (int i = 0; i < count; i++)
                {
                    string hostName = h.HostName;
                    string port = h.Port;

                    // TODO: Program.SystemName add to config
                    ActorSelection a = system.ActorSelection($"akka.tcp://{Program.SystemName}@{hostName}:{port}/user/svr{i}");
                    var t = a.ResolveOne(TimeSpan.FromMinutes(5));
                    t.Wait();
                    actors.Add(t.Result);
                }

            }

            return actors;
        }

        public static List<IActorRef> ReachActors(Host newHost, ActorSystem system, List<IActorRef> actors)
        {

            int count = newHost.ActorsNumber;

            for (int i = 0; i < count; i++)
            {
                string hostName = newHost.HostName;
                string port = newHost.Port;

                // TODO: Program.SystemName add to 
                string path = $"akka.tcp://{Program.SystemName}@{hostName}:{port}/user/svr{i}";
                ActorSelection a = system.ActorSelection(path);
                var t = a.ResolveOne(TimeSpan.FromMinutes(5));
                t.Wait();
                actors.Add(t.Result);
            }

            return actors;
        }
    }
}
