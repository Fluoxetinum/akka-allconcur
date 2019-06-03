using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Collections.ObjectModel;
using Akka.Actor;
using AkkaAllConcur.Configuration;

namespace AkkaAllConcur
{
    class ServerActor : ReceiveActor, IWithUnboundedStash
    {
        public IStash Stash { get; set; }

        Stopwatch timer = new Stopwatch();

        AllConcurConfig algorithmConfig;
        DeploymentConfig deploymentConfig;

        List<IActorRef> allActors;
        List<IActorRef> reliableSuccessors;

        Dictionary<IActorRef, List<IActorRef>> reliableOverlayGraph;

        HashSet<VertexPair> failureNotifications;
        HashSet<IActorRef> faultyServers;
        Dictionary<IActorRef, TrackingGraph> trackingGraphs;

        Queue<Messages.BroadcastAtomically> pendingMessages;
        Messages.BroadcastAtomically EMPTY_MSG = new Messages.BroadcastAtomically(null);
        Messages.BroadcastAtomically stageMsg;

        HashSet<Messages.BroadcastReliably> stageDeliveredMsgs;
        HashSet<IActorRef> stageReceivedFrom;

        Queue<Messages.MembershipRequest> membershipRequests;
        Queue<IActorRef> pendingMembers;
        bool stageNewMemberFlag;
        bool newMembershipInitiatorFlag;
        Dictionary<IActorRef, List<IActorRef>> pendingReliableOverlayGraph;

        int currentStage;

        IActorRef logger;

        public ServerActor(bool logging = false)
        {
            allActors = new List<IActorRef>();
            reliableSuccessors = new List<IActorRef>();

            faultyServers = new HashSet<IActorRef>();
            failureNotifications = new HashSet<VertexPair>();
            trackingGraphs = new Dictionary<IActorRef, TrackingGraph>();

            pendingMessages = new Queue<Messages.BroadcastAtomically>();

            stageDeliveredMsgs = new HashSet<Messages.BroadcastReliably>();
            stageReceivedFrom = new HashSet<IActorRef>();
            stageMsg = null;
            currentStage = 0;

            membershipRequests = new Queue<Messages.MembershipRequest>();
            pendingMembers = new Queue<IActorRef>();
            stageNewMemberFlag = false;
            pendingReliableOverlayGraph = new Dictionary<IActorRef, List<IActorRef>>();
            newMembershipInitiatorFlag = false;

            logger = null;
            if (logging)
            {
                logger = Context.ActorOf<LogActor>();
            }

            Receive<Messages.InitServer>(Initialize);
            ReceiveAny(_ => Stash.Stash());
        }

