using System.Diagnostics;
using System.Text.RegularExpressions;
using Akka.Actor;

namespace AkkaAllConcur
{
    public static class Extensions
    {
        public static string ToShortString(this IActorRef a)
        {
            string root = a.Path.Root.ToString();
            if (Regex.Match(root, "akka:+").Success)
            {
                return a.Path.Name; 
            }
            else
            {
                string[] remoteInfo = root.Split(':', '/');
                string port = remoteInfo[remoteInfo.Length - 2];
                return port + ":" + a.Path.Name;
            }
        }

        public static long ElapsedNanoSeconds(this Stopwatch watch)
        {
            return (long)((double)watch.ElapsedTicks / Stopwatch.Frequency * 1000000000.0 );
        }
        public static long ElapsedMicroSeconds(this Stopwatch watch)
        {
            return (long)((double)watch.ElapsedTicks / Stopwatch.Frequency * 1000000.0 );
        }
    }
}
