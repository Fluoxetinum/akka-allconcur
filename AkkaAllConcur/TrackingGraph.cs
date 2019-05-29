using System.Collections.Generic;
using System.Text;
using Akka.Actor;
using System;

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

        public string GraphInfo()
        {
            StringBuilder strb = new StringBuilder();
            foreach (var pair in graph)
            {
                IActorRef a = pair.Key;
                strb.Append($"{a.ToShortString()} => ");
                strb.Append("[");
                foreach (var v in pair.Value)
                {
                    strb.Append(v.ToShortString() + " ");
                }
                strb.Append("]\n");
            }
            return strb.ToString();
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

            if (from.Path.Equals(to.Path)) return true;

            HashSet<IActorRef> visited = new HashSet<IActorRef>();
            Queue<IActorRef> notVisited = new Queue<IActorRef>();
            notVisited.Enqueue(from);

            while (notVisited.Count != 0)
            {
                var node = notVisited.Dequeue();
                visited.Add(node);

                foreach (var n in graph[node])
                {
                    if (visited.Contains(n))
                    {
                        continue;
                    }

                    if (n.Path.Equals(to.Path))
                    {
                        return true;
                    }
                    else
                    {
                        notVisited.Enqueue(n);
                    }
                }
            }

            return false;
        }

        public void ProcessCrash(VertexPair crashedLink, HashSet<VertexPair> F, bool outFlag)
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
                    if (!n.Equals(pk) && !n.Equals(trackedServer))
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

                        foreach (var err in F)
                        {
                            if (err.V1.Path.Equals(link.V2.Path))
                            {
                                foreach (var n in allSuccessors[link.V2])
                                {
                                    if (n.Path.Equals(trackedServer.Path)) continue;
                                    VertexPair additional = new VertexPair(link.V2, n);
                                    if (outFlag == true)
                                    {
                                        Console.WriteLine($"====================> Try to add {additional.V1.ToShortString()} {additional.V2.ToShortString()}");
                                    }
                                    
                                    if (!F.Contains(additional))
                                    {
                                        if (outFlag == true)
                                        {
                                            Console.WriteLine($"====================> added {additional.V1.ToShortString()} {additional.V2.ToShortString()}");
                                        }
                                        possibleRecipients.Enqueue(additional);
                                    }
                                }
                                break;
                            }
                        }

                    }


                }
            }
            else
            {
                graph[p].Remove(pk);

                if (graph[p].Count == 0) // Not checked
                {
                    Queue<IActorRef> pToDel = new Queue<IActorRef>();
                    pToDel.Enqueue(p);

                    while (pToDel.Count != 0)
                    {
                        IActorRef currentP = pToDel.Dequeue();
                        graph.Remove(currentP);

                        foreach (var key in graph.Keys)
                        {
                            graph[key].Remove(currentP);
                            if (graph[key].Count == 0)
                            {
                                pToDel.Enqueue(key);
                            }
                        }
                    }
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

            bool failure = false;

            foreach (var pair in graph)
            {
                IActorRef link = pair.Key;
                foreach (var err in F)
                {
                    if (err.V1.Path.Equals(link.Path))
                    {
                        failure = true;
                        break;
                    }
                }

                if (!failure)
                {
                    return;
                }
                failure = false;
            }
            if (outFlag)
            {
                Console.WriteLine($"XXXXX {trackedServer.ToShortString()} cleared");
            }
            Clear();

        }
    }
}
