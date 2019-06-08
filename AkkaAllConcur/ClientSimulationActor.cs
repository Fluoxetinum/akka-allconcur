using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Akka.Actor;

namespace AkkaAllConcur
{
    class ClientSimulationActor : ReceiveActor
    {
        List<IActorRef> hostActors;
        public ClientSimulationActor(List<IActorRef> ha, int interval)
        {
            short[] data = new short[50];
            for (short i = 0; i < data.Length; i++)
            {
                data[i] = i;
            }

            hostActors = ha;

            while (true)
            {
                foreach (var a in hostActors)
                {
                    a.Tell(new Messages.BroadcastAtomically(data));
                }
                //Thread.Sleep(TimeSpan.FromSeconds(1));
                Thread.Sleep(TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond * 1000)); // 10 000 per sec
                //Thread.Sleep(TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond / interval));
            }
        }
    }
}
