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

            StringBuilder str = new StringBuilder();
            foreach (var a in actors)
            {
                str.Append(a + ",");
            }

            Console.WriteLine($"{str}");

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
            // just trying to learn...yet

            var successors = new Dictionary<IActorRef, List<IActorRef>>();

            /*

            // d >= 3, n >= 2d
            //int n = 9;
            //int d = 3;

            //int m = n / d;
            //int t = n % d;

            int d = 5;
            int m = 3;

            Dictionary<int, List<int>> graph = new Dictionary<int, List<int>>();

            // ========================== GB

            for (int i = 0; i < m; i++)
            {
                graph[i] = new List<int>();

                for (int j = 0; j < 3; j++)
                {
                    for (int k = 0; k < d; k++)
                    {
                        if (j == (i * d + k) % m)
                        {
                            graph[i].Add(j);
                        }
                    }
                }
            }

            // ======================== GB*

            int minSelfLoops = (int)Math.Floor(d / (double)m);

            List<int> maxSelfLoopsVertices = new List<int>();

            for (int loop = 0; loop < minSelfLoops; loop++)
            {
                for (int i = 0; i < m; i++)
                {
                    int selfLoops = graph[i].Where(v => v == i).Count();

                    if (selfLoops > minSelfLoops)
                    {
                        maxSelfLoopsVertices.Add(i);
                    }

                    graph[i].Add((i + 1) % m);
                }
            }

            for (int s = 0; s < maxSelfLoopsVertices.Count; s++)
            {
                int i = maxSelfLoopsVertices[s];
                int j = maxSelfLoopsVertices[(s + 1) % maxSelfLoopsVertices.Count];
                graph[i].Add(j);
            }

            foreach (var pair in graph)
            {
                var list = pair.Value;
                var i = pair.Key;

                list.RemoveAll(v => v == i);
            }

            // ======================== L(GB*)

            graph.Clear();

            for (int i = 0; i < m; i++)
            {
                graph[i] = new List<int>();
                graph[i].Add((i + 1) % m);
                graph[i].Add((m + i - 1) % m);
            }

            var graphL = new Dictionary<int, List<int>>();

            foreach(var v in graph.Keys)
            {
                foreach(var e in graph[v])
                {
                    graphL[v * 10 + e] = new List<int>();
                }
            }

            foreach(var v1 in graphL.Keys)
            {
                int u = v1 / 10;
                int v = v1 % 10;
                foreach(var v2 in graphL.Keys)
                {
                    int w = v2 / 10;
                    int z = v2 % 10;

                    if (v == w)
                    {
                        graphL[v1].Add(v2);
                    }

                }
            }

            // ======================== GS

            // if t == 0, GS == LGB

            int d2 = 6;
            int t2 = 4;

            Random rand = new Random();
            int randomVertex = rand.Next(m);

            List<int> X_vertexes = new List<int>();
            List<int> Y_vertexes = new List<int>();

            foreach(var vertex in graphL.Keys)
            {
                int u = vertex / 10;
                int v = vertex % 10;

                if (u == randomVertex)
                {
                    X_vertexes.Add(vertex);
                }
                if (v == randomVertex)
                {
                    Y_vertexes.Add(vertex);
                }
            }

            int t = 4; // vertex count to add

            */

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
