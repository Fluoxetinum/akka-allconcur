using System;
using System.Collections.Generic;
using System.Text;

namespace AkkaAllConcur.Configuration
{
    class ProgramConfig
    {
        public AllConcurConfig Algorithm { get; private set; }
        public HostsConfig Hosts { get; private set; }
        public ProgramConfig(AllConcurConfig a, HostsConfig h)
        {
            Algorithm = a;
            Hosts = h;
        }
    }
}
