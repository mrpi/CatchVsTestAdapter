using System;
using System.Xml.Linq;

namespace CatchVsTestAdapter
{
    public class SourceLocation
    {
        public SourceLocation(string file, uint line)
        {
            File = file;
            Line = line;
        }

        public static SourceLocation FromXElement(XElement el)
        {
            try
            {
                var file = el.Attribute("filename").Value;
                var line = uint.Parse(el.Attribute("line").Value);
                return new SourceLocation(file, line);
            }
            catch (Exception)
            {
                return new SourceLocation("unknown file", 0);
            }
        }

        public string File { get; private set; }
        public uint Line { get; private set; }

        public override string ToString()
        {
            return System.IO.Path.GetFileName(File) + "(" + Line + ")";
        }

        public string ToStacktraceString(string type)
        {
            if (Line == 0)
                return "at " + type;
            else
                return "at " + type + " in " + File + ":line " + Line;
        }
    }
}
