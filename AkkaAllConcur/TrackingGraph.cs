using System.Collections.Generic;
using Akka.Actor;

namespace AkkaAllConcur
{
    class TrackingGraph
    {
        IActorRef trackedServer;
        Dictionary<IActorRef, LinkedList<IActorRef>> graph;
        Dictionary<IActorRef, List<IActorRef>> allSuccessors;

        public bool Empty
        {
            get
            {
                return graph.Count == 0;
            }
        }

        public TrackingGraph(Dictionary<IActorRef, List<IActorRef>> alls, IActorRef server)
        {
            allSuccessors = alls;
            trackedServer = server;
            graph = new Dictionary<IActorRef, LinkedList<IActorRef>>();
            Reset();
        }

        public void Reset()
        {
            graph[trackedServer] = new LinkedList<IActorRef>();
        }

        public void Clear()
        {
            graph.Clear();
        }

        private bool hasSuccessors(IActorRef actor)
        {
            return graph[actor].Count != 0;
        }

        private bool pathExists(IActorRef to)
        {
            IActorRef from = trackedServer;
            HashSet<IActorRef> visited = new HashSet<IActorRef>();
            Queue<IActorRef> notVisited = new Queue<IActorRef>();
            notVisited.Enqueue(from);

            while (notVisited.Count != 0)
            {
                var node = notVisited.Dequeue();
                visited.Add(node);

                foreach (var n in allSuccessors[node])
                {
                    if (visited.Contains(n))
                    {
                        continue;
                    }

                    if (n == to)
                    {
                        return false;
                    }
                    else
                    {
                        notVisited.Enqueue(n);
                    }
                }
            }

            return false;
        }

        public void ProcessCrash(VertexPair crashedLink, HashSet<VertexPair> F)
        {
            IActorRef p = crashedLink.V1;
            IActorRef pk = crashedLink.V2;

            if (!graph.ContainsKey(p))
            {
                return;
            }

            if (!hasSuccessors(p))
            {
                Queue<VertexPair> possibleRecipients = new Queue<VertexPair>();

                foreach (var n in allSuccessors[p])
                {
                    if (n != pk && n != trackedServer)
                    {
                        possibleRecipients.Enqueue(new VertexPair(p, n));
                    }
                }

                while (possibleRecipients.Count != 0)
                {
                    VertexPair link = possibleRecipients.Dequeue();
                    graph[link.V1].AddLast(link.V2);
                    if (!graph.ContainsKey(link.V2))
                    {
                        graph[link.V2] = new LinkedList<IActorRef>();
                    }

                    foreach (var err in F)
                    {
                        if (err.V1 == link.V2)
                        {

                            if (graph[link.V2].Count != 0) continue;

                            foreach (var n in allSuccessors[link.V2])
                            {
                                if (n == trackedServer) continue;
                                VertexPair additional = new VertexPair(link.V2, n);
                                if (!F.Contains(additional))
                                {
                                    possibleRecipients.Enqueue(additional);
                                }
                            }
                            break;
                        }
                    }
                }
            }
            else
            {
                graph[p].Remove(pk);

                if (graph[p].Count == 0)
                {
                    graph.Remove(p);
                }

                List<IActorRef> keysToDelete = new List<IActorRef>();
                foreach (var pair in graph)
                {
                    if (!pathExists(pair.Key))
                    {
                        keysToDelete.Add(pair.Key);
                    }
                }
                foreach (var k in keysToDelete)
                {
                    graph.Remove(k);
                }
            }

            int failures = 0;

            foreach (var pair in graph)
            {
                IActorRef link = pair.Key;
                foreach (var err in F)
                {
                    if (err.V1 == link)
                    {
                        failures++;
                        break;
                    }
                }
            }
            if (failures == graph.Count)
            {
                Clear();
            }
        }
    }
}
