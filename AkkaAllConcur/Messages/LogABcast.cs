using System.Collections.ObjectModel;

namespace AkkaAllConcur.Messages
{
    class LogAbcast
    {
        public int Stage { get; private set; }
        public long RoundTime { get; private set; }
        public LogAbcast(int s, long r)
        {
            Stage = s;
            RoundTime = r;
        }
    }
    class LogABcastVerbose : LogAbcast
    {
        public ReadOnlyCollection<Messages.BroadcastReliably> ADeliveredMsgs { get; private set; }

        public LogABcastVerbose(ReadOnlyCollection<Messages.BroadcastReliably> am, long r) : 
            base(am[0].Stage, r)
        {
            ADeliveredMsgs = am;
        }
    }
}
