using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace AkkaAllConcur.Configuration
{
    static class SettingsParser
    {
        static string algorithmConfigPath = "Settings\\algorithm.xml";

        public static AllConcurConfig ParseAlgConfig()
        {

            AllConcurConfig.OverlayGraphType gt = AllConcurConfig.OverlayGraphType.BINOMIAL;
            AllConcurConfig.FailureDetectorType fd = AllConcurConfig.FailureDetectorType.PERFECT;
            AllConcurConfig.AlgorithmVersionType av = AllConcurConfig.AlgorithmVersionType.BASIC;

            using (XmlReader r = XmlReader.Create(algorithmConfigPath,
                new XmlReaderSettings { IgnoreWhitespace = true, IgnoreComments = true }))
            {
                r.MoveToContent();
                r.ReadStartElement("config");

                r.ReadStartElement("graph");
                string g = r.ReadContentAsString();
                if (g == "gs")
                {
                    gt = AllConcurConfig.OverlayGraphType.GS;
                }
                r.ReadEndElement();

                r.ReadStartElement("epfd");
                bool ep = r.ReadContentAsBoolean();
                if (ep)
                {
                    fd = AllConcurConfig.FailureDetectorType.EVENTUALLY_PERFECT;
                }
                r.ReadEndElement();

                r.ReadStartElement("version");
                string v = r.ReadContentAsString();
                if (v == "allconcur-plus")
                {
                    av = AllConcurConfig.AlgorithmVersionType.PLUS;
                }
                else if (v == "allconcur-plus-uniform")
                {
                    av = AllConcurConfig.AlgorithmVersionType.PLUS_AND_UNIFORM;
                }
                r.ReadEndElement();
                r.ReadEndElement();
            }

            AllConcurConfig config = new AllConcurConfig(gt, fd, av);

            return config;
        }
    }
}
