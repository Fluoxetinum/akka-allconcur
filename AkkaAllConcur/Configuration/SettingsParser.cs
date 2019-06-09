using System.Xml;
using System.IO;

namespace AkkaAllConcur.Configuration
{
    static class SettingsParser
    {
        static string[] algorithmConfigPath = { "Settings", "algorithm.xml" };

        public static AllConcurConfig ParseAlgConfig()
        {
            string platformSpecificPath = Path.Combine(algorithmConfigPath);

            AllConcurConfig.OverlayGraphType gt = AllConcurConfig.OverlayGraphType.BINOMIAL;
            AllConcurConfig.OutputVerbosityType ov = AllConcurConfig.OutputVerbosityType.TIME_ONLY;

            using (XmlReader r = XmlReader.Create(platformSpecificPath,
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

                r.ReadStartElement("verbosity");
                string v = r.ReadContentAsString();
                if (v == "debug")
                {
                    ov = AllConcurConfig.OutputVerbosityType.DEBUG;
                }
                else if (v == "info")
                {
                    ov = AllConcurConfig.OutputVerbosityType.INFO;
                }
                r.ReadEndElement();

                r.ReadEndElement();
            }

            AllConcurConfig config = new AllConcurConfig(gt, ov);
            return config;
        }
    }
}
