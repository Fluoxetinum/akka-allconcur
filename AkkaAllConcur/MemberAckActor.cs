using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor;
using AkkaAllConcur.Configuration;

namespace AkkaAllConcur
{
    class MemberAckActor : ReceiveActor 
    {
        bool init = false;

        public MemberAckActor(ActorSystem system, List<IActorRef> thisHostActors, List<HostInfo> hosts,
            AllConcurConfig algConfig, HostInfo currentHost, int interval)
        {
            Receive<Messages.MembershipResponse>((m) => {
                if (!init)
                {
                    foreach (var rh in m.Hosts)
                    {
                        hosts.Add(rh);
                    }

                    List<IActorRef> allActors = Deployment.ReachActors(hosts, system);

                    int stage = m.NextStage;

                    Deployment.InitActors(system, thisHostActors, allActors, hosts, algConfig, currentHost, stage, interval);

                    init = true;
                }
            });
        }
    }
}
