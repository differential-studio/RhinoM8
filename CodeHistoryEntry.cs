using System;

namespace RhinoM8
{
    public class CodeHistoryEntry
    {
        public string Prompt { get; set; }
        public string Code { get; set; }
        public DateTime Timestamp { get; set; }
        public string Provider { get; set; }
        public string GeometryDescription { get; set; }

        public CodeHistoryEntry(string prompt, string code, string provider)
        {
            Prompt = prompt;
            Code = code;
            Provider = provider;
            Timestamp = DateTime.Now;
            GeometryDescription = "";
        }
    }
} 