using System;
using System.Windows.Forms;
using System.Drawing;

namespace RhinoM8
{
    public class LLMSettingsForm : DarkForm
    {
        private NumericUpDown temperatureInput;
        private NumericUpDown maxTokensInput;
        private TextBox systemPromptInput;
        private Button saveButton;
        private Button cancelButton;

        public class LLMSettings
        {
            public double Temperature { get; set; }
            public int MaxTokens { get; set; }
            public string SystemPrompt { get; set; }
            public string OpenAIKey { get; set; }
            public string ClaudeKey { get; set; }
            public string GrokKey { get; set; }
            public string MeshyKey { get; set; }
        }

        public LLMSettings Settings { get; private set; }

        public LLMSettingsForm(LLMSettings currentSettings)
        {
            Settings = new LLMSettings
            {
                Temperature = currentSettings.Temperature,
                MaxTokens = currentSettings.MaxTokens,
                SystemPrompt = currentSettings.SystemPrompt,
                OpenAIKey = currentSettings.OpenAIKey,
                ClaudeKey = currentSettings.ClaudeKey,
                GrokKey = currentSettings.GrokKey,
                MeshyKey = currentSettings.MeshyKey
            };
            
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Text = "Settings";
            this.Size = new Size(800, 1000);  // Increased from 900 to 1000
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(32, 32, 32);
            this.ForeColor = Color.White;

            // Temperature
            var tempLabel = new Label
            {
                Text = "Temperature:",
                Location = new Point(20, 20),
                Size = new Size(200, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f)
            };

            temperatureInput = new NumericUpDown
            {
                Location = new Point(240, 20),
                Size = new Size(120, 30),
                DecimalPlaces = 1,
                Increment = 0.1m,
                Minimum = 0.0m,
                Maximum = 1.0m,
                Value = (decimal)Settings.Temperature,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f)
            };

            // Max Tokens
            var tokensLabel = new Label
            {
                Text = "Max Tokens:",
                Location = new Point(20, 60),
                Size = new Size(200, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f)
            };

            maxTokensInput = new NumericUpDown
            {
                Location = new Point(240, 60),
                Size = new Size(160, 30),
                Minimum = 100,
                Maximum = 4000,
                Value = Settings.MaxTokens,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f)
            };

            // System Prompt
            var promptLabel = new Label
            {
                Text = "System Prompt:",
                Location = new Point(20, 100),
                Size = new Size(200, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f)
            };

