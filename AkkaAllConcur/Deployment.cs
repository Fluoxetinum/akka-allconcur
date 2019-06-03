﻿using System;
using System.Collections.Generic;
using Akka.Actor;
using AkkaAllConcur.Configuration;

namespace AkkaAllConcur
{
    public static class Deployment
    {
        public static List<IActorRef> ReachActors(ICollection<HostInfo> allHosts, ActorSystem system)
        {
            List<IActorRef> actors = new List<IActorRef>();

            foreach (var h in allHosts)
            {
                actors = Reach(system, actors, h.ActorsNumber, h.HostName, h.Port);
            }

            return actors;
        }

        public static List<IActorRef> ReachActors(HostInfo newHost, ActorSystem system, List<IActorRef> actors)
        {
            return Reach(system, actors, newHost.ActorsNumber, newHost.HostName, newHost.Port);
        }

        private static List<IActorRef> Reach(ActorSystem system, List<IActorRef> actors, int count, string hostName, string port)
        {
            for (int i = 0; i < count; i++)
            {
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
