using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using Rhino;

namespace RhinoM8
{
    public class SliderPanel : DarkForm
    {
        public class VariableSlider
        {
            public TrackBar Slider { get; set; }
            public Label NameLabel { get; set; }
            public Label ValueLabel { get; set; }
            public double MinValue { get; set; }
            public double MaxValue { get; set; }
            public double CurrentValue { get; set; }
            public string VariableName { get; set; }
            public bool IsInteger { get; set; }
        }

        private List<VariableSlider> _sliders = new List<VariableSlider>();
        private string _pythonScript;
        private List<Guid> _lastCreatedObjects = new List<Guid>();
        private Button _bakeButton;

        public SliderPanel()
        {
            this.AutoScroll = false;
            this.BackColor = Color.FromArgb(32, 32, 32);
            this.MinimumSize = new Size(400, 200);
            this.Padding = new Padding(10);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.ControlBox = false;
            this.Text = "Parameter Controls";
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = false;

            // Adjust control layout for variable width
            this.SizeChanged += (s, e) =>
            {
                // Update value label positions when form width changes
                foreach (var slider in _sliders)
                {
                    slider.ValueLabel.Location = new Point(
                        this.ClientSize.Width - slider.ValueLabel.Width - 20,
                        slider.ValueLabel.Location.Y
                    );
                    slider.Slider.Width = this.ClientSize.Width - slider.NameLabel.Width - slider.ValueLabel.Width - 60;
                }
                
                // Update bake button position
                _bakeButton.Location = new Point(
                    (this.ClientSize.Width - _bakeButton.Width) / 2,  // Center horizontally
                    _bakeButton.Location.Y
                );
            };

            // Create Bake button
            _bakeButton = new Button
            {
                Text = "Bake",
                Size = new Size(80, 30),
                BackColor = Color.FromArgb(60, 120, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _bakeButton.Click += BakeButton_Click;
            this.Controls.Add(_bakeButton);

            // Handle form closing to hide instead of dispose
            this.FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    this.Hide();
                }
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            
            // Set up owner resize handler after the owner is assigned
            if (this.Owner != null)
            {
                this.Owner.SizeChanged += Owner_SizeChanged;
            }
        }

        private void Owner_SizeChanged(object sender, EventArgs e)
        {
            if (this.Owner != null)
            {
                this.Width = this.Owner.Width;  // Match width only
                this.Location = new Point(
                    this.Owner.Location.X,
                    this.Owner.Location.Y + this.Owner.Height + 5
                );
                // Don't modify height here
            }
        }

        public void UpdateFromScript(string pythonScript)
        {
            _pythonScript = pythonScript;
            var variables = ExtractVariables(pythonScript);
            CreateSliders(variables);
            
            // Clear any existing objects
            _lastCreatedObjects.Clear();

            // Force refresh and layout
            this.PerformLayout();
            this.Refresh();
            
            // Ensure proper sizing and positioning relative to parent form
            if (this.Owner != null)
            {
                this.Width = this.Owner.Width;  // Match width only
                this.Location = new Point(
                    this.Owner.Location.X,
                    this.Owner.Location.Y + this.Owner.Height + 5
                );
            }

            // Force immediate update
            Application.DoEvents();
        }

        private Dictionary<string, (double Value, bool IsInteger)> ExtractVariables(string pythonScript)
        {
            var variables = new Dictionary<string, (double Value, bool IsInteger)>();
            var lines = pythonScript.Split('\n');
            
            foreach (var line in lines)
            {
                // Match patterns like: variable = number
                var match = System.Text.RegularExpressions.Regex.Match(
                    line.Trim(), 
                    @"^(\w+)\s*=\s*([-+]?\d*\.?\d+)(?!\w)"
                );
                
                if (match.Success)
                {
                    string varName = match.Groups[1].Value;
                    if (double.TryParse(match.Groups[2].Value, out double value))
                    {
                        // Check if it's an integer by looking at the original string format
                        bool isInteger = !match.Groups[2].Value.Contains(".") || 
                                       match.Groups[2].Value.EndsWith(".0") ||
                                       match.Groups[2].Value.EndsWith(".");
                        
                        variables[varName] = (value, isInteger);
                    }
                }
            }
            return variables;
        }

        private void CreateSliders(Dictionary<string, (double Value, bool IsInteger)> variables)
        {
            // Clear existing sliders
            foreach (var slider in _sliders)
            {
                this.Controls.Remove(slider.NameLabel);
                this.Controls.Remove(slider.Slider);
                this.Controls.Remove(slider.ValueLabel);
                slider.Slider.Dispose();
                slider.NameLabel.Dispose();
                slider.ValueLabel.Dispose();
            }
            _sliders.Clear();

            int yOffset = 20;
            foreach (var variable in variables)
            {
                var slider = new VariableSlider
                {
                    VariableName = variable.Key,
                    CurrentValue = variable.Value.Value,
                    MinValue = variable.Value.Value - Math.Abs(variable.Value.Value),
                    MaxValue = variable.Value.Value + Math.Abs(variable.Value.Value),
                    IsInteger = variable.Value.IsInteger
                };

                // Create labels with more width
                slider.NameLabel = new Label
                {
                    Text = variable.Key,
                    Location = new Point(20, yOffset + 10),
                    Size = new Size(120, 25),
                    ForeColor = Color.White,
                    AutoSize = false
                };

                slider.ValueLabel = new Label
                {
                    Text = slider.IsInteger ? 
                        ((int)variable.Value.Value).ToString() : 
                        variable.Value.Value.ToString("F2"),
                    Location = new Point(this.ClientSize.Width - 100, yOffset + 10),  // Position from right edge
                    Size = new Size(80, 25),
                    ForeColor = Color.White,
                    AutoSize = false
                };

                // Create slider with initial full width
                slider.Slider = new TrackBar
                {
                    Location = new Point(140, yOffset),
                    Size = new Size(this.ClientSize.Width - slider.NameLabel.Width - slider.ValueLabel.Width - 60, 45),
                    Minimum = 0,
                    Maximum = 1000,
                    Value = 500,
                    BackColor = Color.FromArgb(45, 45, 45)
                };

                // Add event handler with integer/float handling
                slider.Slider.ValueChanged += (s, e) =>
                {
                    double percent = slider.Slider.Value / 1000.0;
                    slider.CurrentValue = slider.MinValue + (slider.MaxValue - slider.MinValue) * percent;
                    
                    // Format display and value based on integer/float
                    if (slider.IsInteger)
                    {
                        slider.CurrentValue = Math.Round(slider.CurrentValue);
                        slider.ValueLabel.Text = ((int)slider.CurrentValue).ToString();
                    }
                    else
                    {
                        slider.ValueLabel.Text = slider.CurrentValue.ToString("F2");
                    }
                    UpdateScript();
                };

                // Add controls
                this.Controls.Add(slider.NameLabel);
                this.Controls.Add(slider.Slider);
                this.Controls.Add(slider.ValueLabel);

                _sliders.Add(slider);
                yOffset += 60;
            }

            // Update the Bake button position
            _bakeButton.Location = new Point(140, yOffset + 10);
            
            // Calculate and set the form height based on content
            int contentHeight = yOffset + _bakeButton.Height + 60;  // Added more padding
            this.ClientSize = new Size(this.ClientSize.Width, contentHeight);
            
            // Force layout update
            this.ResumeLayout(true);
            this.PerformLayout();
        }

        private void UpdateScript()
        {
            if (string.IsNullOrEmpty(_pythonScript)) return;

            // Only update the script variables, don't create geometry
            string updatedScript = _pythonScript;
            foreach (var slider in _sliders)
            {
                var pattern = $"{slider.VariableName} = ";
                var valueStart = updatedScript.IndexOf(pattern) + pattern.Length;
                var valueEnd = updatedScript.IndexOf("\n", valueStart);
                if (valueEnd == -1) valueEnd = updatedScript.Length;
                
                var oldValue = updatedScript.Substring(valueStart, valueEnd - valueStart);
                var newValue = slider.CurrentValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                
                updatedScript = updatedScript.Replace(
                    $"{pattern}{oldValue}", 
                    $"{pattern}{newValue}"
                );
            }

            // Store the updated script
            _pythonScript = updatedScript;
        }

        private void BakeButton_Click(object sender, EventArgs e)
        {
            BakeGeometry(_pythonScript);
        }

        public void BakeGeometry(string script)
        {
            try
            {
                // Execute the script
                var py = Rhino.Runtime.PythonScript.Create();
                if (py == null) throw new Exception("Failed to create Python script engine");

                // Execute and get the result
                object scriptResult = py.ExecuteScript(script);
                string output = scriptResult?.ToString() ?? string.Empty;
                
                // Try to parse as GUID
                if (Guid.TryParse(output.Trim(), out Guid objectId))
                {
                    // Get the object from Rhino
                    var obj = Rhino.RhinoDoc.ActiveDoc.Objects.Find(objectId);
                    if (obj != null)
                    {
                        // Select the object and redraw
                        obj.Select(true);
                        Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
                        return; // Success - exit without error
                    }
                }
                
                // If we get here but the geometry was created, don't show an error
                if (Rhino.RhinoDoc.ActiveDoc.Objects.Count > 0)
                    return;
                
                throw new Exception("No geometry was created by the script");
            }
            catch (Exception ex)
            {
                // Remove the special case for 'result' variable error
                RhinoApp.WriteLine($"Error baking geometry: {ex.Message}");
                throw; // Re-throw the exception to be caught by the caller
            }
        }
    }
} 