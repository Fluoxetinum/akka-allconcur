using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Akka.Actor;
using AkkaAllConcur.Configuration;

namespace AkkaAllConcur
{
    class ServerActor : ReceiveActor, IWithUnboundedStash
    {
        public IStash Stash { get; set; }

        Host myHost;
        int myNumber;

        AllConcurConfig algorithmConfig;
        // Deployment?
        DeploymentConfig deployingConfig;

        List<IActorRef> allActors;
        List<IActorRef> reliableSuccessors;

        Dictionary<IActorRef, List<IActorRef>> reliableOverlayGraph;
        Dictionary<IActorRef, IActorRef> unreliableOverlayGraph;

        HashSet<VertexPair> failureNotifications;
        Dictionary<IActorRef, TrackingGraph> trackingGraphs;

        Queue<Messages.BroadcastAtomically> pendingMessages;
        Messages.BroadcastAtomically EMPTY_MSG = new Messages.BroadcastAtomically(null);
        Messages.BroadcastAtomically stageMsg;

        HashSet<Messages.BroadcastReliably> stageDeliveredMsgs;
        HashSet<IActorRef> stageReceivedFrom;

        Queue<Messages.MembershipRequest> membershipRequests;
        Queue<IActorRef> pendingMembers;
        bool stageNewMember;
        bool newMembershipInitiator;
        Dictionary<IActorRef, List<IActorRef>> pendingReliableOverlayGraph;

        int currentStage;

        public ServerActor()
        {
            allActors = new List<IActorRef>();
            reliableSuccessors = new List<IActorRef>();

            failureNotifications = new HashSet<VertexPair>();
            trackingGraphs = new Dictionary<IActorRef, TrackingGraph>();
            pendingMessages = new Queue<Messages.BroadcastAtomically>();

            stageDeliveredMsgs = new HashSet<Messages.BroadcastReliably>();
            stageReceivedFrom = new HashSet<IActorRef>();

            stageMsg = null;
            currentStage = 0;

            membershipRequests = new Queue<Messages.MembershipRequest>();
            pendingMembers = new Queue<IActorRef>();
            stageNewMember = false;
            pendingReliableOverlayGraph = new Dictionary<IActorRef, List<IActorRef>>();
            newMembershipInitiator = false;

            Receive<Messages.InitServer>(Initialize);
            ReceiveAny(_ => Stash.Stash());
        }

        public void Initialize(Messages.InitServer m)
        {
            algorithmConfig = m.AlgortihmConfig;
            var hs = m.Hosts;
            var h = m.ServerHost;
            var number = m.ServerNumber;

            deployingConfig = new DeploymentConfig(hs.ToHashSet(), h, number);

            foreach (var a in m.AllActors)
            {
                allActors.Add(a);
            }

            reliableOverlayGraph = GraphCreator.ComputeAllReliableSuccessors(algorithmConfig, allActors);
            unreliableOverlayGraph = GraphCreator.ComputeAllUnreliableSucessors(allActors);

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

            myHost = m.ServerHost;
            myNumber = m.ServerNumber;

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
                if (!deployingConfig.Hosts.Contains(m.NewHost))
                {
                    membershipRequests.Enqueue(m);
                    pendingMembers.Enqueue(Sender);
                }
            });
            Receive<Messages.NewMemberNotification>((m) =>
            {
                if (!deployingConfig.Hosts.Contains(m.NewMember))
                {
                    NewMemberReconfigure(m.NewMember);
                    foreach (var s in reliableSuccessors)
                    {
                        s.Tell(new Messages.NewMemberNotification(m.NewMember));
                    }
                    stageNewMember = true;
                }
            });

            Receive<Messages.BroadcastAtomically>(NewMessageToBroadcast);
            Receive<Messages.BroadcastReliably>(NewRbroadcastedMessage);

            Receive<Terminated>((m) => NewFailureNotification(m.ActorRef, Self));
            Receive<Messages.IndirectFailureNotification>((m) => NewFailureNotification(m.ActorRef, Sender));
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
            stageMsg = pendingMessages.Dequeue();
            Messages.BroadcastReliably rbm = new Messages.BroadcastReliably(stageMsg, currentStage);
            foreach (var s in reliableSuccessors)
            {
                s.Tell(rbm);
            }
            stageDeliveredMsgs.Add(rbm);
        }

        public void NewRbroadcastedMessage(Messages.BroadcastReliably m)
        {
            // TODO: What about m.Stage > currentStage?
            if (m.Stage < currentStage)
            {
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
            }

            CheckTermination();
        }

        public void CheckTermination()
        {
            if (trackingGraphs.All(pair => pair.Value.Empty)) // All tracking graphs are cleared 
            {
                var list = stageDeliveredMsgs.ToList();
                list.Sort();

                StringBuilder str = new StringBuilder();
                str.Append($"{Self.Path.Name} A-Delivered : ");
                foreach (var m in list)
                {
                    str.Append($"[S{m.Stage}:{m.Message}],");
                }
                Console.WriteLine(str.ToString());

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

            if (membershipRequests.Count != 0 && !stageNewMember)
            {
                InitMembershipChange();
            }
            else if (stageNewMember)
            {
                CompleteMembershipChange();
            }

            if (pendingMessages.Count != 0)
            {
                SendMyMessage();
            }
        }

        public void NewFailureNotification(IActorRef crashed, IActorRef suspected)
        {
            VertexPair notification = new VertexPair(crashed, suspected);

            if (failureNotifications.Contains(notification))
            {
                return;
            }

            foreach (var a in reliableSuccessors)
            {
                a.Tell(new Messages.IndirectFailureNotification(crashed));
            }

            failureNotifications.Add(notification);

            foreach (var pair in trackingGraphs)
            {
                var graph = pair.Value;
                graph.ProcessCrash(notification, failureNotifications);
            }

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

            newMembershipInitiator = true;
            stageNewMember = true;
        }

        public void CompleteMembershipChange()
        {
            if (newMembershipInitiator)
            {
                IActorRef p = pendingMembers.Dequeue();

                List<Host> hConfig = new List<Host>();
                foreach (var h in deployingConfig.Hosts)
                {
                    hConfig.Add(h);
                }

                p.Tell(new Messages.MembershipResponse(hConfig.AsReadOnly(), hConfig.Count - 1));
                newMembershipInitiator = false;
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
            foreach (var a in reliableOverlayGraph[Self])
            {
                reliableSuccessors.Add(a);
            }
            foreach (var a in allActors)
            {
                trackingGraphs[a] = new TrackingGraph(reliableOverlayGraph, a);
            }
            trackingGraphs[Self].Clear();

            stageNewMember = false;
        }

        public void NewMemberReconfigure(Host newMember)
        {
            deployingConfig.Hosts.Add(newMember);
            allActors = Deployment.ReachActors(newMember, Context.System, allActors);
            pendingReliableOverlayGraph = GraphCreator.ComputeAllReliableSuccessors(algorithmConfig, allActors);

            foreach (var s in pendingReliableOverlayGraph[Self])
            {
                Context.Watch(s);
            }
        }
    }
}
