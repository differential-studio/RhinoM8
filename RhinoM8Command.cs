using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.IO;

namespace RhinoM8
{
    public class RhinoM8Command : Command
    {
        public RhinoM8Command()
        {
            Instance = this;
            _meshyKey = string.Empty;
            _meshyService = new MeshyService(string.Empty);
        }

        ///<summary>The only instance of this command.</summary>
        public static RhinoM8Command Instance { get; private set; }

        private string _claudeApiKey = "";
        private string _openAiApiKey = "";
        private string _grokKey = "";
        private string _systemPrompt = @"You are a helpful assistant that generates Python code for Rhino 8 using rhinoscriptsyntax. Return ONLY executable code without ANY explanations, comments, or markdown formatting.

CRITICAL RULES FOR CODE:
1. ALWAYS start with: import rhinoscriptsyntax as rs

CRITICAL RULES FOR VARIABLES:
1. ALWAYS declare ALL numeric values as variables with '_value' suffix at the start of the script:
   width_value = value     # use the units from user's document
   height_value = value    # use the units from user's document
   length_value = value    # use the units from user's document
   radius_value = value    # use the units from user's document
   angle_value = 45        # degrees (always)
   count_value = 10        # count (always)

2. DO NOT use functions or classes unless specifically requested
3. Keep code linear and simple
4. Place ALL variable declarations at the start
5. Never use hardcoded numbers in geometric functions

CRITICAL GEOMETRY RULES:
1. ALWAYS store object IDs in variables
2. NEVER use rs.LastCreatedObjects() - it returns None
3. NEVER pass point arrays directly to rs.AddPlanarSrf()
4. ALWAYS create curves first, then surfaces
5. ALWAYS check if operations return None before using results
6. Use rs.CopyObject() when you need to preserve original geometry
7. ALWAYS use rs.WorldXYPlane() or rs.PlaneFromFrame() for cylinders
8. NEVER use None as rotation axis - use [0,0,1] instead
9. NEVER delete objects that might be used later
10. ALWAYS store results of boolean operations

COMMON GEOMETRY PATTERNS:

# Creating Cylinder (CORRECT WAY)
radius_value = value
height_value = value
plane = rs.WorldXYPlane()
cylinder = rs.AddCylinder(plane, height_value, radius_value)

# Creating Circle (CORRECT WAY)
radius_value = value
plane = rs.WorldXYPlane()
circle = rs.AddCircle(plane, radius_value)

# Moving Objects (CORRECT WAY)
distance_value = value
move_vector = [distance_value, 0, 0]
moved_id = rs.MoveObject(object_id, move_vector)

# Rotating Objects (CORRECT WAY)
angle_value = 45
axis_point = [0, 0, 0]
axis_vector = [0, 0, 1]
rotated_id = rs.RotateObject(object_id, axis_point, angle_value, axis_vector)

# Boolean Operations (CORRECT WAY)
result = rs.BooleanDifference(main_object, tool_object)
if result:
    main_object = result[0]  # Update the main object ID

# Creating Multiple Copies (CORRECT WAY)
count_value = 8
radius_value = 1000
angle_value = 360 / count_value

copies = []
for i in range(count_value):
    current_angle = i * angle_value
    copy = rs.CopyObject(original_id)
    if copy:
        rotated = rs.RotateObject(copy, [0,0,0], current_angle, [0,0,1])
        if rotated:
            copies.append(rotated)

IMPORTANT OUTPUT RULES:
1. Return ONLY the Python code, nothing else
2. Do not include any explanations before or after the code
3. Do not use markdown code blocks
4. Do not use comments in the code
5. The script must return the GUID of the final created object
6. Store the final object's ID in a variable named 'result'
7. The last line must be ONLY: str(result)";

        private double _temperature = 0.1;
        private int _maxTokens = 2000;

        private readonly HttpClient _httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private MeshyService _meshyService;
        private string _meshyKey;

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "RhinoM8";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // Show the persistent window when command is run
            RhinoM8Plugin.Instance.ShowPromptWindow();
            return Result.Success;
        }

