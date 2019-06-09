using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Akka.Actor;
using AkkaAllConcur.Configuration;

namespace AkkaAllConcur
{
    class ClientSimulationActor : ReceiveActor
    {
        List<IActorRef> hostActors;
        public ClientSimulationActor(List<IActorRef> ha, HostInfo host, int interval)
        {
            short[] data = new short[50];
            for (short i = 0; i < data.Length; i++)
            {
                data[i] = i;
            }

            hostActors = ha;

            int num = 0;

            while (true)
            {
                //foreach (var a in hostActors)
                //{
                //    a.Tell(new Messages.BroadcastAtomically(data));
                //}

                foreach (var a in hostActors)
                {
                    a.Tell(new Messages.Abroadcast($"Message№{num} from server {host.HostName}:{host.Port}...{a.ToShortString()}"));
                }
                num++;

                Thread.Sleep(TimeSpan.FromMilliseconds(interval));
                //Thread.Sleep(TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond * 1000)); // 10 000 per sec
                //Thread.Sleep(TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond / interval));
            }
        }
    }
}
