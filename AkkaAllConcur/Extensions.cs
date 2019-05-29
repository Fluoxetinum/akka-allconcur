using System;
using System.Collections.Generic;
using System.Text;
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
    }
}
