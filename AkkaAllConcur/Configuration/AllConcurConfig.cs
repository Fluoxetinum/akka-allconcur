using System;
using System.Collections.Generic;

namespace AkkaAllConcur.Configuration
{
    class AllConcurConfig
    {
        public enum OverlayGraphType
        {
            BINOMIAL, GS
        }
        public OverlayGraphType OverlayGraph { get; private set; }

        public enum AlgorithmVersionType
        {
            BASIC,
            PLUS,
            PLUS_AND_UNIFORM
        }
        public AlgorithmVersionType AlgorithmVersion { get; private set; }

        public enum OutputVerbosityType
        {
            TIME_ONLY,
            INFO,
            DEBUG
        }
        public OutputVerbosityType OutputVerbosity { get; private set; }

        public AllConcurConfig( OverlayGraphType gt, AlgorithmVersionType av, OutputVerbosityType ov)
        {
            OverlayGraph = gt;
            AlgorithmVersion = av;
            OutputVerbosity = ov;
        }
    }
}
