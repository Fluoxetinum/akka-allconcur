using System;
using System.Collections.Generic;
using System.Text;

namespace AkkaAllConcur.Messages
{
    class LogMessage
    {
        public string Message { get; private set; }

        public LogMessage(string m)
        {
            Message = m;
        }
    }
}