        public async Task<string> ExecutePrompt(string prompt, string provider, IProgress<int> progress = null)
        {
            try
            {
                string units = Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem.ToString();
                double scale = RhinoMath.UnitScale(Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem, UnitSystem.Meters);

                if (provider == "Meshy-3D")
                {
                    await _meshyService.GenerateModelFromPrompt(prompt, progress);
                    return string.Empty;
                }

                // Handle Image-to-3D separately
                if (provider == "Image-to-3D")
                {
                    // Convert image to base64 and return it directly
                    byte[] imageBytes = File.ReadAllBytes(prompt.Replace("Selected: ", "").Trim());
                    string base64Image = Convert.ToBase64String(imageBytes);
                    string imageFormat = Path.GetExtension(prompt).ToLower().Replace(".", "");
                    return $"data:image/{imageFormat};base64,{base64Image}";
                }

                // Existing provider handling
                RhinoApp.WriteLine($"Making API call to {provider} with prompt: {prompt}");
                RhinoApp.WriteLine($"Document Units: {units}, Scale: {scale}");

                string response = null;
                switch (provider)
                {
                    case "OpenAI":
                        response = await GetOpenAIResponse(prompt, units, scale);
                        break;
                    case "Claude":
                        response = await GetClaudeResponse(prompt, units, scale);
                        break;
                    case "Grok":
                        response = await GetGrokResponse(prompt, units, scale);
                        break;
                    case "Meshy-3D":
                        response = await GetMeshyResponse(prompt, units, scale);
                        break;
                    default:
                        throw new Exception($"Unknown provider: {provider}");
                }

                return response;
            }
            catch (Exception ex)
            {
                DarkForm.ShowDialog($"API Call Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RhinoApp.WriteLine($"Stack Trace: {ex.StackTrace}");
                return null;
            }
        }

        private async Task<string> GetAIResponse(string prompt, string provider, string units, double scale)
        {
            try
            {
                if (provider == "claude")
                    return await GetClaudeResponse(prompt, units, scale).ConfigureAwait(false);
                else if (provider == "openai")
                    return await GetOpenAIResponse(prompt, units, scale).ConfigureAwait(false);
                else
                {
                    RhinoApp.WriteLine("Invalid AI provider selected");
                    return null;
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error getting AI response: {ex.Message}");
                return null;
            }
        }

        private async Task<string> GetClaudeResponse(string prompt, string units, double scale)
        {
            const int maxRetries = 3;
            const int delayMilliseconds = 1000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Add("x-api-key", _claudeApiKey);
                    _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

                    var requestBody = new
                    {
                        model = "claude-3-sonnet-20240229",
                        system = _systemPrompt,
                        messages = new[]
                        {
                            new
                            {
                                role = "user",
                                content = string.Format(@"Document settings:
- Units: {0}

IMPORTANT: 
- Return ONLY executable Python code
- DO NOT include any explanations or comments
- Work with the current unit system ({0})
- DO NOT use functions or classes
- ALL numeric values MUST be declared as variables with '_value' suffix
- ALL variable declarations MUST be at the start of the script
- Convert any measurements in the prompt to {0}

Generate code for: {1}",
                                units,
                                prompt)
                            }
                        },
                        max_tokens = _maxTokens,
                        temperature = _temperature
                    };

                    var jsonContent = JsonConvert.SerializeObject(requestBody);

                    using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30)))
                    {
                        var response = await _httpClient.PostAsync(
                            "https://api.anthropic.com/v1/messages",
                            new StringContent(jsonContent, Encoding.UTF8, "application/json"),
                            cts.Token
                        ).ConfigureAwait(false);

                        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (response.IsSuccessStatusCode)
                        {
                            var jsonResponse = JObject.Parse(responseContent);
                            var content = jsonResponse["content"]?[0]?["text"]?.ToString();

                            if (string.IsNullOrEmpty(content))
                            {
                                RhinoApp.WriteLine($"Failed to parse response: {responseContent}");
                                return null;
                            }

                            // Clean the response while preserving indentation
                            return content
                                .Replace("```python", "")
                                .Replace("```", "")
                                .Replace("Here's", "")
                                .Replace("Here is", "")
                                .Replace("the code:", "")
                                .Replace("Python code:", "")
                                .Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                .Where(line => !line.StartsWith("//") && !line.StartsWith("#"))
                                .Where(line => !string.IsNullOrWhiteSpace(line))
                                .Select(line => line.TrimEnd())  // Only trim end, preserve leading spaces
                                .Aggregate((a, b) => a + "\n" + b)
                                .Trim();
                        }
                        else
                        {
                            RhinoApp.WriteLine($"Attempt {attempt} failed: {response.StatusCode} - {responseContent}");

                            if (attempt < maxRetries)
                            {
                                RhinoApp.WriteLine($"Retrying in {delayMilliseconds / 1000} seconds...");
                                await Task.Delay(delayMilliseconds).ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"Attempt {attempt} error: {ex.Message}");

                    if (attempt < maxRetries)
                    {
                        RhinoApp.WriteLine($"Retrying in {delayMilliseconds / 1000} seconds...");
                        await Task.Delay(delayMilliseconds).ConfigureAwait(false);
                    }
                    else
                    {
                        RhinoApp.WriteLine($"All retry attempts failed. Stack Trace: {ex.StackTrace}");
                        return null;
                    }
                }
            }

