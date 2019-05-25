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
        static string SystemName = "AllConcurSystem";      

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

            HostsConfig hostsConfig;
            List<IActorRef> allActors;
            List<Host> hosts = new List<Host>();
            
            if (!attach) // first machine in a system
            {
                hosts.Add(currentHost);
                hostsConfig = new HostsConfig(hosts);
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
                    remoteActor.Ask<Messages.MembershipResponse>(new Messages.MembershipRequest(n)).Result;

                foreach (var rh in m.Hosts)
                {
                    hosts.Add(rh);
                }

                hosts.Add(currentHost);
                hostsConfig = new HostsConfig(hosts);

                allActors = ReachActors(hostsConfig, system);
            }

            ProgramConfig pConfig = new ProgramConfig(algConfig, hostsConfig);

            foreach (var a in thisHostActors)
            {
                a.Tell(new Messages.InitServer(allActors.AsReadOnly(), pConfig));
            }

            int msgI = 0;

            system.Scheduler.Advanced.ScheduleRepeatedly(TimeSpan.FromSeconds(1),
                    TimeSpan.FromMilliseconds(5000),
                    () =>
                    {
                        foreach (var a in thisHostActors)
                        {
                            a.Tell(new Messages.BroadcastAtomically($"[{a.Path.Name}]:m{msgI}"));
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
        static List<IActorRef> ReachActors(HostsConfig hostsConfig, ActorSystem system)
        {
            List<IActorRef> actors = new List<IActorRef>();

            List<Host> allHosts = hostsConfig.Hosts;

            foreach(var h in allHosts)
            {
                int count = h.ActorsNumber;

                for (int i = 0; i < count; i++)
                {
                    string hostName = h.HostName;
                    string port = h.Port;

                    ActorSelection a = system.ActorSelection($"akka.tcp://{SystemName}@{hostName}:{port}/user/svr{i}");
                    var t = a.ResolveOne(TimeSpan.FromMinutes(5));
                    t.Wait();
                    actors.Add(t.Result);
                }

            }

            return actors;
        }

    }
}