            systemPromptInput = new TextBox
            {
                Location = new Point(20, 130),
                Size = new Size(740, 200),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Text = Settings.SystemPrompt,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f)
            };

            // OpenAI API Key
            var openAIInput = new TextBox
            {
                Location = new Point(20, 370),
                Size = new Size(740, 30),
                Text = Settings.OpenAIKey,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                PasswordChar = '•'
            };

            // Claude API Key
            var claudeInput = new TextBox
            {
                Location = new Point(20, 440),
                Size = new Size(740, 30),
                Text = Settings.ClaudeKey,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                PasswordChar = '•'
            };

            // Grok API Key
            var grokInput = new TextBox
            {
                Location = new Point(20, 510),
                Size = new Size(740, 30),
                Text = Settings.GrokKey,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                PasswordChar = '•'
            };

            // Meshy API Key
            var meshyInput = new TextBox
            {
                Location = new Point(20, 620),
                Size = new Size(740, 30),
                Text = Settings.MeshyKey,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                PasswordChar = '•'
            };

            // Move warning label down
            var warningLabel = new Label
            {
                Text = "Note: API keys will be stored in Rhino's settings. Keep your keys secure and don't share them.",
                Location = new Point(20, 700),
                Size = new Size(740, 80),
                ForeColor = Color.Red,
                Font = new Font("Segoe UI", 9f)
            };

            // Move buttons down
            saveButton = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                Location = new Point(480, 830),  // Adjusted from 730
                Size = new Size(120, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f)
            };

            cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(640, 830),  // Adjusted from 730
                Size = new Size(120, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f)
            };

            // Add hover effects
            saveButton.MouseEnter += (s, e) => saveButton.BackColor = Color.FromArgb(70, 70, 70);
            saveButton.MouseLeave += (s, e) => saveButton.BackColor = Color.FromArgb(60, 60, 60);
            cancelButton.MouseEnter += (s, e) => cancelButton.BackColor = Color.FromArgb(70, 70, 70);
            cancelButton.MouseLeave += (s, e) => cancelButton.BackColor = Color.FromArgb(60, 60, 60);

            // Handle save button click
            saveButton.Click += (s, e) =>
            {
                Settings.Temperature = (double)temperatureInput.Value;
                Settings.MaxTokens = (int)maxTokensInput.Value;
                Settings.SystemPrompt = systemPromptInput.Text;
                Settings.OpenAIKey = openAIInput.Text;
                Settings.ClaudeKey = claudeInput.Text;
                Settings.GrokKey = grokInput.Text;
                Settings.MeshyKey = meshyInput.Text;
            };

            // Add all controls to the form
            this.Controls.AddRange(new Control[]
            {
                tempLabel,
                temperatureInput,
                tokensLabel,
                maxTokensInput,
                promptLabel,
                systemPromptInput,
                warningLabel,
                saveButton,
                cancelButton
            });

            // Set form properties
            this.AcceptButton = saveButton;
            this.CancelButton = cancelButton;
            this.TopMost = false;  // Keep window on top

            // OpenAI
            AddApiKeySection(
                "OpenAI API Key:", 
                "Get your API key from OpenAI's developer portal",
                "Get API key from OpenAI",
                "https://platform.openai.com/api-keys",
                new Point(20, 340),
                openAIInput,
                this.Controls
            );

            // Claude
            AddApiKeySection(
                "Anthropic API Key:", 
                "Get your API key from Anthropic's developer console",
                "Get API key from Anthropic",
                "https://console.anthropic.com/",
                new Point(20, 420),  // Adjusted position
                claudeInput,
                this.Controls
            );

            // Grok
            AddApiKeySection(
                "Grok API Key:", 
                "Get your API key from X.AI's developer portal",
                "Get API key from X.AI",
                "https://x.ai/api",
                new Point(20, 500),  // Adjusted position
                grokInput,
                this.Controls
            );

            // Meshy
            AddApiKeySection(
                "Meshy.ai API Key:", 
                "Get your API key from Meshy.ai developer portal",
                "Get API key from Meshy.ai",
                "www.meshy.ai?via=differential",
                new Point(20, 580),  // Adjusted position
                meshyInput,
                this.Controls
            );
        }

        private void AddApiKeySection(string labelText, string helpText, string linkText, string linkUrl, 
            Point labelLocation, TextBox inputBox, Control.ControlCollection controls)
        {
            // Main label
            var label = new Label
            {
                Text = labelText,
                Location = labelLocation,
                Size = new Size(200, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f)
            };

            // Help button
            var helpButton = new Button
            {
                Text = "?",
                Size = new Size(24, 24),
                Location = new Point(labelLocation.X + 210, labelLocation.Y + 3),  // Align with label
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8f),
                Cursor = Cursors.Help
            };

            // Update input box location to be below the label
            inputBox.Location = new Point(labelLocation.X, labelLocation.Y + 35);
            inputBox.Size = new Size(740, 30);

            // Create popup form
            var popup = new Form
            {
                Size = new Size(400, 130),
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                BackColor = Color.FromArgb(45, 45, 45),
                ShowInTaskbar = false,
                TopMost = true  // Make sure popup stays on top
            };

            // Add border to popup
            popup.Paint += (s, e) =>
            {
                var rect = new Rectangle(0, 0, popup.Width - 1, popup.Height - 1);
                e.Graphics.DrawRectangle(new Pen(Color.FromArgb(60, 60, 60)), rect);
            };

            var helpLabel = new Label
            {
                Text = helpText,
                Location = new Point(10, 10),
                Size = new Size(380, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f)
            };

            var linkLabel = new LinkLabel
            {
                Text = linkText,
                Location = new Point(10, 60),
                Size = new Size(380, 30),
                LinkColor = Color.LightBlue,
                ActiveLinkColor = Color.White,
                VisitedLinkColor = Color.LightBlue,
                Font = new Font("Segoe UI", 9f)
            };
            
            // Fix the link click handler to use ProcessStartInfo
            linkLabel.Click += (s, e) => 
            {
                try 
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = linkUrl,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Unable to open link: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            var closeButton = new Button
            {
                Text = "×",
                Size = new Size(24, 24),
                Location = new Point(popup.Width - 30, 5),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f)
            };
            closeButton.Click += (s, e) => popup.Hide();

            popup.Controls.AddRange(new Control[] { helpLabel, linkLabel, closeButton });

            // Show popup when help button is clicked
            helpButton.Click += (s, e) =>
            {
                var button = (Button)s;
                var location = button.PointToScreen(new Point(0, button.Height));
                // Adjust popup position to be next to the button
                popup.Location = new Point(location.X - popup.Width + button.Width, location.Y);
                popup.Show(this);  // Show popup with parent form as owner
            };

            // Hide popup when focus is lost
            popup.Deactivate += (s, e) => popup.Hide();

            // Add hover effects for help button
            helpButton.MouseEnter += (s, e) => helpButton.BackColor = Color.FromArgb(70, 70, 70);
            helpButton.MouseLeave += (s, e) => helpButton.BackColor = Color.FromArgb(60, 60, 60);

            // Add all controls to the form
            controls.AddRange(new Control[] { label, helpButton, inputBox });
            this.AddOwnedForm(popup);  // Add popup as owned form
        }
    }
}
