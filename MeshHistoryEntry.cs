using System;
using Rhino.Geometry;

namespace RhinoM8
{
    public class MeshHistoryEntry
    {
        public string Prompt { get; set; }
        public string Provider { get; set; }
        public DateTime Timestamp { get; set; }
        public string FilePath { get; set; }
        public string ThumbnailPath { get; set; }

        public MeshHistoryEntry(string prompt, string filePath, string provider, string thumbnailPath = null)
        {
            Prompt = prompt;
            FilePath = filePath;
            Provider = provider;
            Timestamp = DateTime.Now;
            ThumbnailPath = thumbnailPath;
        }
    }
}