using System;
using System.Collections.Generic;
using System.Text;

namespace AkkaAllConcur
{
    class AllConcurConfig
    {
        public struct Host
        {
            public string HostName { get; set; }
            public string Port { get; set; }
        }
        private List<Host> hosts;
        public int ThisHostIndex {get; set;}
        public enum OverlayGraphType
        {
            BINOMIAL, GS
        }
        public OverlayGraphType OverlayGraph { get; set; }

        public enum FailureDetectorType {
            PERFECT, EVENTUALLY_PERFECT
        }
        public FailureDetectorType FailureDetector { get; set; }

        public int N { get; set; }

        public enum AlgorithmVersionType
        {
            BASIC,
            PLUS,
            PLUS_AND_UNIFORM
        }

        public AlgorithmVersionType AlgorithmVersion { get; set; }

        public AllConcurConfig()
        {
            hosts = new List<Host>();
            ThisHostIndex = 0;
            N = 9;
            OverlayGraph = OverlayGraphType.BINOMIAL;
            FailureDetector = FailureDetectorType.PERFECT;
            AlgorithmVersion = AlgorithmVersionType.BASIC;
        }

        public void AddHost(string h, string p)
        {
            hosts.Add(new Host { HostName = h, Port = p });
        }
        public int Machines
        {
            get
            {
                return hosts.Count;
            }
        }

        public int getHostActorCount(int i)
        {
            int count = N / Machines;
            if (i == hosts.Count - 1)
            {
                count += N % Machines;
            }
            return count;
        }

        public int getThisHostActorCount()
        {
            return getHostActorCount(ThisHostIndex);
        }

        public Host getHost(int i)
        {
            return hosts[i];
        }

        public Host getThisHost()
        {
            return getHost(ThisHostIndex);
        }
    }
}
