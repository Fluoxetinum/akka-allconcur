using System;
using System.Collections.Generic;

namespace AkkaAllConcur.Configuration
{
    public class AllConcurConfig
    {
        public enum OverlayGraphType
        {
            BINOMIAL, GS
        }
        public OverlayGraphType OverlayGraph { get; private set; }

        public enum OutputVerbosityType
        {
            TIME_ONLY,
            INFO,
            DEBUG
        }
        public OutputVerbosityType OutputVerbosity { get; private set; }

        public AllConcurConfig( OverlayGraphType gt, OutputVerbosityType ov)
        {
            OverlayGraph = gt;
            OutputVerbosity = ov;
        }
    }
}
