using Rhino;
using Rhino.PlugIns;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Rhino.Geometry;
using System.Linq;
using System.Windows.Forms;

namespace RhinoM8
{
    ///<summary>
    /// <para>Every RhinoCommon .rhp assembly must have one and only one PlugIn-derived
    /// class. DO NOT create instances of this class yourself. It is the
    /// responsibility of Rhino to create an instance of this class.</para>
    /// <para>To complete plug-in information, please also see all PlugInDescription
    /// attributes in AssemblyInfo.cs (you might need to click "Project" ->
    /// "Show All Files" to see it in the "Solution Explorer" window).</para>
    ///</summary>
    public class RhinoM8Plugin : Rhino.PlugIns.PlugIn
    {
        private PersistentPromptForm _promptForm;
        private const string SETTINGS_KEY = "RhinoM8";
        private const string CODE_HISTORY_KEY = "RhinoM8_CodeHistory";
        private const string MESH_HISTORY_KEY = "RhinoM8_MeshHistory";
        private const int MAX_HISTORY_ENTRIES = 1000;
        
        private List<CodeHistoryEntry> _codeHistory;
        private List<MeshHistoryEntry> _meshHistory;

        public RhinoM8Plugin()
        {
            Instance = this;
            _codeHistory = LoadCodeHistoryFromSettings();
            _meshHistory = LoadMeshHistoryFromSettings();
        }

        ///<summary>Gets the only instance of the RhinoM8Plugin plug-in.</summary>
        public static RhinoM8Plugin Instance { get; private set; }

        private List<CodeHistoryEntry> LoadCodeHistoryFromSettings()
        {
            var historyJson = Settings.GetString(CODE_HISTORY_KEY, "[]");
            try
            {
                return JsonConvert.DeserializeObject<List<CodeHistoryEntry>>(historyJson) ?? new List<CodeHistoryEntry>();
            }
            catch
            {
                return new List<CodeHistoryEntry>();
            }
        }

        private List<MeshHistoryEntry> LoadMeshHistoryFromSettings()
        {
            var historyJson = Settings.GetString(MESH_HISTORY_KEY, "[]");
            try
            {
                return JsonConvert.DeserializeObject<List<MeshHistoryEntry>>(historyJson) ?? new List<MeshHistoryEntry>();
            }
            catch
            {
                return new List<MeshHistoryEntry>();
            }
        }

        private void SaveHistoryToSettings()
        {
            var codeHistoryJson = JsonConvert.SerializeObject(_codeHistory);
            Settings.SetString(CODE_HISTORY_KEY, codeHistoryJson);

            var meshHistoryJson = JsonConvert.SerializeObject(_meshHistory);
            Settings.SetString(MESH_HISTORY_KEY, meshHistoryJson);
        }

        public void AddToHistory(object entry)
        {
            if (entry is CodeHistoryEntry codeEntry)
            {
                _codeHistory.Insert(0, codeEntry);
                if (_codeHistory.Count > MAX_HISTORY_ENTRIES)
                    _codeHistory.RemoveRange(MAX_HISTORY_ENTRIES, _codeHistory.Count - MAX_HISTORY_ENTRIES);
            }
            else if (entry is MeshHistoryEntry meshEntry)
            {
                _meshHistory.Insert(0, meshEntry);
                if (_meshHistory.Count > MAX_HISTORY_ENTRIES)
                    _meshHistory.RemoveRange(MAX_HISTORY_ENTRIES, _meshHistory.Count - MAX_HISTORY_ENTRIES);
            }
            
            SaveHistoryToSettings();
        }

        public void RemoveFromHistory(object entry)
        {
            if (entry is CodeHistoryEntry codeEntry)
            {
                _codeHistory.Remove(codeEntry);
            }
            else if (entry is MeshHistoryEntry meshEntry)
            {
                // Delete the GLB file if it exists
                if (File.Exists(meshEntry.FilePath))
                {
                    try
                    {
                        File.Delete(meshEntry.FilePath);
                    }
                    catch (Exception ex)
                    {
                        RhinoApp.WriteLine($"Warning: Could not delete file {meshEntry.FilePath}: {ex.Message}");
                    }
                }
                _meshHistory.Remove(meshEntry);
            }
            SaveHistoryToSettings();
        }

