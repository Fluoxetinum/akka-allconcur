﻿using System;
using System.Collections.ObjectModel;
using System.Text;
using Akka.Actor;
using System.Diagnostics;
namespace AkkaAllConcur
{
    class LogActor : ReceiveActor
    {
        Stopwatch sw;

        public LogActor()
        {
            sw = new Stopwatch();
            sw.Start();

            Receive<Messages.LogABcastVerbose>((m) => {

                long time = m.RoundTime;
                long mcs = time / 1000;
                long ns = time % 1000;
                long ms = mcs / 1000;
                mcs %= 1000;

                StringBuilder output = new StringBuilder();
                output.AppendLine(new string('-', 80));
                var msgs = m.ADeliveredMsgs;
                output.AppendLine($"Round {m.Stage}, Actor '{Sender.Path}' " +
                    $"A-Delivered {msgs.Count} msgs in [{ms}ms{mcs}mcs{ns}ns] : ");

                for (int i = 0; i < msgs.Count; i++)
                {
                    var inner_msgs = msgs[i].Message.Value as ReadOnlyCollection<Messages.Abroadcast>;
                    if (inner_msgs == null)
                    {
                        output.Append(msgs[i].Message);
                    }
                    else
                    {
                        for (int j = 0; j < inner_msgs.Count; j++)
                        {
                            output.Append(inner_msgs[j]);
                            output.Append((j != inner_msgs.Count - 1) ? "," : "");
                        }
                    }

                    output.Append((i != msgs.Count - 1) ? " -> " : "#");
                }
                Console.WriteLine(output);
            });
            Receive<Messages.LogAbcast>((m) => {
                Console.WriteLine($"{m.Stage} {m.RoundTime} {m.ActorsNumber} {m.Throughput} {sw.ElapsedMilliseconds}ms");
            });

            Receive<Messages.LogGraph>((m) => {
                Console.WriteLine(graphString(m.Graph));
            });
            Receive<Messages.LogTrackGraph>((m) => {
                StringBuilder output = new StringBuilder();
                output.AppendLine(new string('+', 80));
                output.AppendLine($"Tracking graphs of the actor '{Sender.Path}' : ");
                foreach (var g in m.Graphs)
                {
                    IActorRef a = g.Key;
                    output.Append($"Message from {a.ToShortString()} graph: \n");
                    output.Append(graphString(g.Value));
                }
                output.Append("\n");
                Console.WriteLine(output);
            });
            Receive<Messages.LogFailureVerbose>((m) => {
                StringBuilder output = new StringBuilder();
                output.AppendLine(failureString(m.Notification) + $"::{Sender}");
                foreach (var pair in m.FailureNotifications)
                {
                    output.Append($"|'{pair.V1.ToShortString()} crashed' - {pair.V2.ToShortString()} said.|\n");
                }
                Console.WriteLine(output);
            });
            Receive<Messages.LogFailure>((m) => {
                Console.WriteLine(failureString(m.Notification) + $"::{Sender}");
            });
            Receive<Messages.LogMessage>((m) => {
                Console.WriteLine(m.Message);
            });
        }

        private string failureString(VertexPair notification)
        {
            return $"XX> Failure notification about {notification.V1.ToShortString()} " +
                $"from {notification.V2.ToShortString()} was r-delivered <XX";
        }

        private string graphString(ReadOnlyDictionary<IActorRef, ReadOnlyCollection<IActorRef>> graph)
        {
            StringBuilder output = new StringBuilder();
            foreach (var pair in graph)
            {
                IActorRef a = pair.Key;
                output.Append($"{a.ToShortString()} => ");
                output.Append("[ ");
                foreach (var v in pair.Value)
                {
                    output.Append(v.ToShortString() + " ");
                }
                output.Append("]\n");
            }

            return output.ToString();
        }
    }
}
