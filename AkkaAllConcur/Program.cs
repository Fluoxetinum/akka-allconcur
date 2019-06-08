using System;
using System.Threading;
using System.Collections.Generic;
using Akka.Actor;
using Akka.Configuration;
using AkkaAllConcur.Configuration;
using System.Diagnostics;

namespace AkkaAllConcur
{
    class Program
    {
        enum DeploymentMode
        {
            Local, Remote
        }

        const int DEFAULT_N = 9;
        const int DEFAULT_INTERVAL = 1;

        public static string SystemName = "AllConcurSystem";

        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("AkkaAllConcur (hostname port actors_count [hostname_to_connect port_to_connect])");
                return;
            }

            DeploymentMode chosenMode;
            if (args.Length > 4)
            {
                chosenMode = DeploymentMode.Remote;
            }
            else
            {
                chosenMode = DeploymentMode.Local;
            }

            string hostname = args[0];
            string port = args[1];

            int n;
            try
            {
                n = Convert.ToInt32(args[2]);
            }
            catch (Exception)
            {
                n = DEFAULT_N;
            }

            AllConcurConfig algConfig = SettingsParser.ParseAlgConfig();
            ActorSystem system = null;
            try
            {
                system = CreateActorSystem(hostname, port);
            }
            catch (Exception)
            {
                Console.WriteLine($"[ERR]: Actor system creation has failed. Hostname : {hostname}, Port : {port}.");
                return;
            }
            List<IActorRef> thisHostActors = CreateActors(system, n, chosenMode);

            HostInfo currentHost = 
                new HostInfo { HostName = hostname, Port = port, ActorsNumber = n };

            int stage = 0;

            List<IActorRef> allActors;
            List<HostInfo> hosts = new List<HostInfo>();


            int interval;
            try
            {
                if (chosenMode == DeploymentMode.Remote)
                {
                    interval = Convert.ToInt32(args[5]);
                }
                else //if (chosenMode == DeploymentMode.Local)
                {
                    interval = Convert.ToInt32(args[3]);
                }
            }
            catch (Exception)
            {
                interval = DEFAULT_INTERVAL;
            }

            Console.WriteLine(interval);

            if (chosenMode == DeploymentMode.Remote)
            {
                string remoteHost = args[3];
                string remotePort = args[4];

                Console.WriteLine($"Connecting to {remoteHost}:{remotePort}...");
                // Seed node is always srv0 
                string remoteActorPath = $"akka.tcp://{SystemName}@{remoteHost}:{remotePort}/user/svr0";
                var remoteActor = system.ActorSelection(remoteActorPath);
                try
                {
                    var t = remoteActor.ResolveOne(TimeSpan.FromSeconds(30));
                    t.Wait();
                }
                catch (Exception)
                {
                    Console.WriteLine($"[ERR]: Connection to actor system on {remoteHost}:{remotePort} was not established.");
                    return;
                }

                Messages.MembershipResponse m =
                    remoteActor.Ask<Messages.MembershipResponse>(new Messages.MembershipRequest(currentHost), TimeSpan.FromMinutes(5)).Result;

                foreach (var rh in m.Hosts)
                {
                    hosts.Add(rh);
                }

                allActors = Deployment.ReachActors(hosts, system);

                stage = m.NextStage;
            }
            else //if (chosenMode == DeploymentMode.Local)
            {
                Console.WriteLine($"Starting system on {hostname}:{port}");
                hosts.Add(currentHost);
                allActors = thisHostActors;
            }

            int number = 0;
            foreach (var a in thisHostActors)
            {
                a.Tell(new Messages.InitServer(allActors.AsReadOnly(), algConfig, hosts.AsReadOnly(), number, currentHost, stage));
                number++;
            }

            Props props = Props.Create(() => new ClientSimulationActor(thisHostActors, interval));
            IActorRef newActor = system.ActorOf(props, $"client");
           
            Thread.Sleep(TimeSpan.FromMinutes(10));

            system.Dispose();
        }

        static ActorSystem CreateActorSystem(string hostname, string port)
        {   // loglevel = WARNING
            Config systemConfig = ConfigurationFactory.ParseString(@"
                akka {
                    
                    actor {
                        provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                    }
                    remote {
                        dot-netty.tcp {
                            hostname = " + hostname + @"
                            port = " + port + @"
                        }
                        transport-failure-detector {
                            
                        }
                    }
                }
            ");
            
            ActorSystem actorSystem = ActorSystem.Create(SystemName, systemConfig);
            return actorSystem;
        }
        static List<IActorRef> CreateActors(ActorSystem system, int n, DeploymentMode mode)
        {
            int count = n;

            List<IActorRef> actors = new List<IActorRef>();
            for (int i = 0; i < count; i++)
            {
                Props props;
                if (mode == DeploymentMode.Local && i == 0) {
                    props = Props.Create(() => new ServerActor(true));
                }
                else
                {
                    props = Props.Create(() => new ServerActor(false));
                }
                IActorRef newActor = system.ActorOf(props, $"svr{i}");
                actors.Add(newActor);
            }
            return actors;
        }
    }
}
