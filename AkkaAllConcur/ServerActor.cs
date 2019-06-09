using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Collections.ObjectModel;
using Akka.Actor;
using AkkaAllConcur.Configuration;
using System;

namespace AkkaAllConcur
{
    class ServerActor : ReceiveActor, IWithUnboundedStash
    {
        int ACTOR_CAP = 1;

        public IStash Stash { get; set; }

        Stopwatch timer = new Stopwatch();

        AllConcurConfig algorithmConfig;
        DeploymentConfig deploymentConfig;

        List<IActorRef> allActors;
        List<IActorRef> reliableSuccessors;
        List<IActorRef> predecessors;

        Dictionary<IActorRef, List<IActorRef>> reliableOverlayGraph;

        HashSet<VertexPair> failureNotifications;
        HashSet<IActorRef> faultyServers;
        Dictionary<IActorRef, TrackingGraph> trackingGraphs;

        Queue<Messages.Abroadcast> pendingMessages;
        Messages.Abroadcast EMPTY_MSG = new Messages.Abroadcast(null);
        Messages.Abroadcast stageMsg;

        HashSet<Messages.Rbroadcast> stageDeliveredMsgs;
        HashSet<IActorRef> stageReceivedFrom;

        HostInfo newMember;
        bool stageNewMemberFlag;

        int currentRound;

        IActorRef logger;

        HashSet<IActorRef> olds;

        public ServerActor(bool logging = false)
        {
            allActors = new List<IActorRef>();
            olds = new HashSet<IActorRef>();
            reliableSuccessors = new List<IActorRef>();
            predecessors = new List<IActorRef>();

            faultyServers = new HashSet<IActorRef>();
            failureNotifications = new HashSet<VertexPair>();
            trackingGraphs = new Dictionary<IActorRef, TrackingGraph>();

            pendingMessages = new Queue<Messages.Abroadcast>();

            stageDeliveredMsgs = new HashSet<Messages.Rbroadcast>();
            stageReceivedFrom = new HashSet<IActorRef>();
            stageMsg = null;
            currentRound = 0;

            stageNewMemberFlag = false;

            logger = null;
            if (logging)
            {
                logger = Context.ActorOf<LogActor>();
            }

            Receive<Messages.InitServer>(Initialize);
            ReceiveAny(_ => Stash.Stash());
        }

        #region Logger Functions
        public void SendLogMsg(string msg)
        {
            if (logger != null)
            {
                logger.Tell(new Messages.LogMessage(msg));
            }
        }
        public void SendADeliverLogMsg(List<Messages.Rbroadcast> list, Stopwatch timer)
        {
            if (logger != null)
            {
                if (algorithmConfig.OutputVerbosity == AllConcurConfig.OutputVerbosityType.INFO)
                {
                    logger.Tell(new Messages.LogABcastVerbose(list.AsReadOnly(), timer.ElapsedNanoSeconds(), trackingGraphs.Count));
                }
                else if (algorithmConfig.OutputVerbosity == AllConcurConfig.OutputVerbosityType.TIME_ONLY)
                {
                    int throughput = list.Aggregate(0, (seed, n) => {
                        var q = n.Message.Value as ReadOnlyCollection<Messages.Abroadcast>;
                        if (q != null)
                        {
                            return seed + q.Count;
                        } 
                        else
                        {
                            return seed + 1;
                        }
                    });

                    logger.Tell(new Messages.LogAbcast(list[0].Round, timer.ElapsedNanoSeconds(), trackingGraphs.Count, throughput));
                }
            }
        }

        public void SendGraphLogMsg(Dictionary<IActorRef, List<IActorRef>> reliableOverlayGraph)
        {
            if (logger != null)
            {
                if (algorithmConfig.OutputVerbosity == AllConcurConfig.OutputVerbosityType.DEBUG)
                {
                    var graphMsg = new ReadOnlyDictionary<IActorRef, ReadOnlyCollection<IActorRef>>
                        (reliableOverlayGraph.ToDictionary(k => k.Key, v => v.Value.AsReadOnly()));

                    logger.Tell(new Messages.LogGraph(graphMsg));
                }
            }
        }

        public void SendTrackGraphLogMsg()
        {
            if (logger != null)
            {
                if (algorithmConfig.OutputVerbosity == AllConcurConfig.OutputVerbosityType.DEBUG)
                {
                    var graphMsg = new ReadOnlyDictionary<IActorRef, ReadOnlyDictionary<IActorRef, ReadOnlyCollection<IActorRef>>>
                    (trackingGraphs.ToDictionary(k => k.Key, v => v.Value.GetGraphCopy()));

                    logger.Tell(new Messages.LogTrackGraph(graphMsg));
                }
            }
        }

