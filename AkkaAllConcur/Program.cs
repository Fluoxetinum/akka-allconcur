using System;
using System.Xml;
using System.Collections.Generic;
using Akka.Actor;
using Akka.Configuration;

namespace AkkaAllConcur
{
    class Program
    {
        static string ConfigPath = "StartUpConfig.xml";
        static string SystemName = "AllConcurSystem";

        static void Main(string[] args)
        {

            AllConcurConfig config = ParseConfig(ConfigPath);

            if (args.Length != 0)
            {
                config.ThisHostIndex = Convert.ToInt32(args[0]);
            }

            ActorSystem system = CreateActorSystem(config);

            List<IActorRef> thisHostActors = CreateActors(config, system);

            List<IActorRef> allActors = ReachActors(config, system);

            Dictionary<IActorRef, List<IActorRef>> reliableOverlayGraph = GraphCreator.ComputeAllReliableSuccessors(config, allActors);

            Dictionary<IActorRef, IActorRef> unreliableOverlayGraph = GraphCreator.ComputeAllUnreliableSucessors(allActors);            

            Console.ReadKey();

            system.Dispose();
        }

        static AllConcurConfig ParseConfig(string path)
        {
            AllConcurConfig config = new AllConcurConfig();

            using (XmlReader r = XmlReader.Create(ConfigPath,
                new XmlReaderSettings { IgnoreWhitespace = true, IgnoreComments = true }))
            {
                r.MoveToContent();
                r.ReadStartElement("config");


                r.ReadStartElement("hosts");
                while (true)
                {
                    try
                    {
                        r.ReadStartElement("host");
                    }
                    catch (XmlException)
                    {
                        break;
                    }

                    r.ReadStartElement("hostname");
                    string host = r.ReadContentAsString();
                    r.ReadEndElement();
                    r.ReadStartElement("port");
                    string port = r.ReadContentAsString();
                    r.ReadEndElement();

                    r.ReadEndElement();

                    config.AddHost(host, port);
                }
                r.ReadEndElement();

                r.ReadStartElement("n");
                config.N = r.ReadContentAsInt();
                r.ReadEndElement();

                r.ReadStartElement("graph");
                string g = r.ReadContentAsString();
                if (g == "gs")
                {
                    config.OverlayGraph = AllConcurConfig.OverlayGraphType.GS;
                }
                r.ReadEndElement();

                r.ReadStartElement("epfd");
                bool ep = r.ReadContentAsBoolean();
                if (ep)
                {
                    config.FailureDetector = AllConcurConfig.FailureDetectorType.EVENTUALLY_PERFECT;
                }
                r.ReadEndElement();

                r.ReadStartElement("version");
                string v = r.ReadContentAsString();
                if (v == "allconcur-plus")
                {
                    config.AlgorithmVersion = AllConcurConfig.AlgorithmVersionType.PLUS;
                }
                else if (v == "allconcur-plus-uniform")
                {
                    config.AlgorithmVersion = AllConcurConfig.AlgorithmVersionType.PLUS_AND_UNIFORM;
                }
                r.ReadEndElement();

                r.ReadEndElement();
            }
            return config;
        }
        static ActorSystem CreateActorSystem(AllConcurConfig algorithmConfig)
        {
            var host = algorithmConfig.getThisHost();

            Config systemConfig = ConfigurationFactory.ParseString(@"
                akka {
                    actor {
                        provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                    }
                    remote {
                        dot-netty.tcp {
                            hostname = " + host.HostName + @"
                            port = " + host.Port + @"
                        }
                    }
                }
            ");

            ActorSystem actorSystem = ActorSystem.Create(SystemName, systemConfig);

            return actorSystem;
        }

        static List<IActorRef> CreateActors(AllConcurConfig algorithmConfig, ActorSystem system)
        {
            int count = algorithmConfig.getThisHostActorCount();
            int machineIndex = algorithmConfig.ThisHostIndex;

            List<IActorRef> actors = new List<IActorRef>();

            for (int i = 0; i < count; i++)
            {
                IActorRef newActor = system.ActorOf<ServerActor>($"Server{machineIndex}-{i}");
                actors.Add(newActor);
            }

            return actors;
        }

        static List<IActorRef> ReachActors(AllConcurConfig algorithmConfig, ActorSystem system)
        {
            Console.Write("Trying to connect to all actors in the system...");

            List<IActorRef> actors = new List<IActorRef>();

            int M = algorithmConfig.Machines;
        
            for (int i = 0; i < M; i++)
            {
                int count = algorithmConfig.getHostActorCount(i);
                var host = algorithmConfig.getHost(i);

                for (int j = 0; j < count; j++)
                {
                    ActorSelection a = system.ActorSelection($"akka.tcp://{SystemName}@{host.HostName}:{host.Port}/user/Server{i}-{j}");
                    var t = a.ResolveOne(TimeSpan.FromMinutes(5));
                    t.Wait();
                    actors.Add(t.Result);
                }
            }

            Console.WriteLine("OK");

            return actors;
        }
    }
}
