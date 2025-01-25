using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Rhino.Geometry;
using Rhino.FileIO;
using System.IO;

namespace RhinoM8
{
    public class MeshyService
    {
        private readonly HttpClient _client;
        private readonly string _apiKey;
        private int _targetPolycount = 30000;

        public MeshyService(string apiKey)
        {
            _apiKey = apiKey;
            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        public class TextTo3DRequest
        {
            public string mode { get; set; } = "preview";
            public string prompt { get; set; }
            public string art_style { get; set; } = "realistic";
            public string negative_prompt { get; set; } = "low quality, low resolution, low poly, ugly";
            public string ai_model { get; set; } = "meshy-4";
            public string topology { get; set; } = "quad";
            public int target_polycount { get; set; } = 30000;
            public string preview_task_id { get; set; }  // For refine mode
            public bool enable_pbr { get; set; } = true;
        }

        public class TextTo3DResponse
        {
            public string result { get; set; }  // Task ID
        }

        public class TaskResponse
        {
            public string id { get; set; }
            public ModelUrls model_urls { get; set; }
            public string thumbnail_url { get; set; }
            public int progress { get; set; }
            public long started_at { get; set; }
            public long created_at { get; set; }
            public long expires_at { get; set; }
            public long finished_at { get; set; }
            public string status { get; set; }
            public List<TextureUrls> texture_urls { get; set; }
            public int preceding_tasks { get; set; }
            public TaskError task_error { get; set; }
        }

        public class TextureUrls
        {
            public string base_color { get; set; }
            public string metallic { get; set; }
            public string normal { get; set; }
            public string roughness { get; set; }
        }

        public class TaskError
        {
            public string message { get; set; }
        }

        public class ModelUrls
        {
            public string glb { get; set; }
            public string fbx { get; set; }
            public string obj { get; set; }
            public string mtl { get; set; }
            public string usdz { get; set; }
        }

        public void SetTargetPolycount(int value)
        {
            _targetPolycount = value;
        }

        public async Task<Mesh> GenerateModelFromPrompt(string prompt, IProgress<int> progress = null)
        {
            try
            {
                // Create preview task
                var previewRequest = new TextTo3DRequest 
                { 
                    mode = "preview",
                    prompt = prompt,
                    art_style = "realistic",
                    topology = "triangle",
                    enable_pbr = true,
                    target_polycount = _targetPolycount  // Use the configured value
                };

                var json = JsonConvert.SerializeObject(previewRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _client.PostAsync("https://api.meshy.ai/v2/text-to-3d", content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<TextTo3DResponse>(responseString);

                if (string.IsNullOrEmpty(result?.result))
                {
                    throw new Exception("No task ID in response");
                }

                // Poll for preview completion
                string taskId = result.result;
                TaskResponse taskStatus;
                do
                {
                    await Task.Delay(2000); // Wait 2 seconds between polls
                    var statusResponse = await _client.GetAsync($"https://api.meshy.ai/v2/text-to-3d/{taskId}");
                    statusResponse.EnsureSuccessStatusCode();

                    var statusString = await statusResponse.Content.ReadAsStringAsync();
                    taskStatus = JsonConvert.DeserializeObject<TaskResponse>(statusString);

                    if (taskStatus.status == "FAILED")
                    {
                        throw new Exception("Preview task failed");
                    }

                    progress?.Report(taskStatus.progress / 2);  // First half of progress

                } while (taskStatus.status != "SUCCEEDED");

                // Create refine task
                var refineRequest = new TextTo3DRequest
                {
                    mode = "refine",
                    preview_task_id = taskId
                };

                json = JsonConvert.SerializeObject(refineRequest);
                content = new StringContent(json, Encoding.UTF8, "application/json");

                response = await _client.PostAsync("https://api.meshy.ai/v2/text-to-3d", content);
                response.EnsureSuccessStatusCode();

                responseString = await response.Content.ReadAsStringAsync();
                result = JsonConvert.DeserializeObject<TextTo3DResponse>(responseString);

                if (string.IsNullOrEmpty(result?.result))
                {
                    throw new Exception("No refine task ID in response");
                }

                // Poll for refine completion
                taskId = result.result;
                do
                {
                    await Task.Delay(2000);
                    var statusResponse = await _client.GetAsync($"https://api.meshy.ai/v2/text-to-3d/{taskId}");
                    statusResponse.EnsureSuccessStatusCode();

                    var statusString = await statusResponse.Content.ReadAsStringAsync();
                    taskStatus = JsonConvert.DeserializeObject<TaskResponse>(statusString);

                    if (taskStatus.status == "FAILED")
                    {
                        throw new Exception("Refine task failed");
                    }

                    progress?.Report(50 + taskStatus.progress / 2);  // Second half of progress

                } while (taskStatus.status != "SUCCEEDED");

                // Create models directory if it doesn't exist
                var modelsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RhinoM8", "Models");
                Directory.CreateDirectory(modelsDir);

                // Save GLB file with timestamp and sanitized prompt as name
                var safePrompt = string.Join("_", prompt.Split(Path.GetInvalidFileNameChars()));
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"{timestamp}_{safePrompt}";
                
                // Download and save GLB file
                var glbPath = Path.Combine(modelsDir, fileName + ".glb");
                var modelBytes = await _client.GetByteArrayAsync(taskStatus.model_urls.glb);
                File.WriteAllBytes(glbPath, modelBytes);

                // Save thumbnail for preview
                var thumbnailDir = Path.Combine(modelsDir, "thumbnails");
                Directory.CreateDirectory(thumbnailDir);
                var thumbnailPath = Path.Combine(thumbnailDir, $"{fileName}_thumb.png");

                // Download and save thumbnail if available
                if (!string.IsNullOrEmpty(taskStatus.thumbnail_url))
                {
                    var thumbnailBytes = await _client.GetByteArrayAsync(taskStatus.thumbnail_url);
                    File.WriteAllBytes(thumbnailPath, thumbnailBytes);
                }

                // Download and save textures if they exist
                if (taskStatus.texture_urls != null && taskStatus.texture_urls.Any())
                {
                    var textureDir = Path.Combine(modelsDir, fileName + "_textures");
                    Directory.CreateDirectory(textureDir);

                    foreach (var texture in taskStatus.texture_urls)
                    {
                        if (!string.IsNullOrEmpty(texture.base_color))
                        {
                            var textureBytes = await _client.GetByteArrayAsync(texture.base_color);
                            var texturePath = Path.Combine(textureDir, "base_color.png");
                            File.WriteAllBytes(texturePath, textureBytes);
                        }
                    }
                }

                // Create history entry with file path
                var historyEntry = new MeshHistoryEntry(prompt, glbPath, "Meshy-3D", thumbnailPath);
                RhinoM8Plugin.Instance.AddToHistory(historyEntry);

                // Import the file directly into Rhino
                var doc = Rhino.RhinoDoc.ActiveDoc;
                if (doc != null)
                {
                    // Import the GLB file with materials
                    Rhino.RhinoApp.RunScript($"_-Import \"{glbPath}\" _Enter _Materials=_Yes _Enter", false);
                    doc.Views.Redraw();
                }

                // Return a placeholder mesh (since the actual geometry is already in the document)
                return new Mesh();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error generating 3D model: {ex.Message}");
            }
        }

        public async Task<string> ProcessImageToMesh(string base64Image)
        {
            try
            {
                if (string.IsNullOrEmpty(base64Image))
                {
                    throw new ArgumentException("Base64 image data is empty");
                }

                // Create the request payload according to the documentation
                var imageRequest = new
                {
                    image_url = base64Image,  // Using the full data URI
                    enable_pbr = true,
                    should_texture = true,
                    topology = "triangle",
                    target_polycount = _targetPolycount,
                };

                var json = JsonConvert.SerializeObject(imageRequest);
                
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    // Set proper headers and use correct endpoint
                    _client.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
                    
                    var response = await _client.PostAsync("https://api.meshy.ai/openapi/v1/image-to-3d", content);
                    
                    var responseString = await response.Content.ReadAsStringAsync();
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"API request failed: {response.StatusCode} - {responseString}");
                    }

                    var result = JsonConvert.DeserializeObject<TextTo3DResponse>(responseString);
                    return result?.result;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error processing image: {ex.Message}", ex);
            }
        }

        public async Task<Mesh> ProcessImageToMeshComplete(string base64Image, string prompt, IProgress<int> progress = null)
        {
            try
            {
                // First, send the image to get a task ID
                var taskId = await ProcessImageToMesh(base64Image);
                if (string.IsNullOrEmpty(taskId))
                {
                    throw new Exception("Failed to get task ID from image upload");
                }

                // Poll for completion
                TaskResponse taskStatus;
                do
                {
                    await Task.Delay(2000);
                    taskStatus = await GetTaskStatus(taskId);

                    if (taskStatus.status == "FAILED")
                    {
                        var errorMessage = !string.IsNullOrEmpty(taskStatus.task_error?.message) 
                            ? taskStatus.task_error.message 
                            : "Image processing task failed";
                        throw new Exception(errorMessage);
                    }

                    progress?.Report(taskStatus.progress);

                } while (taskStatus.status != "SUCCEEDED");

                // Once succeeded, download and import the model
                if (taskStatus.model_urls?.glb != null)
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var safePrompt = !string.IsNullOrEmpty(prompt) 
                        ? string.Join("_", prompt.Split(Path.GetInvalidFileNameChars()))
                        : "image";
                    var fileName = $"{timestamp}_{safePrompt}";

                    var modelsDir = Path.Combine(Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData), "RhinoM8", "Models");
                    Directory.CreateDirectory(modelsDir);

                    // Download GLB
                    var glbPath = Path.Combine(modelsDir, fileName + ".glb");
                    var modelBytes = await _client.GetByteArrayAsync(taskStatus.model_urls.glb);
                    File.WriteAllBytes(glbPath, modelBytes);

                    // Download thumbnail
                    var thumbnailPath = "";
                    if (!string.IsNullOrEmpty(taskStatus.thumbnail_url))
                    {
                        var thumbnailDir = Path.Combine(modelsDir, "thumbnails");
                        Directory.CreateDirectory(thumbnailDir);
                        thumbnailPath = Path.Combine(thumbnailDir, $"{fileName}_thumb.png");
                        var thumbnailBytes = await _client.GetByteArrayAsync(taskStatus.thumbnail_url);
                        File.WriteAllBytes(thumbnailPath, thumbnailBytes);
                    }

                    // Download textures
                    if (taskStatus.texture_urls?.Any() == true)
                    {
                        var textureDir = Path.Combine(modelsDir, fileName + "_textures");
                        Directory.CreateDirectory(textureDir);

                        foreach (var texture in taskStatus.texture_urls)
                        {
                            if (!string.IsNullOrEmpty(texture.base_color))
                                await DownloadTexture(texture.base_color, textureDir, "base_color.png");
                            if (!string.IsNullOrEmpty(texture.metallic))
                                await DownloadTexture(texture.metallic, textureDir, "metallic.png");
                            if (!string.IsNullOrEmpty(texture.normal))
                                await DownloadTexture(texture.normal, textureDir, "normal.png");
                            if (!string.IsNullOrEmpty(texture.roughness))
                                await DownloadTexture(texture.roughness, textureDir, "roughness.png");
                        }
                    }

                    // Create history entry
                    var historyEntry = new MeshHistoryEntry(prompt ?? "Image Upload", glbPath, "Image-to-3D", thumbnailPath);
                    RhinoM8Plugin.Instance.AddToHistory(historyEntry);

                    // Import into Rhino
                    var doc = Rhino.RhinoDoc.ActiveDoc;
                    if (doc != null)
                    {
                        Rhino.RhinoApp.RunScript($"_-Import \"{glbPath}\" _Enter _Materials=_Yes _Enter", false);
                        doc.Views.Redraw();
                    }
                }

                return new Mesh();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error processing image to mesh: {ex.Message}");
            }
        }

        private async Task DownloadTexture(string url, string directory, string filename)
        {
            try
            {
                var bytes = await _client.GetByteArrayAsync(url);
                File.WriteAllBytes(Path.Combine(directory, filename), bytes);
            }
            catch (Exception ex)
            {
                Rhino.RhinoApp.WriteLine($"Warning: Failed to download texture {filename}: {ex.Message}");
            }
        }

        public async Task<TaskResponse> GetTaskStatus(string taskId)
        {
            try
            {
                var response = await _client.GetAsync($"https://api.meshy.ai/openapi/v1/image-to-3d/{taskId}");
                response.EnsureSuccessStatusCode();

                var statusString = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<TaskResponse>(statusString);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error checking task status: {ex.Message}", ex);
            }
        }
    }
} 