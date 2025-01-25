using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Windows.Forms;
using Rhino;

namespace RhinoM8
{
    public class MeshyImageService
    {
        private readonly MeshyService _meshyService;

        public MeshyImageService(MeshyService meshyService)
        {
            _meshyService = meshyService;
        }

        public async Task<string> ProcessImage(string base64Image)
        {
            try 
            {
                if (string.IsNullOrEmpty(base64Image))
                {
                    RhinoApp.WriteLine("Error: No image data received");
                    return null;
                }

                RhinoApp.WriteLine("Starting image processing...");
                
                var taskId = await _meshyService.ProcessImageToMesh(base64Image);
                
                if (string.IsNullOrEmpty(taskId))
                {
                    RhinoApp.WriteLine("Error: No task ID received from Meshy API");
                    return null;
                }

                RhinoApp.WriteLine($"Successfully started image processing with task ID: {taskId}");
                return taskId;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error processing image: {ex.Message}");
                return null;
            }
        }
    }
} 