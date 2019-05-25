using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor;

namespace AkkaAllConcur
{
    static class GraphCreator
    {
        public static Dictionary<IActorRef, List<IActorRef>> ComputeAllReliableSuccessors(AllConcurConfig config, List<IActorRef> actors)
        {      
            return config.OverlayGraph == AllConcurConfig.OverlayGraphType.BINOMIAL ? 
                ComputeUsingBinomial(actors) : ComputeUsingGs(actors);
        }

        private static Dictionary<IActorRef, List<IActorRef>> ComputeUsingBinomial(List<IActorRef> actors)
        {
            var successors = new Dictionary<IActorRef, List<IActorRef>>();
            int N = actors.Count;
            int steps = (int)Math.Floor(Math.Log(N, 2));

            for (int i = 0; i < N; i++)
            {
                List<IActorRef> pSuccessors = new List<IActorRef>();

                for (int j = 0; j < N; j++)
                {
                    if (j == i) continue;
                    for (int l = 0; l <= steps; l++)
                    {
                        int p = (int)Math.Pow(2, l);
                        int cw = (j + p) % N;
                        int ccw = (N + j - p) % N;

                        if (i == cw || i == ccw)
                        {
                            pSuccessors.Add(actors[j]);
                            break;
                        }
                    }
                }
                successors.Add(actors[i], pSuccessors);
            }
            return successors;
        }

        private static Dictionary<IActorRef, List<IActorRef>> ComputeUsingGs(List<IActorRef> actors)
        {
            var successors = new Dictionary<IActorRef, List<IActorRef>>();

            return successors;
        }

        public static Dictionary<IActorRef, IActorRef> ComputeAllUnreliableSucessors(List<IActorRef> actors)
        {
            var successors = new Dictionary<IActorRef, IActorRef>();

            for (int i = 0; i < actors.Count; i++)
            {
                successors.Add(actors[i], actors[(i + 1) % actors.Count]);
            }

            return successors;
        }

    }
}