        #region Logger Functions
        public void SendADeliverLogMsg(List<Messages.BroadcastReliably> list, Stopwatch timer)
        {
            if (logger != null)
            {
                if (algorithmConfig.OutputVerbosity == AllConcurConfig.OutputVerbosityType.INFO)
                {
                    logger.Tell(new Messages.LogABcastVerbose(list.AsReadOnly(), timer.ElapsedNanoSeconds()));
                }
                else if (algorithmConfig.OutputVerbosity == AllConcurConfig.OutputVerbosityType.TIME_ONLY)
                {
                    logger.Tell(new Messages.LogAbcast(list[0].Stage, timer.ElapsedNanoSeconds()));
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
            currentStage = m.Stage;

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
            Receive<Messages.MembershipRequest>((m) =>
            {
                if (!deploymentConfig.Hosts.Contains(m.NewHost))
                {
                    membershipRequests.Enqueue(m);
                    pendingMembers.Enqueue(Sender);
                }
            });
            Receive<Messages.NewMemberNotification>((m) =>
            {
                if (!deploymentConfig.Hosts.Contains(m.NewMember))
                {
                    NewMemberReconfigure(m.NewMember);
                    foreach (var s in reliableSuccessors)
                    {
                        s.Tell(new Messages.NewMemberNotification(m.NewMember));
                    }
                    stageNewMemberFlag = true;
                }
            });

            Receive<Messages.BroadcastAtomically>(NewMessageToBroadcast);
            Receive<Messages.BroadcastReliably>(NewRbroadcastedMessage);

            Receive<Terminated>((m) =>
            {

                IActorRef crashed = m.ActorRef;
                
                faultyServers.Add(crashed);

                IActorRef suspected = Self;

                VertexPair notification = new VertexPair(crashed, suspected);

                if (failureNotifications.Contains(notification))
                {
                    return;
                }

                foreach (var a in reliableSuccessors)
                {
                    a.Tell(new Messages.IndirectFailureNotification(crashed));
                }

                NewFailureNotification(notification);

            });
            Receive<Messages.IndirectFailureNotification>((m) =>
            {

                IActorRef crashed = m.ActorRef;
                IActorRef suspected = Sender;

                faultyServers.Add(crashed);

                VertexPair notification = new VertexPair(crashed, suspected);

                if (failureNotifications.Contains(notification))
                {
                    return;
                }

                foreach (var a in reliableSuccessors)
                {
                    a.Forward(m);
                    a.Tell(new Messages.IndirectFailureNotification(crashed));
                }

                NewFailureNotification(notification);

                VertexPair notificationSelf = new VertexPair(crashed, Self);

                if (failureNotifications.Contains(notificationSelf))
                {
                    return;
                }

                NewFailureNotification(notificationSelf);
            });
            ReceiveAny(_ => Stash.Stash());
        }

        public void NewMessageToBroadcast(Messages.BroadcastAtomically m)
        {
            pendingMessages.Enqueue(m);
            if (stageMsg == null)
            {
                SendMyMessage();
            }
        }

        public void SendMyMessage()
        {
            timer.Restart();
            stageMsg = pendingMessages.Dequeue();
            Messages.BroadcastReliably rbm = new Messages.BroadcastReliably(stageMsg, currentStage);
            foreach (var s in reliableSuccessors)
            {
                s.Tell(rbm);
            }
            stageDeliveredMsgs.Add(rbm);
            stageReceivedFrom.Add(Self);
        }

        public void NewRbroadcastedMessage(Messages.BroadcastReliably m)
        {

            if (faultyServers.Contains(Sender))
            {
                return;
            }

            if (m.Stage < currentStage)
            {
                return;
            }
            if (m.Stage > currentStage)
            {
                Stash.Stash();
                return;
            }

            if (stageMsg == null)
            {
                if (pendingMessages.Count == 0)
                {
                    pendingMessages.Enqueue(EMPTY_MSG);
                }
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

            }




            CheckTermination();
        }

        public void CheckTermination()
        {
            if (trackingGraphs.All(pair => pair.Value.Empty)) // All tracking graphs are cleared 
            {
                var list = stageDeliveredMsgs.ToList();
                list.Sort();

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

            currentStage++;
            stageMsg = null;

            if (membershipRequests.Count != 0 && !stageNewMemberFlag)
            {
                InitMembershipChange();
            }
            else if (stageNewMemberFlag)
            {
                CompleteMembershipChange();
            }
            Stash.UnstashAll();
            if (pendingMessages.Count != 0)
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

        public void InitMembershipChange()
        {
            Messages.MembershipRequest req = membershipRequests.Dequeue();

            NewMemberReconfigure(req.NewHost);

            foreach (var s in reliableSuccessors)
            {
                s.Tell(new Messages.NewMemberNotification(req.NewHost));
            }

            newMembershipInitiatorFlag = true;
            stageNewMemberFlag = true;
        }

        public void CompleteMembershipChange()
        {
            if (newMembershipInitiatorFlag)
            {
                IActorRef p = pendingMembers.Dequeue();

                List<HostInfo> hConfig = new List<HostInfo>();
                foreach (var h in deploymentConfig.Hosts)
                {
                    hConfig.Add(h);
                }

                p.Tell(new Messages.MembershipResponse(hConfig.AsReadOnly(), currentStage));
                newMembershipInitiatorFlag = false;
            }

            foreach (var s in reliableSuccessors)
            {
                if (!pendingReliableOverlayGraph[Self].Contains(s))
                {
                    Context.Unwatch(s);
                }
            }
            reliableOverlayGraph = pendingReliableOverlayGraph;
            pendingReliableOverlayGraph = null;

            reliableSuccessors.Clear();
            foreach (var a in reliableOverlayGraph[Self])
            {
                reliableSuccessors.Add(a);
            }
            trackingGraphs = new Dictionary<IActorRef, TrackingGraph>();
            foreach (var a in allActors)
            {
                trackingGraphs[a] = new TrackingGraph(reliableOverlayGraph, a);
            }
            trackingGraphs[Self].Clear();

            stageNewMemberFlag = false;
        }

        public void NewMemberReconfigure(HostInfo newMember)
        {
            deploymentConfig.Hosts.Add(newMember);
            allActors = Deployment.ReachActors(newMember, Context.System, allActors);
            pendingReliableOverlayGraph = GraphCreator.ComputeAllReliableSuccessors(algorithmConfig, allActors);

            foreach (var s in pendingReliableOverlayGraph[Self])
            {
                Context.Watch(s);
            }
        }
    }
}
