using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System.Xml.Linq;

namespace CatchVsTestAdapter
{
    [FileExtension(".exe")]
    [DefaultExecutorUri(CatchTestExecutor.ExecutorUriString)]
    public class CatchTestDiscoverer : ITestDiscoverer
    {
        static private IMessageLogger currentLogger;

        /// <summary>
        /// Finds tests in Catch unit test binaries. Note: We have to run the binary to enumerate tests.
        /// </summary>
        /// <param name="sources">Binaries to search for tests.</param>
        public void DiscoverTests(IEnumerable<string> sources, IDiscoveryContext context, IMessageLogger logger, ITestCaseDiscoverySink discoverySink)
        {
            currentLogger = logger;

            foreach (var source in sources)
            {
                switch (checkCatchTestBinaryFormat(source))
                {
                    case CatchBinaryFormat.NoCatchTestBinary:
                        continue;
                    case CatchBinaryFormat.CatchTestBinary:
                        foreach (var testCase in ListTestsInBinary(source))
                        {
                            discoverySink.SendTestCase(testCase);
                        }
                        continue;
                    case CatchBinaryFormat.CatchTestBinaryWithXmlTestList:
                        foreach (var testCase in ListTestsInBinaryXml(source))
                        {
                            discoverySink.SendTestCase(testCase);
                        }
                        break;
                }
            }
        }

        internal enum CatchBinaryFormat
        {
            NoCatchTestBinary,
            CatchTestBinary,
            CatchTestBinaryWithXmlTestList
        }

        internal static CatchBinaryFormat checkCatchTestBinaryFormat(string source)
        {
            // This is a dirty hack to detect whether a binary is a catch test.
            // We really need to know since to detect tests, the binary needs to be run.
            var fileContents = File.ReadAllText(source);

            if (fileContents.Contains("--list-tests") && fileContents.Contains("--list-tags"))
            {
                if (fileContents.Contains("CatchTestList"))
                    return CatchBinaryFormat.CatchTestBinaryWithXmlTestList;

                return CatchBinaryFormat.CatchTestBinary;
            }

            return CatchBinaryFormat.NoCatchTestBinary;
        }

        internal static IEnumerable<TestCase> ListTestsInBinaries(IEnumerable<string> sources)
        {
            var tests = new List<TestCase>();

            foreach (var source in sources)
            {
                tests.AddRange(ListTestsInBinary(source));
            }

            return tests;
        }
        internal static IEnumerable<TestCase> ListTestsInBinaryXml(string source)
        {
            currentLogger.SendMessage(TestMessageLevel.Informational, "Parsing tests from " + source);

            var tests = new List<TestCase>();

            var listOutput = Utility.runExe(source, "--list-tests", "-r", "xml");
            currentLogger.SendMessage(TestMessageLevel.Informational, "Tests executed");

            var xdoc = XDocument.Parse(listOutput);
            currentLogger.SendMessage(TestMessageLevel.Informational, "Xml parsed");

            foreach (var xTest in xdoc.Element("CatchTestList").Elements("TestCase"))
            {
                currentLogger.SendMessage(TestMessageLevel.Informational, "Reading test");

                var name = xTest.Attribute("name").Value;
                String fullQualifiedName = name;
                currentLogger.SendMessage(TestMessageLevel.Informational, "Processing test " + fullQualifiedName);

                var test = new TestCase(fullQualifiedName, CatchTestExecutor.ExecutorUri, source);
                test.LineNumber = Int32.Parse(xTest.Element("Source").Attribute("line").Value);
                test.CodeFilePath = xTest.Element("Source").Attribute("filename").Value;

                tests.Add(test);
            }

            return tests;
        }

        internal static IEnumerable<TestCase> ListTestsInBinary(string source)
        {
            var tests = new List<TestCase>();

            var listOutput = Utility.runExe(source, "--list-tests");

            // Match a test case out of the output.
            const string regexStr = @"\r?\n[ ]{2}(?<name>[^\r\n]*)(?:\r?\n[ ]{4}(?<name>[^ ][^\r\n]*))*(?:\r?\n[ ]{6}(?<tag>\s*\[[^\r\n]*\])*)?";

            foreach (Match match in Regex.Matches(listOutput, regexStr))
            {
                IEnumerable<string> nameLines = match.Groups["name"].Captures.OfType<Capture>().Select(x => x.Value).ToList();
                var fullyQuallifiedName = (nameLines.Count() == 1) ? nameLines.First() : nameLines.First() + "*";
                var test = new TestCase(fullyQuallifiedName, CatchTestExecutor.ExecutorUri, source)
                {
                    DisplayName = nameLines.Aggregate((x, y) => x + " " + y)
                };

                // Add test tags as traits.
                if (test.GetType().GetProperty("Traits") != null) //< Don't populate traits on older versions of VS.
                {
                    foreach (Capture tag in match.Groups["tag"].Captures)
                    {
                        test.Traits.Add("Tags", tag.Value);
                    }
                }

                tests.Add(test);
            }

            return tests;
        }
    }
}
