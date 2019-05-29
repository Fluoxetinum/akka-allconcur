using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Akka.Actor;
using AkkaAllConcur.Configuration;

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

            //StringBuilder str = new StringBuilder();
            //foreach (var a in actors)
            //{
            //    str.Append(a + ",");
            //}

            //Console.WriteLine($"{str}");

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
            int n = actors.Count;
            // d >= 3, n >= 2d Quasiminimal
            int some = (int)Math.Floor(Math.Sqrt(n));
            int d = n / some;
            if (d < 3)
            {
                Console.WriteLine("To few 'servers' in the system for Gs graph (mimimum - 9).");
                throw new NotImplementedException();
            }

            int m = n / d;
            int t = n % d;

            Dictionary<int, List<int>> Gb = new Dictionary<int, List<int>>();

            for (int i = 0; i < m; i++)
            {
                Gb[i] = new List<int>();
                for (int j = 0; j < m; j++)
                {
                    for (int a = 0; a < d; a++)
                    {
                        if (j == (i * d + a) % m)
                        {
                            Gb[i].Add(j);
                        }
                    }
                }
            }

            // Gb*

            int minSelfLoops = (int)Math.Floor(d / (double)m);
            List<int> maxSelfLoopsVertices = new List<int>();

            for (int i = 0; i < m; i++)
            {
                int selfLoops = Gb[i].Where(v => v == i).Count();

                if (selfLoops > minSelfLoops)
                {
                    maxSelfLoopsVertices.Add(i);
                }
            }

            for (int loop = 0; loop < minSelfLoops; loop++)
            {
                for (int i = 0; i < m; i++)
                {
                    Gb[i].Add((i + 1) % m);
                }
            }

            for (int s = 0; s < maxSelfLoopsVertices.Count; s++)
            {
                int i = maxSelfLoopsVertices[s];
                int j = maxSelfLoopsVertices[(s + 1) % maxSelfLoopsVertices.Count];
                Gb[i].Add(j);
            }

            foreach (var pair in Gb)
            {
                var list = pair.Value;
                var i = pair.Key;
                list.RemoveAll(v => v == i);
            }

            // L(Gb*)

            Dictionary<int, KeyValuePair<int, int>> LgbVertexes = new Dictionary<int, KeyValuePair<int, int>>();
            Dictionary<int, List<int>> Lgb = new Dictionary<int, List<int>>();

            int counterI = 0;
            int counterJ = 0;
            foreach (var u in Gb.Keys)
            {
                counterJ = 0;
                foreach (var v in Gb[u])
                {
                    int i = counterI + counterJ;
                    Lgb[i] = new List<int>();
                    LgbVertexes[i] = new KeyValuePair<int, int>(u, v);
                    counterJ++;
                }

                counterI += Gb[u].Count;
            }

            int lastCounter = counterI;

            foreach (var uv1 in LgbVertexes)
            {
                int i = uv1.Key;
                int u = LgbVertexes[i].Key;
                int v = LgbVertexes[i].Value;

                foreach (var uv2 in LgbVertexes)
                {
                    int j = uv2.Key;
                    int w = LgbVertexes[j].Key;
                    int z = LgbVertexes[j].Value;

                    if (v == w)
                    {
                        Lgb[i].Add(j);
                    }

                }

            }

            if (t == 0)
            {
                return CreateActorsGraphFromGs(actors, Lgb);
            }

            // Arbitrary value from Gb*
            Random rand = new Random();
            int randomVertex = rand.Next(m);

            List<int> X_vertexes = new List<int>();
            List<int> Y_vertexes = new List<int>();

            foreach (var pair in LgbVertexes)
            {
                int vertex = pair.Key;
                int u = pair.Value.Key;
                int v = pair.Value.Value;

                if (u == randomVertex)
                {
                    X_vertexes.Add(vertex);
                }
                if (v == randomVertex)
                {
                    Y_vertexes.Add(vertex);
                }
            }

            int jLast = lastCounter;
            for (int i = 0; i < t; i++)
            {
                Lgb[jLast] = new List<int>();

                for (int k = lastCounter; k < lastCounter + t; k++)
                {
                    if (k != jLast)
                    {
                        Lgb[jLast].Add(k);
                    }
                }
                jLast++;
            }

            for (int i = 0; i < t; i++)
            {
                for (int j = i; j < i + d - t; j++)
                {
                    Lgb[X_vertexes[j]].Add(lastCounter + i);
                    Lgb[lastCounter + i].Add(Y_vertexes[j]);
                }
            }

            for (int i = 0; i < t; i++)
            {
                for (int p = 0; p < d - t + 1; p++)
                {
                    int q = (i + p) % (d - t + 1);
                    int x = X_vertexes[i + p];
                    int y = Y_vertexes[i + q];
                    Lgb[x].Remove(y);
                }
            }

            return CreateActorsGraphFromGs(actors, Lgb);
        }

        private static Dictionary<IActorRef, List<IActorRef>> CreateActorsGraphFromGs(List<IActorRef> actors, Dictionary<int, List<int>> gs)
        {

            var successors = new Dictionary<IActorRef, List<IActorRef>>();

            foreach (var key in gs.Keys)
            {
                IActorRef i = actors[key];
                successors[i] = new List<IActorRef>();
                foreach (var val in gs[key])
                {
                    successors[i].Add(actors[val]);
                }
            }
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