        public IEnumerable<object> GetAllHistory()
        {
            var allHistory = new List<object>();
            allHistory.AddRange(_codeHistory);
            allHistory.AddRange(_meshHistory);
            return allHistory.OrderByDescending(h => 
                h is CodeHistoryEntry c ? c.Timestamp :
                h is MeshHistoryEntry m ? m.Timestamp : DateTime.MinValue);
        }

        // Helper methods for mesh serialization
        public static byte[] SerializeMesh(Rhino.Geometry.Mesh mesh)
        {
            try
            {
                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    // Write version number for future compatibility
                    writer.Write((int)1);

                    // Write vertices
                    writer.Write(mesh.Vertices.Count);
                    foreach (var vertex in mesh.Vertices)
                    {
                        writer.Write(vertex.X);
                        writer.Write(vertex.Y);
                        writer.Write(vertex.Z);
                    }

                    // Write vertex normals if they exist
                    var hasNormals = mesh.Normals.Count == mesh.Vertices.Count;
                    writer.Write(hasNormals);
                    if (hasNormals)
                    {
                        foreach (var normal in mesh.Normals)
                        {
                            writer.Write(normal.X);
                            writer.Write(normal.Y);
                            writer.Write(normal.Z);
                        }
                    }

                    // Write faces
                    writer.Write(mesh.Faces.Count);
                    foreach (var face in mesh.Faces)
                    {
                        writer.Write(face.IsTriangle);
                        writer.Write(face.A);
                        writer.Write(face.B);
                        writer.Write(face.C);
                        if (!face.IsTriangle)
                            writer.Write(face.D);
                    }

                    // Write texture coordinates if they exist
                    var hasTexCoords = mesh.TextureCoordinates.Count > 0;
                    writer.Write(hasTexCoords);
                    if (hasTexCoords)
                    {
                        writer.Write(mesh.TextureCoordinates.Count);
                        foreach (var tc in mesh.TextureCoordinates)
                        {
                            writer.Write(tc.X);
                            writer.Write(tc.Y);
                        }
                    }

                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error serializing mesh: {ex.Message}");
            }
        }

