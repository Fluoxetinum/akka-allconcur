using System;
using System.Collections.ObjectModel;
using System.Text;

namespace AkkaAllConcur.Messages
{
    class LogFailure
    {
        public VertexPair Notification { get; private set; }
        public LogFailure(VertexPair n)
        {
            Notification = n;
        }
    }
    class LogFailureVerbose : LogFailure
    {
        public ReadOnlyCollection<VertexPair> FailureNotifications { get; private set; }
        public LogFailureVerbose(VertexPair n, ReadOnlyCollection<VertexPair> fn) :
            base(n)
        {
            FailureNotifications = fn;
        }
    }
}