        public void SendFailureLogMsg(VertexPair notification)
        {
            if (logger != null)
            {
                if (algorithmConfig.OutputVerbosity == AllConcurConfig.OutputVerbosityType.INFO)
                {
                    logger.Tell(new Messages.LogFailure(notification));
                }
                else if (algorithmConfig.OutputVerbosity == AllConcurConfig.OutputVerbosityType.DEBUG)
                {
                    logger.Tell(new Messages.LogFailureVerbose(notification, failureNotifications.ToList().AsReadOnly()));
                }
            }
        }
        #endregion

        public void Initialize(Messages.InitServer m)
        {
            currentRound = m.Round;

            algorithmConfig = m.AlgortihmConfig;
            deploymentConfig = new DeploymentConfig(m.Hosts.ToHashSet(), m.ServerHost, m.ServerNumber);

            foreach (var a in m.AllActors)
            {
                allActors.Add(a);
            }

            reliableOverlayGraph = GraphCreator.ComputeAllReliableSuccessors(algorithmConfig, allActors);

            SendGraphLogMsg(reliableOverlayGraph);

            foreach (var a in reliableOverlayGraph[Self])
            {
                reliableSuccessors.Add(a);
                Context.Watch(a);
            }

            predecessors = GraphCreator.ComputeInverse(reliableOverlayGraph, Self);
            foreach (var a in predecessors)
            {
                Context.Watch(a);
            }

            foreach (var a in allActors)
            {
                trackingGraphs[a] = new TrackingGraph(reliableOverlayGraph, a);
            }
            trackingGraphs[Self].Clear();

            BecomeReady();
        }

        public void BecomeReady()
        {
            Stash.UnstashAll();
            Become(Ready);
        }

        public void Ready()
        {
            Receive<Messages.Abroadcast>(NewMessageToBroadcast);
            Receive<Messages.Rbroadcast>(NewRbroadcastedMessage);

            Receive<Terminated>((m) =>
            {

                IActorRef crashed = m.ActorRef;   
                faultyServers.Add(crashed);
                IActorRef suspected = Self;
                VertexPair notification = new VertexPair(crashed, suspected);
                if (!failureNotifications.Contains(notification))
                {
                    foreach (var a in reliableSuccessors)
                    {
                        a.Tell(new Messages.IndirectFailureNotification(crashed));
                    }
                }

                NewFailureNotification(notification);

            });
            Receive<Messages.IndirectFailureNotification>((m) =>
            {

                IActorRef crashed = m.ActorRef;
                IActorRef suspected = Sender;
                faultyServers.Add(crashed);
                VertexPair notification = new VertexPair(crashed, suspected);
                if (!failureNotifications.Contains(notification))
                {
                    foreach (var a in reliableSuccessors)
                    {
                        a.Forward(m);
                    }
                }

                NewFailureNotification(notification);

            });
            ReceiveAny(_ => Stash.Stash());
            
        }

        public void NewMessageToBroadcast(Messages.Abroadcast m)
        {
            pendingMessages.Enqueue(m);

            if (stageMsg == null && pendingMessages.Count >= ACTOR_CAP)
            {
                SendMyMessage();
            }
        }

        public void SendMyMessage()
        {
            timer.Restart();

            Messages.Rbroadcast rbm;

            if (pendingMessages.Count == 0)
            {
                stageMsg = EMPTY_MSG;
                rbm = new Messages.Rbroadcast(stageMsg, currentRound);
            }
            else
            {
                var list = new List<Messages.Abroadcast>();

                int c = ACTOR_CAP;
                if (pendingMessages.Count > ACTOR_CAP)
                {
                    c *= 3;
                }

                for (int i = 0; i < c && i < pendingMessages.Count; i++)
                {
                    list.Add(pendingMessages.Dequeue());
                }

                stageMsg = new Messages.Abroadcast(list.AsReadOnly());

                rbm = new Messages.Rbroadcast(stageMsg, currentRound);
            }

            foreach (var s in reliableSuccessors)
            {
                s.Tell(rbm);
            }
            stageDeliveredMsgs.Add(rbm);
            stageReceivedFrom.Add(Self);
            CheckTermination(); 
        }

        public void NewRbroadcastedMessage(Messages.Rbroadcast m)
        {
            if (faultyServers.Contains(Sender))
            {
                return;
            }

            if (m.Round < currentRound)
            {
                return;
            }
            if (m.Round > currentRound)
            {
                Stash.Stash();
                return;
            }

            if (stageMsg == null)
            {
                SendMyMessage();
            }

            if (stageDeliveredMsgs.Add(m)) // not yet delivered
            {
                stageReceivedFrom.Add(Sender);
                foreach (var s in reliableSuccessors)
                {
                    s.Forward(m);
                }
                trackingGraphs[Sender].Clear();

                if (deploymentConfig.ThisServerNumber == 0)
                {
                    SendTrackGraphLogMsg();
                }


                if (stageNewMemberFlag && stageReceivedFrom.SetEquals(olds))
                {
                    SendLogMsg("Joining completed.");
                    var p = 
                        Context.ActorSelection($"akka.tcp://{Program.SystemName}@{newMember.HostName}:{newMember.Port}/user/ack");

                    List<HostInfo> hConfig = new List<HostInfo>();
                    foreach (var h in deploymentConfig.Hosts)
                    {
                        hConfig.Add(h);
                    }

                    p.Tell(new Messages.MembershipResponse(hConfig.AsReadOnly(), currentRound));
                    stageNewMemberFlag = false;

                }
            }
            CheckTermination();
        }