        public static Rhino.Geometry.Mesh DeserializeMesh(byte[] data)
        {
            try
            {
                using (var ms = new MemoryStream(data))
                using (var reader = new BinaryReader(ms))
                {
                    var mesh = new Rhino.Geometry.Mesh();

                    // Read version number
                    var version = reader.ReadInt32();
                    if (version != 1)
                        throw new Exception($"Unsupported mesh version: {version}");

                    // Read vertices
                    int vertexCount = reader.ReadInt32();
                    if (vertexCount <= 0 || vertexCount > 1000000)  // Sanity check
                        throw new Exception($"Invalid vertex count: {vertexCount}");

                    for (int i = 0; i < vertexCount; i++)
                    {
                        double x = reader.ReadDouble();
                        double y = reader.ReadDouble();
                        double z = reader.ReadDouble();
                        mesh.Vertices.Add(x, y, z);
                    }

                    // Read normals if they exist
                    bool hasNormals = reader.ReadBoolean();
                    if (hasNormals)
                    {
                        mesh.Normals.Count = vertexCount;
                        for (int i = 0; i < vertexCount; i++)
                        {
                            double x = reader.ReadDouble();
                            double y = reader.ReadDouble();
                            double z = reader.ReadDouble();
                            mesh.Normals[i] = new Rhino.Geometry.Vector3f((float)x, (float)y, (float)z);
                        }
                    }

                    // Read faces
                    int faceCount = reader.ReadInt32();
                    if (faceCount <= 0 || faceCount > 1000000)  // Sanity check
                        throw new Exception($"Invalid face count: {faceCount}");

                    for (int i = 0; i < faceCount; i++)
                    {
                        bool isTriangle = reader.ReadBoolean();
                        int a = reader.ReadInt32();
                        int b = reader.ReadInt32();
                        int c = reader.ReadInt32();
                        if (isTriangle)
                            mesh.Faces.AddFace(a, b, c);
                        else
                        {
                            int d = reader.ReadInt32();
                            mesh.Faces.AddFace(a, b, c, d);
                        }
                    }

                    // Read texture coordinates if they exist
                    bool hasTexCoords = reader.ReadBoolean();
                    if (hasTexCoords)
                    {
                        int tcCount = reader.ReadInt32();
                        for (int i = 0; i < tcCount; i++)
                        {
                            double x = reader.ReadDouble();
                            double y = reader.ReadDouble();
                            mesh.TextureCoordinates.Add(x, y);
                        }
                    }

                    if (!hasNormals)
                        mesh.Normals.ComputeNormals();

                    mesh.Compact();
                    if (!mesh.IsValid)
                        throw new Exception("Resulting mesh is invalid");

                    return mesh;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error deserializing mesh: {ex.Message}");
            }
        }

        // Add method to save settings
        public void SaveSettings(LLMSettingsForm.LLMSettings settings)
        {
            // Save each setting
            Settings.SetString(SETTINGS_KEY + "OpenAIKey", settings.OpenAIKey);
            Settings.SetString(SETTINGS_KEY + "ClaudeKey", settings.ClaudeKey);
            Settings.SetDouble(SETTINGS_KEY + "Temperature", settings.Temperature);
            Settings.SetInteger(SETTINGS_KEY + "MaxTokens", settings.MaxTokens);
            Settings.SetString(SETTINGS_KEY + "SystemPrompt", settings.SystemPrompt);
            Settings.SetString(SETTINGS_KEY + "GrokKey", settings.GrokKey);
            Settings.SetString(SETTINGS_KEY + "MeshyKey", settings.MeshyKey);
        }

        // Add method to load settings
        public LLMSettingsForm.LLMSettings LoadSettings()
        {
            return new LLMSettingsForm.LLMSettings
            {
                OpenAIKey = Settings.GetString(SETTINGS_KEY + "OpenAIKey", ""),
                ClaudeKey = Settings.GetString(SETTINGS_KEY + "ClaudeKey", ""),
                Temperature = Settings.GetDouble(SETTINGS_KEY + "Temperature", 0.1),
                MaxTokens = Settings.GetInteger(SETTINGS_KEY + "MaxTokens", 2000),
                SystemPrompt = Settings.GetString(SETTINGS_KEY + "SystemPrompt", 
                    "You are a helpful assistant that generates Python code for Rhino 8. Return ONLY executable code without explanations or markdown formatting."),
                GrokKey = Settings.GetString(SETTINGS_KEY + "GrokKey", ""),
                MeshyKey = Settings.GetString(SETTINGS_KEY + "MeshyKey", "")
            };
        }

        // You can override methods here to change the plug-in behavior on
        // loading and shut down, add options pages to the Rhino _Option command
        // and maintain plug-in wide options in a document.

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            // Create the command instance
            var command = new RhinoM8Command();
            
            // Create and show the persistent form independently
            _promptForm = new PersistentPromptForm(command);
            _promptForm.Show();  // Show independently instead of using Rhino's window handle
            
            return LoadReturnCode.Success;
        }

        protected override void OnShutdown()
        {
            if (_promptForm != null && !_promptForm.IsDisposed)
            {
                _promptForm.Dispose();
                _promptForm = null;
            }
            base.OnShutdown();
        }

        public void ShowPromptWindow()
        {
            // Check if form is disposed or null
            if (_promptForm == null || _promptForm.IsDisposed)
            {
                // Create a new command instance
                var command = new RhinoM8Command();
                
                // Create new form instance
                _promptForm = new PersistentPromptForm(command);
            }

            if (!_promptForm.Visible)
            {
                // Get Rhino's main window handle and show the form
                var mainWindow = new WindowWrapper(Rhino.RhinoApp.MainWindowHandle());
                _promptForm.Show(mainWindow);
                _promptForm.BringToFront();
            }
        }

        public List<CodeHistoryEntry> GetCodeHistory()
        {
            return _codeHistory;
        }

        // Helper class to wrap the window handle
        private class WindowWrapper : System.Windows.Forms.IWin32Window
        {
            public WindowWrapper(IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle { get; }
        }
    }
}
