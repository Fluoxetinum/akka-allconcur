using System;
using System.Xml;
using System.Collections.Generic;
using Akka.Actor;
using Akka.Configuration;
using AkkaAllConcur.Configuration;

namespace AkkaAllConcur
{
    class Program
    {
        public static string SystemName = "AllConcurSystem";

        static void Main(string[] args)
        {
            string hostname = args[0];
            string port = args[1];
            int n = Convert.ToInt32(args[2]);

            AllConcurConfig algConfig = SettingsParser.ParseAlgConfig();
            ActorSystem system = CreateActorSystem(hostname, port);
            List<IActorRef> thisHostActors = CreateActors(system, n);

            Host currentHost = new Host { HostName = hostname, Port = port, ActorsNumber = n };

            bool attach;
            while (true)
            {
                Console.WriteLine("|ALLCONCUR|: Do you want to attach to [r]emote system or [s]tart your own?");
                Console.Write(" => ");
                string input = Console.ReadLine();
                switch(input)
                {
                    case "s":
                    case "start":
                        attach = false;
                        break;
                    case "r":
                    case "remote":
                        attach = true;
                        break;
                    default:
                        continue;
                }
                break;
            }

            List<IActorRef> allActors;
            List<Host> hosts = new List<Host>();
            
            if (!attach) // first machine in a system
            {
                hosts.Add(currentHost);
                allActors = thisHostActors;
            }
            else
            {
                Console.WriteLine("|ALLCONCUR|: Please, input ip and port of the remote system actor yout want to connect to.");
                Console.Write(" IP (localhost) => ");
                string remoteHost = Console.ReadLine();
                if (string.IsNullOrEmpty(remoteHost))
                {
                    remoteHost = "localhost";
                }
                Console.Write(" Port (14700) => ");
                string remotePort = Console.ReadLine();
                if (string.IsNullOrEmpty(remotePort))
                {
                    remotePort = "14700";
                }

                string remoteActorPath = $"akka.tcp://{SystemName}@{remoteHost}:{remotePort}/user/svr0";
                var remoteActor = system.ActorSelection(remoteActorPath);
                var t = remoteActor.ResolveOne(TimeSpan.FromMinutes(5));
                t.Wait();

                Messages.MembershipResponse m = 
                    remoteActor.Ask<Messages.MembershipResponse>(new Messages.MembershipRequest(currentHost), TimeSpan.FromMinutes(5)).Result;

                foreach (var rh in m.Hosts)
                {
                    hosts.Add(rh);
                }

                allActors = Deployment.ReachActors(hosts, system);
            }
            
            int number = 0;
            foreach (var a in thisHostActors)
            {
                a.Tell(new Messages.InitServer(allActors.AsReadOnly(), algConfig, hosts.AsReadOnly(), number, currentHost));
                number++;
            }

            int msgI = 0;

            system.Scheduler.Advanced.ScheduleRepeatedly(TimeSpan.FromSeconds(1),
                    TimeSpan.FromMilliseconds(4000),
                    () =>
                    {
                        foreach (var a in thisHostActors)
                        {
                            a.Tell(new Messages.BroadcastAtomically($"[{hostname}:{port}-{a.Path.Name}]:M{msgI}"));
                        }
                        msgI++;
                    });

            Console.ReadKey();
            system.Dispose();
        }

        static ActorSystem CreateActorSystem(string hostname, string port)
        {
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
                            heartbeat-interval = 4 s
                        }
                    }
                }
            ");

            ActorSystem actorSystem = ActorSystem.Create(SystemName, systemConfig);

            return actorSystem;
        }
        static List<IActorRef> CreateActors(ActorSystem system, int n)
        {
            int count = n;

            List<IActorRef> actors = new List<IActorRef>();

            for (int i = 0; i < count; i++)
            {
                IActorRef newActor = system.ActorOf<ServerActor>($"svr{i}");
                actors.Add(newActor);
            }

            return actors;
        }


    }
}
