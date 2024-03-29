﻿using System.Collections.ObjectModel;

namespace AkkaAllConcur.Messages
{
    class LogAbcast
    {
        public int Stage { get; private set; }
        public long RoundTime { get; private set; }
        public int ActorsNumber { get; private set; }
        public int Throughput { get; private set; }
        public LogAbcast(int s, long r, int an, int t) :
            this(s, r, an)
        {
            Throughput = t;
        }
        public LogAbcast(int s, long r, int an)
        {
            Stage = s;
            RoundTime = r;
            ActorsNumber = an;
        }
    }
    class LogABcastVerbose : LogAbcast
    {
        public ReadOnlyCollection<Messages.Rbroadcast> ADeliveredMsgs { get; private set; }

        public LogABcastVerbose(ReadOnlyCollection<Messages.Rbroadcast> am, long r, int an) : 
            base(am[0].Round, r, an)
        {
            ADeliveredMsgs = am;
        }
    }
}