        public void CheckTermination()
        {
            if (trackingGraphs.All(pair => pair.Value.Empty)) // All tracking graphs are cleared 
            {
                var list = stageDeliveredMsgs.ToList();
                list.Sort();

                foreach (var m in list)
                {
                    var innerList = m.Message.Value as ReadOnlyCollection<Messages.Abroadcast>;
                    if (innerList == null)
                    {
                        continue;
                    }
                    foreach (var im in innerList)
                    {
                        var req = im.Value as Messages.MembershipRequest;
                        if (req != null)
                        {
                            stageNewMemberFlag = true;
                            newMember = req.NewHost;
                            break;
                        }
                    }

                }

                timer.Stop();

                SendADeliverLogMsg(list, timer);

                NextStagePreparing();
            }
        }

        public void NextStagePreparing()
        {

            // If Server does not deliver a message it will be regarded as failed.
            List<IActorRef> keysToDelete = new List<IActorRef>();
            foreach (var key in trackingGraphs.Keys)
            {
                if (!stageReceivedFrom.Contains(key))
                {
                    keysToDelete.Add(key);
                }
            }
            foreach (var key in keysToDelete)
            {
                trackingGraphs.Remove(key);
                reliableSuccessors.Remove(key);
                Context.Unwatch(key);
            }

            foreach (var err in failureNotifications)
            {
                if (trackingGraphs.ContainsKey(err.V1))
                {
                    foreach (var actor in reliableSuccessors)
                    {
                        actor.Tell(new Messages.IndirectFailureNotification(err.V1), err.V2);
                    }
                }
            }

            stageDeliveredMsgs.Clear();
            stageReceivedFrom.Clear();
            foreach (var pair in trackingGraphs)
            {
                if (pair.Key != Self)
                {
                    pair.Value.Reset();
                }
            }

            currentRound++;
            stageMsg = null;

            if (stageNewMemberFlag)
            {
                olds = new HashSet<IActorRef>(allActors);
                NewMemberReconfigure(newMember);
                SendLogMsg("New membership request.");
            }

            Stash.UnstashAll();
            if (pendingMessages.Count != 0 && pendingMessages.Count >= ACTOR_CAP)
            {
                SendMyMessage();
            }
        }

        public void NewFailureNotification(VertexPair notification)
        {
            failureNotifications.Add(notification);

            SendFailureLogMsg(notification);

            foreach (var pair in trackingGraphs)
            {
                var graph = pair.Value;
                if (algorithmConfig.OutputVerbosity == AllConcurConfig.OutputVerbosityType.DEBUG)
                {
                    graph.ProcessCrash(notification, failureNotifications, true);
                }
                else
                {
                    graph.ProcessCrash(notification, failureNotifications, false);
                }
            }

            SendTrackGraphLogMsg();

            CheckTermination();
        }

        public void NewMemberReconfigure(HostInfo newMember)
        {

            deploymentConfig.Hosts.Add(newMember);
            allActors = Deployment.ReachActors(newMember, Context.System, allActors);
            var pendingReliableOverlayGraph = GraphCreator.ComputeAllReliableSuccessors(algorithmConfig, allActors);

            reliableSuccessors.Clear();

            foreach (var s in pendingReliableOverlayGraph[Self])
            {
                reliableSuccessors.Add(s);
                Context.Watch(s);
            }

            foreach (var s in reliableOverlayGraph[Self])
            {
                if (!reliableSuccessors.Contains(s))
                {
                    Context.Unwatch(s);
                }
            }

            reliableOverlayGraph = pendingReliableOverlayGraph;

            var pendingPredecessors = GraphCreator.ComputeInverse(reliableOverlayGraph, Self);
            foreach (var a in pendingPredecessors)
            {
                Context.Watch(a);
            }

            foreach (var a in predecessors)
            {
                if (!pendingPredecessors.Contains(a))
                {
                    Context.Unwatch(a);
                }
            }

            predecessors = pendingPredecessors;

            trackingGraphs = new Dictionary<IActorRef, TrackingGraph>();
            foreach (var a in allActors)
            {
                trackingGraphs[a] = new TrackingGraph(reliableOverlayGraph, a);
            }
            trackingGraphs[Self].Clear();
        }
    }
}
