using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Rhino.Geometry;
using System.IO;

namespace RhinoM8
{
    public class EdifyService
    {
        private readonly HttpClient _client;
        private readonly string _apiKey;

        public EdifyService(string apiKey)
        {
            _apiKey = apiKey;
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            _client.DefaultRequestHeaders.Add("User-Agent", "RhinoM8/1.0");
        }

        public class TextTo3DRequest
        {
            public string prompt { get; set; }
            public TextTo3DOptions options { get; set; }
        }

        public class TextTo3DOptions
        {
            public bool mesh_adaptive_num_faces { get; set; } = true;
            public string mesh_retopo_form { get; set; } = "quad";
            public string mode { get; set; } = "full";
            public string[] output_format_list { get; set; } = new[] { "glb" };
            public bool prompt_is_upsampled { get; set; } = true;
            public int texture_res_num { get; set; } = 2048;
            public string turntable_format { get; set; } = "none";
        }

        public class TextTo3DResponse
        {
            public string id { get; set; }
            public string status { get; set; }
            public string model_url { get; set; }
        }

        public async Task<Mesh> GenerateModelFromPrompt(string prompt)
        {
            var request = new TextTo3DRequest
            {
                prompt = prompt,
                options = new TextTo3DOptions()
            };

            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _client.PostAsync("https://api.shutterstock.com/v2/ai-generated/text-to-3d", content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<TextTo3DResponse>(responseString);

                if (result?.model_url != null)
                {
                    // Download the GLB file
                    var modelBytes = await _client.GetByteArrayAsync(result.model_url);
                    
                    // Create a temporary file with .glb extension
                    var tempPath = Path.GetTempPath();
                    var tempFileName = Path.GetRandomFileName() + ".glb";
                    var tempFile = Path.Combine(tempPath, tempFileName);

                    try
                    {
                        // Write the file synchronously
                        File.WriteAllBytes(tempFile, modelBytes);

                        // Convert GLB to Rhino Mesh
                        var mesh = ConvertGLBToRhinoMesh(tempFile);

                        return mesh;
                    }
                    finally
                    {
                        // Clean up temp file
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                    }
                }

                throw new Exception("No model URL in response");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating 3D model: {ex.Message}");
            }
        }

        private Mesh ConvertGLBToRhinoMesh(string glbFilePath)
        {
            // TODO: Implement GLB to Rhino Mesh conversion
            // For now, return a simple test mesh to verify the pipeline works
            var mesh = new Mesh();
            mesh.Vertices.Add(0, 0, 0);
            mesh.Vertices.Add(1, 0, 0);
            mesh.Vertices.Add(0, 1, 0);
            mesh.Faces.AddFace(0, 1, 2);
            mesh.Normals.ComputeNormals();
            
            return mesh;
        }
    }
} 