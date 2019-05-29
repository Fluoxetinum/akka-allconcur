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

        int WatchActor = -1; // for clumsy debuging

        AllConcurConfig algorithmConfig;
        DeploymentConfig deploymentConfig;

        List<IActorRef> allActors;
        List<IActorRef> reliableSuccessors;

        Dictionary<IActorRef, List<IActorRef>> reliableOverlayGraph;
        Dictionary<IActorRef, IActorRef> unreliableOverlayGraph;

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
        bool stageNewMember;
        bool newMembershipInitiator;
        Dictionary<IActorRef, List<IActorRef>> pendingReliableOverlayGraph;

        int currentStage;

        public ServerActor()
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
            stageNewMember = false;
            pendingReliableOverlayGraph = new Dictionary<IActorRef, List<IActorRef>>();
            newMembershipInitiator = false;

            Receive<Messages.InitServer>(Initialize);
            ReceiveAny(_ => Stash.Stash());
        }

        public void Initialize(Messages.InitServer m)
        {

            currentStage = m.Stage;

            algorithmConfig = m.AlgortihmConfig;
            var hs = m.Hosts;
            var h = m.ServerHost;
            var number = m.ServerNumber;

            deploymentConfig = new DeploymentConfig(hs.ToHashSet(), h, number);

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

            if (deploymentConfig.ThisServerNumber == WatchActor)
            {
                StringBuilder str = new StringBuilder(">\n");
                foreach (var pair in reliableOverlayGraph)
                {
                    IActorRef a = pair.Key;
                    str.Append($">> Actor({a.ToShortString()}) = \n");
                    foreach (var x in pair.Value)
                    {
                        str.Append($"{x.ToShortString()}\n");
                    }
                }
                str.Append(">\n");
                Console.WriteLine(str);
            }



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
                    stageNewMember = true;
                }
            });

            Receive<Messages.BroadcastAtomically>(NewMessageToBroadcast);
            Receive<Messages.BroadcastReliably>(NewRbroadcastedMessage);

            Receive<Terminated>((m) => {

                IActorRef crashed = m.ActorRef;
                // TODO: Fauly servers, stop receiving from them (need for Eventually perfect)
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
            Receive<Messages.IndirectFailureNotification>((m) => {

                IActorRef crashed = m.ActorRef;
                IActorRef suspected = Sender;

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
            stageMsg = pendingMessages.Dequeue();
            Console.WriteLine($"{currentStage} - [{Self}] sending {stageMsg.Value}...");
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
            // TODO: What about m.Stage > currentStage?
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

                if (deploymentConfig.ThisServerNumber == WatchActor)
                {
                    Console.WriteLine($"==> Message {m.Message} from {Sender.ToShortString()} was r-delivered <==");
                    showStatus();
                }

            }




            CheckTermination();
        }

        public void showStatus()
        {
            if (deploymentConfig.ThisServerNumber == WatchActor)
            {
                StringBuilder str = new StringBuilder($"\n !!! Tracking Graphs State !!!\n");
                foreach (var g in trackingGraphs)
                {
                    IActorRef a = g.Key;
                    str.Append($"Message from {a.ToShortString()} graph: \n");
                    str.Append(g.Value.GraphInfo());
                }
                str.Append("\n");

                Console.WriteLine(str);
            }
        }

        public void CheckTermination()
        {
            if (trackingGraphs.All(pair => pair.Value.Empty)) // All tracking graphs are cleared 
            {
                var list = stageDeliveredMsgs.ToList();
                list.Sort();

                StringBuilder str = new StringBuilder();
                str.Append($"\n{Self.Path.Name} A-Delivered : ");
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
            Stash.UnstashAll();
            if (pendingMessages.Count != 0)
            {
                SendMyMessage();
            }
        }

        bool firstNotification = true;

        public void NewFailureNotification(VertexPair notification)
        {
            
            failureNotifications.Add(notification);
            if (deploymentConfig.ThisServerNumber == WatchActor)
            {
                if (firstNotification)
                {
                    Console.WriteLine(new string('U', 80));
                    firstNotification = false;
                }
                Console.WriteLine($"XX> Failure notification about {notification.V1.ToShortString()} from {notification.V2.ToShortString()} was r-delivered <XX");
                StringBuilder sb = new StringBuilder();
                foreach (var pair in failureNotifications)
                {
                    sb.Append($"|'{pair.V1.ToShortString()} crashed' - {pair.V2.ToShortString()} said.|\n");    
                }
                Console.WriteLine(sb);

            }
            foreach (var pair in trackingGraphs)
            {
                var graph = pair.Value;
                if (deploymentConfig.ThisServerNumber == WatchActor)
                { 
                    graph.ProcessCrash(notification, failureNotifications, true);
                } 
                else
                {
                    graph.ProcessCrash(notification, failureNotifications, false);
                }
            }

            if (deploymentConfig.ThisServerNumber == WatchActor)
            {
                showStatus();
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
                foreach (var h in deploymentConfig.Hosts)
                {
                    hConfig.Add(h);
                }

                p.Tell(new Messages.MembershipResponse(hConfig.AsReadOnly(), currentStage));
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

            stageNewMember = false;
        }

        public void NewMemberReconfigure(Host newMember)
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
