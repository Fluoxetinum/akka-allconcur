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

        public enum FailureDetectorType {
            PERFECT, EVENTUALLY_PERFECT
        }
        public FailureDetectorType FailureDetector { get; private set; }

        public enum AlgorithmVersionType
        {
            BASIC,
            PLUS,
            PLUS_AND_UNIFORM
        }
        public AlgorithmVersionType AlgorithmVersion { get; private set; }

        public AllConcurConfig( OverlayGraphType gt, FailureDetectorType fd, AlgorithmVersionType av)
        {
            OverlayGraph = gt;
            FailureDetector = fd;
            AlgorithmVersion = av;
        }
    }
}