            return null;
        }

        private async Task<string> GetOpenAIResponse(string prompt, string units, double scale)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_openAiApiKey}");

                var messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = _systemPrompt
                    },
                    new
                    {
                        role = "user",
                        content = string.Format(@"Document settings:
- Units: {0}

IMPORTANT: 
- Return ONLY executable Python code
- DO NOT include any explanations or comments
- Work with the current unit system ({0})
- DO NOT use functions or classes
- ALL numeric values MUST be declared as variables with '_value' suffix
- ALL variable declarations MUST be at the start of the script
- Convert any measurements in the prompt to {0}

Generate code for: {1}",
                            units,
                            prompt)
                    }
                };

                var requestBody = new
                {
                    model = "gpt-4",
                    messages = messages,
                    temperature = 0.1,  // Changed from 0.7 to 0.1
                    max_tokens = 2000
                };

                var jsonContent = JsonConvert.SerializeObject(requestBody);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    "https://api.openai.com/v1/chat/completions",
                    httpContent
                ).ConfigureAwait(false);

                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                RhinoApp.WriteLine($"OpenAI response: {responseContent}"); // Debug line

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = JObject.Parse(responseContent);
                    var assistantMessage = jsonResponse["choices"]?[0]?["message"]?["content"]?.ToString();

                    if (string.IsNullOrEmpty(assistantMessage))
                    {
                        RhinoApp.WriteLine($"Empty or invalid message format: {responseContent}");
                        return null;
                    }

                    // Clean the response while preserving indentation
                    return assistantMessage
                        .Replace("```python", "")
                        .Replace("```", "")
                        .Replace("Here's", "")
                        .Replace("Here is", "")
                        .Replace("the code:", "")
                        .Replace("Python code:", "")
                        .Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(line => !line.StartsWith("//") && !line.StartsWith("#"))
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => line.TrimEnd())  // Only trim end, preserve leading spaces
                        .Aggregate((a, b) => a + "\n" + b)
                        .Trim();
                }
                else
                {
                    RhinoApp.WriteLine($"OpenAI API call failed: {responseContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                DarkForm.ShowDialog($"API Call Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RhinoApp.WriteLine($"Stack Trace: {ex.StackTrace}");
                return null;
            }
        }

        private async Task<string> GetAssistantResponse(string prompt, string units, double scale)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_openAiApiKey}");
                _httpClient.DefaultRequestHeaders.Add("OpenAI-Beta", "assistants=v2");

                // Create a thread
                var threadRequest = new { };
                var threadContent = new StringContent(
                    JsonConvert.SerializeObject(threadRequest),
                    Encoding.UTF8,
                    "application/json"
                );
                
                var threadResponse = await _httpClient.PostAsync(
                    "https://api.openai.com/v1/threads",
                    threadContent
                );
                
                if (!threadResponse.IsSuccessStatusCode)
                {
                    var threadError = await threadResponse.Content.ReadAsStringAsync();
                    RhinoApp.WriteLine($"Thread creation failed: {threadResponse.StatusCode} - {threadError}");
                    return null;
                }

                var threadJson = JObject.Parse(await threadResponse.Content.ReadAsStringAsync());
                string threadId = threadJson["id"]?.ToString();
                
                if (string.IsNullOrEmpty(threadId))
                {
                    RhinoApp.WriteLine("Failed to get thread ID from response");
                    return null;
                }

                // Add message to thread with system prompt
                var messageRequest = new
                {
                    role = "user",
                    content = $"{_systemPrompt}\n\nDocument settings:\n- Units: {units}\n\nGenerate code for: {prompt}"
                };
                
                var messageContent = new StringContent(
                    JsonConvert.SerializeObject(messageRequest),
                    Encoding.UTF8,
                    "application/json"
                );
                
                var messageResponse = await _httpClient.PostAsync(
                    $"https://api.openai.com/v1/threads/{threadId}/messages",
                    messageContent
                );

                if (!messageResponse.IsSuccessStatusCode)
                {
                    var messageError = await messageResponse.Content.ReadAsStringAsync();
                    RhinoApp.WriteLine($"Message creation failed: {messageResponse.StatusCode} - {messageError}");
                    return null;
                }

                // Run the assistant
                var runRequest = new
                {
                    assistant_id = "asst_zXbv0mZn4J1OZ42CsLgDUIIE",
                    instructions = "You are a Python code generator for Rhino. Return ONLY executable code."
                };
                
                var runContent = new StringContent(
                    JsonConvert.SerializeObject(runRequest),
                    Encoding.UTF8,
                    "application/json"
                );
                
                var runResponse = await _httpClient.PostAsync(
                    $"https://api.openai.com/v1/threads/{threadId}/runs",
                    runContent
                );

                if (!runResponse.IsSuccessStatusCode)
                {
                    var runError = await runResponse.Content.ReadAsStringAsync();
                    RhinoApp.WriteLine($"Run creation failed: {runResponse.StatusCode} - {runError}");
                    return null;
                }
                
                var runJson = JObject.Parse(await runResponse.Content.ReadAsStringAsync());
                string runId = runJson["id"]?.ToString();

                if (string.IsNullOrEmpty(runId))
                {
                    RhinoApp.WriteLine("Failed to get run ID from response");
                    return null;
                }

                // Poll for completion with timeout
                int maxAttempts = 30; // 30 seconds timeout
                int attempts = 0;

                while (attempts < maxAttempts)
                {
                    await Task.Delay(1000);
                    attempts++;

                    var statusResponse = await _httpClient.GetAsync(
                        $"https://api.openai.com/v1/threads/{threadId}/runs/{runId}"
                    );

                    if (!statusResponse.IsSuccessStatusCode)
                    {
                        var statusError = await statusResponse.Content.ReadAsStringAsync();
                        RhinoApp.WriteLine($"Status check failed: {statusResponse.StatusCode} - {statusError}");
                        continue;
                    }

                    var statusJson = JObject.Parse(await statusResponse.Content.ReadAsStringAsync());
                    string status = statusJson["status"]?.ToString();
                    
                    RhinoApp.WriteLine($"Run status: {status} (attempt {attempts}/{maxAttempts})");

                    if (status == "completed")
                    {
                        var messagesResponse = await _httpClient.GetAsync(
                            $"https://api.openai.com/v1/threads/{threadId}/messages"
                        );

                        if (!messagesResponse.IsSuccessStatusCode)
                        {
                            var messagesError = await messagesResponse.Content.ReadAsStringAsync();
                            RhinoApp.WriteLine($"Messages retrieval failed: {messagesResponse.StatusCode} - {messagesError}");
                            return null;
                        }

                        var messagesJson = JObject.Parse(await messagesResponse.Content.ReadAsStringAsync());
                        var assistantMessage = messagesJson["data"]?[0]?["content"]?[0]?["text"]?["value"]?.ToString();

                        if (string.IsNullOrEmpty(assistantMessage))
                        {
                            RhinoApp.WriteLine("Failed to extract message content from response");
                            return null;
                        }

                        return CleanResponse(assistantMessage);
                    }
                    else if (status == "failed" || status == "cancelled" || status == "expired")
                    {
                        var error = statusJson["last_error"]?.ToString() ?? "Unknown error";
                        throw new Exception($"Assistant run failed with status: {status}. Error: {error}");
                    }
                }

                throw new Exception($"Assistant run timed out after {maxAttempts} seconds");
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Assistant API Error: {ex.Message}");
                RhinoApp.WriteLine($"Stack Trace: {ex.StackTrace}");
                return null;
            }
        }

        private async Task<string> GetGrokResponse(string prompt, string units, double scale)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_grokKey}");

                var requestBody = new
                {
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = _systemPrompt
                        },
                        new
                        {
                            role = "user",
                            content = string.Format(@"Document settings:
- Units: {0}

IMPORTANT: 
- Return ONLY executable Python code
- DO NOT include any explanations or comments
- Work with the current unit system ({0})
- DO NOT use functions or classes
- ALL numeric values MUST be declared as variables with '_value' suffix
- ALL variable declarations MUST be at the start of the script
- Convert any measurements in the prompt to {0}

Generate code for: {1}",
                            units,
                            prompt)
                        }
                    },
                    model = "grok-beta",
                    stream = false,
                    temperature = _temperature
                };

                var jsonContent = JsonConvert.SerializeObject(requestBody);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    "https://api.x.ai/v1/chat/completions",
                    httpContent
                ).ConfigureAwait(false);

                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                RhinoApp.WriteLine($"Grok response: {responseContent}"); // Debug line

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = JObject.Parse(responseContent);
                    var assistantMessage = jsonResponse["choices"]?[0]?["message"]?["content"]?.ToString();

                    if (string.IsNullOrEmpty(assistantMessage))
                    {
                        RhinoApp.WriteLine($"Empty or invalid message format: {responseContent}");
                        return null;
                    }

                    return CleanResponse(assistantMessage);
                }
                else
                {
                    RhinoApp.WriteLine($"Grok API call failed: {responseContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                DarkForm.ShowDialog($"API Call Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RhinoApp.WriteLine($"Stack Trace: {ex.StackTrace}");
                return null;
            }
        }

        // Add this helper method to clean responses consistently
        private string CleanResponse(string response)
        {
            return response
                .Replace("```python", "")
                .Replace("```", "")
                .Replace("Here's", "")
                .Replace("Here is", "")
                .Replace("the code:", "")
                .Replace("Python code:", "")
                .Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.StartsWith("//") && !line.StartsWith("#"))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.TrimEnd())  // Only trim end, preserve leading spaces
                .Aggregate((a, b) => a + "\n" + b)
                .Trim();
        }

        public void UpdateLLMSettings(LLMSettingsForm.LLMSettings settings)
        {
            _temperature = settings.Temperature;
            _maxTokens = settings.MaxTokens;
            _systemPrompt = settings.SystemPrompt;
            _claudeApiKey = settings.ClaudeKey;
            _openAiApiKey = settings.OpenAIKey;
            _grokKey = settings.GrokKey;
            _meshyKey = settings.MeshyKey;
            _meshyService = new MeshyService(_meshyKey);
        }

        private async Task<string> GetMeshyResponse(string prompt, string units, double scale)
        {
            try
            {
                // Use GenerateModelFromPrompt instead of SendPrompt
                await _meshyService.GenerateModelFromPrompt(prompt);
                // Since GenerateModelFromPrompt handles the import directly, we don't need to return a script
                return string.Empty;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Meshy API Error: {ex.Message}");
                return null;
            }
        }
    }
}
