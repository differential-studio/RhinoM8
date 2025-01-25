using System;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using System.Threading.Tasks;  // Add this for Task support
using System.Threading;       // Add this for CancellationToken
using System.IO;
using System.Runtime.InteropServices;

namespace RhinoM8
{
    public class PersistentPromptForm : DarkForm
    {
        // Add mainPanel as a class field
        private TableLayoutPanel mainPanel;
        private TextBox promptTextBox;
        private ComboBox aiProviderComboBox;
        private Button submitButton;
        private Button addGeometryButton;
        private RichTextBox geometryTextBox;
        private RhinoM8Command command;
        private LLMSettingsForm.LLMSettings currentSettings;
        private SliderPanel sliderPanel;
        private string lastGeneratedScript;
        private CodeViewerForm _codeViewer;
        private Button debugButton;
        private string lastApiResponse;
        private string lastError;
        private Button uploadImageButton;
        private string selectedImagePath;
        private MeshyService meshyService;
        private string _openAiApiKey;
        private string _claudeApiKey;
        private string _grokKey;
        private string _meshyKey;
        private TableLayoutPanel buttonPanel;
        private bool hasShownTextToCodeDisclaimer = false;
        private Button uploadButton;
        private string selectedImageBase64;
        private TransparentTrackBar polycountSlider = null;
        private Form _aboutForm;

        // Add this class inside PersistentPromptForm.cs, before the main class
        private class TransparentTrackBar : TrackBar
        {
            public TransparentTrackBar()
            {
                this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
                this.BackColor = Color.Transparent;
            }
        }

        // Add this class inside the namespace, outside the form class
        internal static class DwmApi
        {
            [DllImport("dwmapi.dll")]
            private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

            private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
            private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
            private const int DWMWA_CAPTION_COLOR = 35;
            private const int DWMWA_TEXT_COLOR = 36;

            internal static bool UseImmersiveDarkMode(IntPtr handle, bool enabled)
            {
                if (Environment.OSVersion.Platform != PlatformID.Win32NT) return false;
                
                var attribute = DWMWA_USE_IMMERSIVE_DARK_MODE;
                int useImmersiveDarkMode = enabled ? 1 : 0;
                bool success = DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) >= 0;
                
                if (!success)
                {
                    attribute = DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
                    success = DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) >= 0;
                }
                
                return success;
            }

            internal static void SetDarkTitleBar(IntPtr handle)
            {
                if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;

                // Set caption (title bar) color to dark
                int darkColor = ColorToInt(Color.FromArgb(32, 32, 32));
                DwmSetWindowAttribute(handle, DWMWA_CAPTION_COLOR, ref darkColor, sizeof(int));

                // Set text color to white
                int textColor = ColorToInt(Color.White);
                DwmSetWindowAttribute(handle, DWMWA_TEXT_COLOR, ref textColor, sizeof(int));
            }

            private static int ColorToInt(Color color)
            {
                return color.R | (color.G << 8) | (color.B << 16);
            }
        }

        // Add this class inside PersistentPromptForm.cs, near the top with other custom controls
        private class DarkComboBox : ComboBox
        {
            private const int WM_PAINT = 0xF;
            private readonly int buttonWidth = SystemInformation.HorizontalScrollBarArrowWidth;

            public DarkComboBox()
            {
                DrawMode = DrawMode.OwnerDrawFixed;
                DropDownStyle = ComboBoxStyle.DropDownList;
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);

                if (m.Msg == WM_PAINT && DropDownStyle != ComboBoxStyle.Simple)
                {
                    using (var g = Graphics.FromHwnd(Handle))
                    {
                        // Draw the dark background for the button area using the same color as the ComboBox
                        var buttonRect = new Rectangle(
                            Width - buttonWidth - 1,
                            1,
                            buttonWidth,
                            Height - 2
                        );

                        // Use the same background color as the ComboBox (45, 45, 45)
                        g.FillRectangle(
                            new SolidBrush(Color.FromArgb(45, 45, 45)),
                            buttonRect
                        );

                        // Draw the arrow
                        var arrowColor = Color.FromArgb(200, 200, 200);
                        var points = new Point[]
                        {
                            new Point(buttonRect.Left + (buttonRect.Width / 4), buttonRect.Top + (buttonRect.Height / 3)),
                            new Point(buttonRect.Right - (buttonRect.Width / 4), buttonRect.Top + (buttonRect.Height / 3)),
                            new Point(buttonRect.Left + (buttonRect.Width / 2), buttonRect.Bottom - (buttonRect.Height / 3))
                        };

                        g.FillPolygon(
                            new SolidBrush(arrowColor),
                            points
                        );
                    }
                }
            }
        }

        public PersistentPromptForm(RhinoM8Command cmd)
        {
            command = cmd;
            
            // Load saved settings and set API keys
            currentSettings = RhinoM8Plugin.Instance.LoadSettings();
            _openAiApiKey = currentSettings.OpenAIKey;
            _claudeApiKey = currentSettings.ClaudeKey;
            _grokKey = currentSettings.GrokKey;
            _meshyKey = currentSettings.MeshyKey;
            
            command.UpdateLLMSettings(currentSettings);
            
            InitializeComponents();
            
            // Load the most recent script from history
            var history = RhinoM8Plugin.Instance.GetAllHistory();
            if (history.Any())
            {
                var latestEntry = history.First();
                if (latestEntry is CodeHistoryEntry codeEntry)
                {
                    lastGeneratedScript = codeEntry.Code;
                    // Create the code viewer with the latest script and pass 'this' as parent
                    _codeViewer = new CodeViewerForm(lastGeneratedScript, sliderPanel, this);
                }
                else
                {
                    // Create empty code viewer and pass 'this' as parent
                    _codeViewer = new CodeViewerForm("", sliderPanel, this);
                }
            }
            else
            {
                // Create empty code viewer and pass 'this' as parent
                _codeViewer = new CodeViewerForm("", sliderPanel, this);
            }
            
            // Set form properties for persistent window
            this.ShowInTaskbar = false;
            this.MinimumSize = new Size(800, 600);
            this.Size = new Size(800, 800);
            this.StartPosition = FormStartPosition.Manual;
            
            // Position the window on the right side of the screen
            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(screen.Right - this.Width - 20, screen.Top + 100);

            meshyService = new MeshyService(currentSettings.MeshyKey);
        }

        private void InitializeComponents()
        {
            this.Text = "RhinoM8";

            // Create main container panel
            mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5,  // Increase row count to accommodate polycount panel
                ColumnCount = 1,
                BackColor = Color.FromArgb(32, 32, 32),
                Padding = new Padding(10)
            };

            // Configure row styles with proportions
            mainPanel.RowStyles.Clear();
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));    // Top controls row
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));    // Prompt area
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));    // Buttons row
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));    // Polycount panel row
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));     // Geometry area - initially collapsed

            // Top controls panel
            var topPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.FromArgb(32, 32, 32),
                Height = 45
            };
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45F));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45F));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45F));

            // AI Provider ComboBox
            aiProviderComboBox = new DarkComboBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f),
                IntegralHeight = false
            };

            // Add custom drawing for the dropdown button
            aiProviderComboBox.Paint += (s, e) =>
            {
                if (aiProviderComboBox.Items.Count > 0)
                {
                    var comboBox = (ComboBox)s;
                    var width = SystemInformation.HorizontalScrollBarArrowWidth;
                    var rect = new Rectangle(
                        comboBox.Width - width - 2,
                        2,
                        width,
                        comboBox.Height - 4
                    );

                    // Draw the dark background for the button area
                    e.Graphics.FillRectangle(
                        new SolidBrush(Color.FromArgb(35, 35, 35)),
                        rect
                    );

                    // Draw the arrow
                    var arrowColor = Color.FromArgb(200, 200, 200);
                    var points = new Point[]
                    {
                        new Point(rect.Left + (rect.Width / 4), rect.Top + (rect.Height / 3)),
                        new Point(rect.Right - (rect.Width / 4), rect.Top + (rect.Height / 3)),
                        new Point(rect.Left + (rect.Width / 2), rect.Bottom - (rect.Height / 3))
                    };

                    e.Graphics.FillPolygon(
                        new SolidBrush(arrowColor),
                        points
                    );
                }
            };

            // Set dropdown height
            aiProviderComboBox.DropDown += (s, e) =>
            {
                var comboBox = (ComboBox)s;
                int itemHeight = comboBox.ItemHeight;
                int totalHeight = itemHeight * comboBox.Items.Count + 4;
                comboBox.DropDownHeight = totalHeight * 2;
            };

            // Add tooltip for the ComboBox
            var toolTip = new ToolTip();
            toolTip.SetToolTip(aiProviderComboBox, "Pick your AI model");

            // Add items with group information
            aiProviderComboBox.Items.AddRange(new object[]
            {
                new LLMOption("Text-to-3D", null, true),    // Group header
                new LLMOption("Meshy-3D", "Text-to-3D"),
                new LLMOption("Image-to-3D", "Text-to-3D"),  // New option
                new LLMOption("Text-to-Code (experimental, works only for simple geometries)", null, true),
                new LLMOption("OpenAI", "Text-to-Code"),
                new LLMOption("Claude", "Text-to-Code"),
                new LLMOption("Grok", "Text-to-Code")
            });

            // Set default selection
            aiProviderComboBox.SelectedIndex = 1; // Select Meshy-3D by default

            aiProviderComboBox.SelectedIndexChanged += (s, e) =>
            {
                var selectedOption = aiProviderComboBox.SelectedItem as LLMOption;
                // If a group header is selected, revert to previous selection
                if (selectedOption?.IsGroupHeader == true)
                {
                    // Prevent the selection of group headers
                    aiProviderComboBox.SelectedIndex = Math.Max(aiProviderComboBox.SelectedIndex + 1, 1);
                    return;
                }
                
                if (selectedOption?.Group == "Text-to-Code" && !hasShownTextToCodeDisclaimer)
                {
                    ShowTextToCodeDisclaimer();
                    hasShownTextToCodeDisclaimer = true;
                }
                UpdateUIForProvider(selectedOption);
            };

            // Update the DrawItem event handler
            aiProviderComboBox.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;

                var item = aiProviderComboBox.Items[e.Index] as LLMOption;
                if (item == null) return;

                // Draw background - same color for all items
                if ((e.State & DrawItemState.Selected) == DrawItemState.Selected && !item.IsGroupHeader)
                {
                    e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(60, 60, 60)), e.Bounds);
                }
                else
                {
                    e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(45, 45, 45)), e.Bounds);
                }

                var bounds = new Rectangle(
                    e.Bounds.X + 1,
                    e.Bounds.Y + 1,
                    e.Bounds.Width - 2,
                    e.Bounds.Height - 2
                );

                // Draw text - different color for headers
                using (var brush = new SolidBrush(item.IsGroupHeader ? Color.Gray : Color.White))
                {
                    e.Graphics.DrawString(
                        item.Name,
                        e.Font,
                        brush,
                        bounds,
                        new StringFormat { 
                            Trimming = StringTrimming.EllipsisCharacter,
                            LineAlignment = StringAlignment.Center
                        }
                    );
                }

                // Only draw focus rectangle for non-header items
                if (!item.IsGroupHeader && (e.State & DrawItemState.Focus) == DrawItemState.Focus)
                {
                    e.DrawFocusRectangle();
                }
            };

            // Code Editor Button
            var codeEditorButton = new Button
            {
                Text = "📝",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12f)
            };
            toolTip.SetToolTip(codeEditorButton, "Archive");
            codeEditorButton.Click += CodeEditorButton_Click;

            // Settings Button
            var settingsButton = new Button
            {
                Text = "⚙",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12f)
            };
            toolTip.SetToolTip(settingsButton, "Settings");
            settingsButton.Click += (s, e) => ShowSettingsDialog();

            // About Button
            var aboutButton = new Button
            {
                Text = "?",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12f)
            };
            toolTip.SetToolTip(aboutButton, "About");
            aboutButton.Click += AboutButton_Click;

            // Add controls to top panel
            topPanel.Controls.Add(aiProviderComboBox, 0, 0);
            topPanel.Controls.Add(codeEditorButton, 1, 0);
            topPanel.Controls.Add(settingsButton, 2, 0);
            topPanel.Controls.Add(aboutButton, 3, 0);

            // Prompt TextBox
            promptTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.None,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.Gray,  // Start with gray color for placeholder
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                AcceptsReturn = true,
                AcceptsTab = true,
                Text = "Type here your text prompt..."  // Default placeholder text
            };

            // Add placeholder behavior
            promptTextBox.GotFocus += (s, e) =>
            {
                if (promptTextBox.Text == "Type here your text prompt...")
                {
                    promptTextBox.Text = "";
                    promptTextBox.ForeColor = Color.White;
                }
            };

            promptTextBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(promptTextBox.Text))
                {
                    promptTextBox.Text = "Type here your text prompt...";
                    promptTextBox.ForeColor = Color.Gray;
                }
            };

            // Add KeyDown event handler to promptTextBox
            promptTextBox.KeyDown += (s, e) =>
            {
                // Check if Enter is pressed without Shift (Shift+Enter will still create a new line)
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;  // Prevent the beep sound
                    submitButton.PerformClick(); // Trigger the submit button click
                }
            };

            // Buttons Panel
            buttonPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Color.FromArgb(32, 32, 32)
            };
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45F));
            buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45F));

            // Submit Button
            submitButton = new Button
            {
                Text = "Generate",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 120, 60),
                ForeColor = Color.White
            };
            submitButton.Click += SubmitButton_Click;

            // Debug Button
            debugButton = new Button
            {
                Text = "Debug Last",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Enabled = false
            };
            debugButton.Click += DebugButton_Click;
            toolTip.SetToolTip(debugButton, "Click debug in case of the script execution error");

            // Add/Clear Geometry Buttons
            addGeometryButton = new Button
            {
                Text = "+",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            addGeometryButton.Click += AddGeometryButton_Click;
            toolTip.SetToolTip(addGeometryButton, "Click to add reference geometry");

            var clearGeometryButton = new Button
            {
                Text = "-",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };
            clearGeometryButton.Click += ClearGeometryButton_Click;
            toolTip.SetToolTip(clearGeometryButton, "Click to remove reference geometry");

            // Add buttons to panel (remove any reference to loading label in button panel)
            buttonPanel.Controls.Add(submitButton, 0, 0);
            buttonPanel.Controls.Add(debugButton, 1, 0);
            buttonPanel.Controls.Add(addGeometryButton, 2, 0);
            buttonPanel.Controls.Add(clearGeometryButton, 3, 0);

            // Geometry TextBox - set initially invisible
            geometryTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10f),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Visible = false  // Initially hidden
            };

            // Add upload image button (initially hidden)
            uploadImageButton = new Button
            {
                Text = "Image",
                Visible = false,
                Dock = DockStyle.None,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                Size = new Size(100, 30),
                Location = new Point(promptTextBox.Right - 110, promptTextBox.Bottom + 10)
            };
            uploadImageButton.Click += UploadImage_Click;

            // Add handler for provider selection change
            aiProviderComboBox.SelectedIndexChanged += (s, e) =>
            {
                var selectedOption = aiProviderComboBox.SelectedItem as LLMOption;
                if (selectedOption?.Group == "Text-to-Code" && !hasShownTextToCodeDisclaimer)
                {
                    ShowTextToCodeDisclaimer();
                    hasShownTextToCodeDisclaimer = true;  // Set flag after showing disclaimer
                }
                UpdateUIForProvider(selectedOption);
            };

            // Call it initially to set correct state
            UpdateUIForProvider(aiProviderComboBox.SelectedItem as LLMOption);

            // Add the upload button to the form
            this.Controls.Add(uploadImageButton);

            // Initialize MeshyService
            meshyService = new MeshyService(currentSettings.MeshyKey);

            // Add all elements to main panel
            mainPanel.Controls.Add(topPanel, 0, 0);
            mainPanel.Controls.Add(promptTextBox, 0, 1);
            mainPanel.Controls.Add(buttonPanel, 0, 2);
            mainPanel.Controls.Add(geometryTextBox, 0, 3);

            // Initialize sliderPanel as a separate window
            sliderPanel = new SliderPanel();
            sliderPanel.Owner = this;  // Set owner explicitly
            
            // Set initial size and position
            sliderPanel.Width = this.Width;
            sliderPanel.Location = new Point(
                this.Location.X,
                this.Location.Y + this.Height + 5
            );

            // Remove the container panel code and directly add mainPanel to the form
            this.Controls.Add(mainPanel);

            // Position the slider panel relative to the main form
            this.LocationChanged += (s, e) =>
            {
                if (sliderPanel.Visible)
                {
                    sliderPanel.Location = new Point(
                        this.Location.X,
                        this.Location.Y + this.Height + 5  // Position below with 5px gap
                    );
                    sliderPanel.Width = this.Width;  // Match width only, don't affect height
                }
            };

            // Update the SizeChanged event handler
            this.SizeChanged += (s, e) =>
            {
                if (sliderPanel.Visible)
                {
                    sliderPanel.Width = this.Width;  // Match width only, don't affect height
                }
            };

            // Add hover effects for buttons
            foreach (Button btn in new[] { submitButton, debugButton, addGeometryButton, clearGeometryButton, codeEditorButton, settingsButton, aboutButton })
            {
                btn.MouseEnter += (s, e) => { if (btn.Enabled) btn.BackColor = Color.FromArgb(70, 70, 70); };
                btn.MouseLeave += (s, e) => { if (btn.Enabled) btn.BackColor = Color.FromArgb(60, 60, 60); };
            }

            // Configure tooltip
            toolTip.AutoPopDelay = 5000;
            toolTip.InitialDelay = 500;
            toolTip.ReshowDelay = 200;
            toolTip.ShowAlways = true;

            // Add selection change handler
            aiProviderComboBox.SelectedIndexChanged += AiProviderComboBox_SelectedIndexChanged;
        }

        private string GetGeometryDescription(Rhino.DocObjects.RhinoObject obj)
        {
            if (obj == null)
            {
                RhinoApp.WriteLine("Null object received");
                return null;
            }

            var geometry = obj.Geometry;
            if (geometry == null)
            {
                RhinoApp.WriteLine("Null geometry in object");
                return null;
            }

            RhinoApp.WriteLine($"Processing geometry type: {geometry.GetType().Name}");

            try
            {
                if (geometry is Rhino.Geometry.Point point)
                {
                    var pt = point.Location;
                    return $"Point({pt.X:F2}, {pt.Y:F2}, {pt.Z:F2})";
                }
                else if (geometry is Rhino.Geometry.LineCurve lineCurve)
                {
                    var line = lineCurve.Line;
                    return $"Line: From({line.From.X:F2}, {line.From.Y:F2}, {line.From.Z:F2}) To({line.To.X:F2}, {line.To.Y:F2}, {line.To.Z:F2})";
                }
                else if (geometry is Rhino.Geometry.ArcCurve arcCurve && arcCurve.IsCircle())
                {
                    var arc = arcCurve.Arc;
                    return $"Circle: Center({arc.Center.X:F2}, {arc.Center.Y:F2}, {arc.Center.Z:F2}), Radius({arc.Radius:F2})";
                }
                else if (geometry is Rhino.Geometry.Curve curve)
                {
                    var bbox = curve.GetBoundingBox(true);
                    return $"Curve: BoundingBox(min({bbox.Min.X:F2}, {bbox.Min.Y:F2}, {bbox.Min.Z:F2}), max({bbox.Max.X:F2}, {bbox.Max.Y:F2}, {bbox.Max.Z:F2}))";
                }
                else if (geometry is Rhino.Geometry.Mesh mesh)
                {
                    return $"Mesh with {mesh.Vertices.Count} vertices";
                }
                else if (geometry is Rhino.Geometry.Surface surface)
                {
                    var bbox = surface.GetBoundingBox(true);
                    return $"Surface: BoundingBox(min({bbox.Min.X:F2}, {bbox.Min.Y:F2}, {bbox.Min.Z:F2}), max({bbox.Max.X:F2}, {bbox.Max.Y:F2}, {bbox.Max.Z:F2}))";
                }
                else if (geometry is Rhino.Geometry.Brep brep)
                {
                    var bbox = brep.GetBoundingBox(true);
                    return $"Polysurface: BoundingBox(min({bbox.Min.X:F2}, {bbox.Min.Y:F2}, {bbox.Min.Z:F2}), max({bbox.Max.X:F2}, {bbox.Max.Y:F2}, {bbox.Max.Z:F2}))";
                }

                RhinoApp.WriteLine($"Unhandled geometry type: {geometry.GetType().Name}");
                return null;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error processing geometry: {ex.Message}");
                return null;
            }
        }

        private void AddGeometryButton_Click(object sender, EventArgs e)
        {
            var doc = RhinoDoc.ActiveDoc;
            if (doc == null) return;

            RhinoApp.WriteLine("Add Geometry button clicked");

            // Try multiple methods to get selected objects
            var selectedObjects = doc.Objects.GetSelectedObjects(false, true);
            
            if (selectedObjects == null || !selectedObjects.Any())
            {
                // Try alternative method using IsSelected properly
                var selected = from obj in doc.Objects
                              where obj.IsSelected(true) == 1  // IsSelected returns an int, 1 means selected
                              select obj;
                selectedObjects = selected.ToList();
            }

            if (selectedObjects == null || !selectedObjects.Any())
            {
                // Try one more method
                var objectIds = doc.Objects.GetSelectedObjects(true, true)
                    .Select(obj => obj.Id)
                    .ToList();

                if (objectIds.Any())
                {
                    selectedObjects = objectIds
                        .Select(id => doc.Objects.Find(id))
                        .Where(obj => obj != null)
                        .ToList();
                }
            }

            if (selectedObjects == null || !selectedObjects.Any())
            {
                RhinoApp.WriteLine("No objects selected");
                MessageBox.Show("Please select one or more objects first.", "No Selection", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            RhinoApp.WriteLine($"Found {selectedObjects.Count()} selected objects");

            var descriptions = new List<string>();
            foreach (var obj in selectedObjects)
            {
                try
                {
                    var geometry = obj.Geometry;
                    string description = null;

                    if (geometry is Rhino.Geometry.Point point)
                    {
                        var pt = point.Location;
                        description = $"Point at ({pt.X:F2}, {pt.Y:F2}, {pt.Z:F2})";
                    }
                    else if (geometry is Rhino.Geometry.Curve curve)
                    {
                        if (curve.IsClosed)
                            description = "Closed curve";
                        else
                            description = "Curve";

                        var bbox = curve.GetBoundingBox(true);
                        description += $" with bounds: min({bbox.Min.X:F2}, {bbox.Min.Y:F2}, {bbox.Min.Z:F2}), max({bbox.Max.X:F2}, {bbox.Max.Y:F2}, {bbox.Max.Z:F2})";
                    }
                    else if (geometry is Rhino.Geometry.Brep brep)
                    {
                        description = $"Surface/Brep with {brep.Faces.Count} faces";
                    }
                    else if (geometry is Rhino.Geometry.Mesh mesh)
                    {
                        description = $"Mesh with {mesh.Vertices.Count} vertices";
                    }
                    else
                    {
                        description = $"Geometry type: {geometry.GetType().Name}";
                    }

                    if (description != null)
                    {
                        descriptions.Add(description);
                        RhinoApp.WriteLine($"Added description: {description}");
                    }
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"Error processing object: {ex.Message}");
                }
            }

            if (descriptions.Count > 0)
            {
                // Show the geometry textbox and adjust layout
                this.SuspendLayout();
                try
                {
                    // Adjust the row style for geometry area
                    mainPanel.RowStyles[1] = new RowStyle(SizeType.Percent, 70F);  // Prompt area
                    mainPanel.RowStyles[3] = new RowStyle(SizeType.Percent, 30F);  // Geometry area
                    
                    geometryTextBox.Visible = true;
                    
                    string currentText = geometryTextBox.Text.Trim();
                    string newDescriptions = string.Join("\n", descriptions);
                    
                    if (string.IsNullOrEmpty(currentText))
                    {
                        geometryTextBox.Text = newDescriptions;
                    }
                    else
                    {
                        geometryTextBox.Text = currentText + "\n" + newDescriptions;
                    }

                    geometryTextBox.SelectionStart = geometryTextBox.Text.Length;
                    geometryTextBox.ScrollToCaret();
                }
                finally
                {
                    this.ResumeLayout(true);
                }
            }
        }

        private void ClearGeometryButton_Click(object sender, EventArgs e)
        {
            this.SuspendLayout();
            try
            {
                // Collapse the geometry area
                mainPanel.RowStyles[1] = new RowStyle(SizeType.Percent, 100F);  // Prompt area takes full space
                mainPanel.RowStyles[3] = new RowStyle(SizeType.Absolute, 0F);   // Collapse geometry area
                
                geometryTextBox.Clear();
                geometryTextBox.Visible = false;
            }
            finally
            {
                this.ResumeLayout(true);
            }
        }

        private async void SubmitButton_Click(object sender, EventArgs e)
        {
            var selectedOption = aiProviderComboBox.SelectedItem as LLMOption;
            if (selectedOption?.Name == "Image-to-3D")
            {
                if (string.IsNullOrEmpty(selectedImageBase64))
                {
                    DarkForm.ShowDialog("Image Required", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    // Disable controls during processing
                    submitButton.Enabled = false;
                    aiProviderComboBox.Enabled = false;
                    promptTextBox.ReadOnly = true;

                    // Create progress handler
                    var progress = new Progress<int>(percent =>
                    {
                        // Update UI with progress
                        promptTextBox.Text = $"Processing image: {percent}% complete...";
                        Application.DoEvents(); // Ensure UI updates
                    });

                    // Use the complete processing method
                    await meshyService.ProcessImageToMeshComplete(
                        selectedImageBase64,
                        Path.GetFileNameWithoutExtension(selectedImagePath),
                        progress
                    );

                    promptTextBox.Text = "Model successfully imported!";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error processing image: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    promptTextBox.Text = "Error occurred during processing.";
                }
                finally
                {
                    // Re-enable controls
                    submitButton.Enabled = true;
                    aiProviderComboBox.Enabled = true;
                    promptTextBox.ReadOnly = false;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(promptTextBox.Text))
                {
                    MessageBox.Show("Please enter a prompt.", "Input Required", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (selectedOption == null || selectedOption.IsGroupHeader)
                {
                    MessageBox.Show("Please select a valid AI provider.", "Invalid Selection", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string originalPrompt = promptTextBox.Text;
                string rhinoScript = null;  // Declare rhinoScript here

                // Disable controls
                submitButton.Enabled = false;
                debugButton.Enabled = false;
                aiProviderComboBox.Enabled = false;

                // Show loading in the prompt text box
                promptTextBox.ReadOnly = true;
                promptTextBox.Text = selectedOption.Name == "Meshy-3D" ? 
                    "Generating 3D model...\nPlease wait..." : 
                    "Generating...\nPlease wait...";

                try
                {
                    string fullPrompt = originalPrompt;
                    if (!string.IsNullOrEmpty(geometryTextBox.Text))
                    {
                        fullPrompt += "\nSelected geometry:\n" + geometryTextBox.Text;
                    }

                    // Add progress handler for Meshy-3D
                    if (selectedOption.Name == "Meshy-3D")
                    {
                        var progress = new Progress<int>(percent =>
                        {
                            promptTextBox.Text = $"Generating 3D model: {percent}% complete. Please wait...";
                        });
                        
                        rhinoScript = await command.ExecutePrompt(fullPrompt, selectedOption.Name, progress);
                    }
                    else
                    {
                        rhinoScript = await command.ExecutePrompt(fullPrompt, selectedOption.Name);
                    }
                    
                    if (!string.IsNullOrEmpty(rhinoScript))
                    {
                        lastGeneratedScript = rhinoScript;
                        lastApiResponse = rhinoScript;
                        lastError = null;

                        // Execute the script immediately for text-to-code providers
                        if (selectedOption.Group == "Text-to-Code")
                        {
                            RhinoApp.WriteLine("Executing script from API response...");
                            
                            // Update slider panel first
                            sliderPanel.UpdateFromScript(rhinoScript);
                            ShowSliderPanel();

                            // Trigger bake through slider panel
                            await Task.Delay(100); // Small delay to ensure UI is updated
                            try 
                            {
                                sliderPanel.BakeGeometry(rhinoScript);
                            }
                            catch (Exception ex)
                            {
                                lastError = $"Original Prompt:\n{promptTextBox.Text}\n\n" +
                                           $"Error Details:\n{ex.Message}\n\n" +
                                           $"Generated Script:\n{rhinoScript}";
                                debugButton.Enabled = true;
                                MessageBox.Show($"Error: {ex.Message}", "Error", 
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }

                            var historyEntry = new CodeHistoryEntry(
                                fullPrompt,
                                rhinoScript,
                                selectedOption.Name
                            );
                            RhinoM8Plugin.Instance.AddToHistory(historyEntry);

                            if (_codeViewer != null && !_codeViewer.IsDisposed)
                            {
                                _codeViewer.UpdateCode(rhinoScript);
                                _codeViewer.LoadCodeHistory();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    lastError = $"Original Prompt:\n{originalPrompt}\n\n" +
                               $"Error Details:\n{ex.Message}\n\n" +
                               $"Generated Script:\n{lastGeneratedScript}\n\n" +
                               $"API Response:\n{lastApiResponse}";
                    
                    debugButton.Enabled = true;
                    MessageBox.Show($"Error: {ex.Message}", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    // Re-enable controls
                    submitButton.Enabled = true;
                    debugButton.Enabled = true;
                    aiProviderComboBox.Enabled = true;
                    promptTextBox.ReadOnly = false;
                    
                    // Restore original prompt
                    promptTextBox.Text = originalPrompt;
                }
            }
        }

        private async void DebugButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(lastError))
            {
                MessageBox.Show("No error information available to debug.", "Debug Info Missing", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            debugButton.Enabled = false;
            submitButton.Enabled = false;
            try
            {
                string debugPrompt = $"Fix the following code that failed:\n\n{lastError}\n\n" +
                                   "Please provide a corrected version that follows all rules and handles the error case.";

                string rhinoScript = await command.ExecutePrompt(debugPrompt, 
                    aiProviderComboBox.SelectedItem.ToString());
                
                if (!string.IsNullOrEmpty(rhinoScript))
                {
                    lastGeneratedScript = rhinoScript;
                    lastApiResponse = rhinoScript;
                    lastError = null;
                    sliderPanel.UpdateFromScript(rhinoScript);
                    ShowSliderPanel();
                }
            }
            catch (Exception ex)
            {
                lastError = $"Debug Error:\n{ex.Message}\n\nPrevious Error:\n{lastError}";
                MessageBox.Show($"Debug failed: {ex.Message}", "Debug Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                debugButton.Enabled = true;
                submitButton.Enabled = true;
            }
        }

        public void ShowPromptWindow()
        {
            if (!this.Visible)
            {
                this.Show();
                this.BringToFront();
            }
        }

        private void ShowSettingsDialog()
        {
            // Load existing settings
            var existingSettings = RhinoM8Plugin.Instance.LoadSettings();
            
            using (var settingsForm = new LLMSettingsForm(existingSettings))
            {
                if (settingsForm.ShowDialog(this) == DialogResult.OK)
                {
                    currentSettings = settingsForm.Settings;
                    command.UpdateLLMSettings(currentSettings);
                    // Save the settings
                    RhinoM8Plugin.Instance.SaveSettings(currentSettings);
                }
            }
        }

        private void CodeEditorButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (_codeViewer == null || _codeViewer.IsDisposed)
                {
                    _codeViewer = new CodeViewerForm(lastGeneratedScript ?? "", sliderPanel, this);
                }
                
                if (!_codeViewer.Visible)
                {
                    _codeViewer.Show();
                }
                _codeViewer.BringToFront();
                _codeViewer.WindowState = FormWindowState.Normal;
                
                if (!string.IsNullOrEmpty(lastGeneratedScript))
                {
                    _codeViewer.UpdateCode(lastGeneratedScript);
                }
                
                _codeViewer.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening script viewer: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void UpdateFromHistory(CodeHistoryEntry entry)
        {
            lastGeneratedScript = entry.Code;
            promptTextBox.Text = entry.Prompt;
        }

        // Update the slider panel visibility handling
        private void ShowSliderPanel()
        {
            if (!sliderPanel.Visible)
            {
                sliderPanel.Location = new Point(
                    this.Location.X,
                    this.Location.Y + this.Height + 5  // Position below with 5px gap
                );
                sliderPanel.Width = this.Width;  // Match width
                sliderPanel.Show();
            }
            sliderPanel.BringToFront();
        }

        private void UploadImage_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";
                openFileDialog.Title = "Select an Image";
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Store the full path
                        selectedImagePath = openFileDialog.FileName;

                        // Read the file and convert to Base64
                        byte[] imageBytes = File.ReadAllBytes(selectedImagePath);
                        selectedImageBase64 = Convert.ToBase64String(imageBytes);

                        // Create the data URI
                        string fileExtension = Path.GetExtension(selectedImagePath).ToLower();
                        string mimeType;
                        
                        switch (fileExtension)
                        {
                            case ".png":
                                mimeType = "image/png";
                                break;
                            case ".jpg":
                            case ".jpeg":
                                mimeType = "image/jpeg";
                                break;
                            case ".bmp":
                                mimeType = "image/bmp";
                                break;
                            default:
                                mimeType = "image/png";
                                break;
                        }

                        // Store the complete data URI
                        selectedImageBase64 = $"data:{mimeType};base64,{selectedImageBase64}";

                        // Update UI to show selected file
                        promptTextBox.Text = $"Image ready: {Path.GetFileName(selectedImagePath)}";

                        // Update button states
                        uploadButton.BackColor = Color.FromArgb(60, 60, 60);  // Grey
                        submitButton.BackColor = Color.FromArgb(60, 120, 60); // Green
                        submitButton.Enabled = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading image: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        selectedImageBase64 = null;
                        selectedImagePath = null;
                    }
                }
            }
        }

        private async Task HandleSubmit()
        {
            var selected = aiProviderComboBox.SelectedItem as LLMOption;
            if (selected == null) return;

            ShowLoadingState(true);

            try
            {
                if (selected.Name == "Image-to-3D")
                {
                    if (string.IsNullOrEmpty(selectedImagePath))
                    {
                        MessageBox.Show("Please select an image first.", "Image Required",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var progress = new Progress<int>(percent =>
                    {
                        promptTextBox.Text = $"Generating 3D model: {percent}% complete. Please wait...";
                    });

                    await meshyService.GenerateModelFromPrompt(selectedImagePath, progress);
                }
                else if (selected.Name == "Meshy-3D")  // Add specific handling for Meshy-3D
                {
                    string prompt = promptTextBox.Text.Trim();
                    if (string.IsNullOrEmpty(prompt))
                    {
                        MessageBox.Show("Please enter a prompt.", "Prompt Required",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var progress = new Progress<int>(percent =>
                    {
                        promptTextBox.Text = $"Generating 3D model: {percent}% complete. Please wait...";
                        promptTextBox.Refresh();  // Force refresh of the label
                    });

                    await meshyService.GenerateModelFromPrompt(prompt, progress);
                }
                else  // Other LLM providers
                {
                    string prompt = promptTextBox.Text.Trim();
                    if (string.IsNullOrEmpty(prompt))
                    {
                        MessageBox.Show("Please enter a prompt.", "Prompt Required",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    promptTextBox.Text = "Generating...";
                    promptTextBox.Refresh();  // Force refresh of the label
                    await HandleTextPrompt(selected, prompt);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ShowLoadingState(false);
            }
        }

        private async Task HandleTextPrompt(LLMOption selected, string prompt)
        {
            try
            {
                string rhinoScript = await command.ExecutePrompt(prompt, selected.Name);
                if (!string.IsNullOrEmpty(rhinoScript))
                {
                    await Task.Run(() => 
                    {
                        Rhino.RhinoApp.RunScript(rhinoScript, false);
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private class LLMOption
        {
            public string Name { get; }
            public string Group { get; }
            public bool IsGroupHeader { get; }

            public LLMOption(string name, string group, bool isGroupHeader = false)
            {
                Name = name;
                Group = group;
                IsGroupHeader = isGroupHeader;
            }

            public override string ToString()
            {
                return Name;
            }
        }

        // Helper class to wrap the window handle (if not already defined)
        private class WindowWrapper : IWin32Window
        {
            public WindowWrapper(IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle { get; }
        }

        public new void Show()
        {
            var mainWindow = new WindowWrapper(Rhino.RhinoApp.MainWindowHandle());
            base.Show(mainWindow);
        }

        private void InitializeLoadingAnimation()
        {
            promptTextBox.Refresh();
        }

        private void ShowLoadingState(bool isLoading)
        {
            submitButton.Enabled = !isLoading;
            promptTextBox.Enabled = !isLoading;
            aiProviderComboBox.Enabled = !isLoading;
            
            if (isLoading)
                promptTextBox.Text = "Processing...";
            else
            {
                // Let UpdateUIForProvider handle the text instead of setting it here
                var selectedOption = aiProviderComboBox.SelectedItem as LLMOption;
                UpdateUIForProvider(selectedOption);
            }
        }

        // Add this method to handle UI elements visibility
        private void UpdateUIForProvider(LLMOption selectedOption)
        {
            if (selectedOption == null) return;

            // Clear existing column styles
            buttonPanel.ColumnStyles.Clear();

            // Hide all buttons initially
            debugButton.Visible = false;
            addGeometryButton.Visible = false;
            geometryTextBox.Visible = false;

            // Handle slider panel visibility
            if (selectedOption.Group != "Text-to-Code")
            {
                if (sliderPanel != null && sliderPanel.Visible)
                {
                    sliderPanel.Hide();
                }
            }

            // Handle prompt textbox state
            promptTextBox.ReadOnly = (selectedOption.Name == "Image-to-3D");
            
            // Set appropriate prompt text
            if (selectedOption.Name == "Image-to-3D")
            {
                promptTextBox.Text = "Please use the Upload Image button...";
                promptTextBox.BackColor = Color.FromArgb(35, 35, 35);
                
                // Set upload button to green and generate button to default
                if (uploadButton != null) uploadButton.BackColor = Color.FromArgb(60, 120, 60);
                submitButton.BackColor = Color.FromArgb(60, 60, 60);
            }
            else
            {
                // Only set default text if the current text is empty or was the image upload message
                if (string.IsNullOrWhiteSpace(promptTextBox.Text) || 
                    promptTextBox.Text == "Please use the Upload Image button..." ||
                    promptTextBox.Text.StartsWith("Selected image:") ||
                    promptTextBox.Text.StartsWith("Image ready:"))
                {
                    promptTextBox.Text = "Type here your text prompt...";
                }
                promptTextBox.BackColor = Color.FromArgb(45, 45, 45);
                
                // Set generate button to green and upload button to default (if it exists)
                submitButton.BackColor = Color.FromArgb(60, 120, 60);
                if (uploadButton != null) uploadButton.BackColor = Color.FromArgb(60, 60, 60);
            }

            // Find the clear geometry button (-) and hide it
            Button clearButton = null;
            foreach (Control control in buttonPanel.Controls)
            {
                if (control is Button btn && btn.Text == "-")
                {
                    btn.Visible = false;
                    clearButton = btn;
                    break;
                }
            }

            // Create upload button if it doesn't exist
            if (uploadButton == null)
            {
                uploadButton = new Button
                {
                    Name = "uploadButton",
                    Text = "Image",
                    Dock = DockStyle.Fill,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(60, 60, 60),
                    ForeColor = Color.White,
                    Visible = false
                };
                uploadButton.Click += UploadButton_Click;
                buttonPanel.Controls.Add(uploadButton);
            }

            // Configure layout based on provider type
            if (selectedOption.Group == "Text-to-Code")
            {
                // Text-to-Code: Generate, Debug, +, -
                buttonPanel.ColumnCount = 4;
                buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
                buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45F));
                buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45F));

                debugButton.Visible = true;
                addGeometryButton.Visible = true;
                if (clearButton != null) clearButton.Visible = true;
                geometryTextBox.Visible = true;
                uploadButton.Visible = false;
            }
            else if (selectedOption.Name == "Image-to-3D")
            {
                // Image-to-3D: Generate, Upload
                buttonPanel.ColumnCount = 2;
                buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));

                uploadButton.Visible = true;
            }
            else // Text-to-3D
            {
                // Text-to-3D: Generate only
                buttonPanel.ColumnCount = 1;
                buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                uploadButton.Visible = false;
            }

            // Update button positions
            buttonPanel.Controls.SetChildIndex(submitButton, 0);
            if (uploadButton.Visible)
            {
                buttonPanel.Controls.SetChildIndex(uploadButton, 1);
            }
            if (debugButton.Visible)
            {
                buttonPanel.Controls.SetChildIndex(debugButton, buttonPanel.ColumnCount - 3);
                buttonPanel.Controls.SetChildIndex(addGeometryButton, buttonPanel.ColumnCount - 2);
                if (clearButton != null)
                    buttonPanel.Controls.SetChildIndex(clearButton, buttonPanel.ColumnCount - 1);
            }

            // Force layout update
            buttonPanel.PerformLayout();

            bool isMeshyProvider = selectedOption?.Name == "Meshy-3D" || selectedOption?.Name == "Image-to-3D";
            
            if (isMeshyProvider)
            {
                if (polycountSlider == null)
                {
                    var polycountPanel = new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill,
                        Height = 55,
                        BackColor = Color.FromArgb(32, 32, 32),
                        Padding = new Padding(10, 10, 10, 10),
                        ColumnCount = 3
                    };

                    // Configure columns
                    polycountPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
                    polycountPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                    polycountPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));

                    var nameLabel = new Label
                    {
                        Text = "Target Polycount",
                        ForeColor = Color.White,
                        Font = new Font("Segoe UI", 9f),
                        AutoSize = true,
                        Anchor = AnchorStyles.Left | AnchorStyles.Top,
                        Margin = new Padding(0, 8, 0, 0)
                    };

                    var valueLabel = new Label
                    {
                        Text = "30,000",
                        ForeColor = Color.White,
                        TextAlign = ContentAlignment.MiddleRight,
                        Font = new Font("Segoe UI", 9f),
                        AutoSize = false,
                        Width = 90,
                        Anchor = AnchorStyles.Right | AnchorStyles.Top,
                        Margin = new Padding(0, 8, 0, 0)
                    };

                    polycountSlider = new TransparentTrackBar  // Use our custom TrackBar
                    {
                        Minimum = 100,
                        Maximum = 300000,
                        Value = 30000,
                        TickFrequency = 100,
                        LargeChange = 1000,
                        SmallChange = 100,
                        BackColor = Color.FromArgb(32, 32, 32),
                        Dock = DockStyle.Fill,
                        AutoSize = false,
                        Height = 35
                    };

                    polycountSlider.ValueChanged += (s, e) =>
                    {
                        // Snap to nearest 100
                        int snappedValue = ((polycountSlider.Value + 50) / 100) * 100;
                        if (polycountSlider.Value != snappedValue)
                        {
                            polycountSlider.Value = snappedValue;
                            return;
                        }
                        
                        valueLabel.Text = polycountSlider.Value.ToString("N0");
                        if (meshyService != null)
                        {
                            meshyService.SetTargetPolycount(polycountSlider.Value);
                        }
                    };

                    // Add tooltip
                    var tooltip = new ToolTip();
                    tooltip.SetToolTip(polycountSlider, 
                        "The valid value range varies depending on the user tier:\n\n" +
                        "Premium users: 100 to 300,000 (inclusive)\n" +
                        "Free users: 10,000 to 30,000 (inclusive)");
                    tooltip.InitialDelay = 100;
                    tooltip.AutoPopDelay = 10000;
                    tooltip.ReshowDelay = 100;

                    // Add controls to specific columns
                    polycountPanel.Controls.Add(nameLabel, 0, 0);    // Column 0
                    polycountPanel.Controls.Add(polycountSlider, 1, 0);  // Column 1
                    polycountPanel.Controls.Add(valueLabel, 2, 0);   // Column 2

                    // Add to main panel in the correct row
                    mainPanel.Controls.Add(polycountPanel, 0, 3);  // Column 0, Row 3
                    mainPanel.RowStyles[3] = new RowStyle(SizeType.Absolute, 100F);  // Match the new height
                }
                if (polycountSlider?.Parent != null)
                {
                    polycountSlider.Parent.Visible = true;
                    mainPanel.RowStyles[3] = new RowStyle(SizeType.Absolute, 100F);  // Match the new height
                }
            }
            else
            {
                if (polycountSlider?.Parent != null)
                {
                    polycountSlider.Parent.Visible = false;
                    mainPanel.RowStyles[3] = new RowStyle(SizeType.Absolute, 0F);
                }
            }
        }

        // Add this to handle combobox selection changes
        private void AiProviderComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedOption = aiProviderComboBox.SelectedItem as LLMOption;
            if (selectedOption != null && !selectedOption.IsGroupHeader)
            {
                UpdateUIForProvider(selectedOption);
            }
        }

        private void ShowTextToCodeDisclaimer()
        {
            using (var disclaimerForm = new DarkForm())  // Use DarkForm instead of Form
            {
                disclaimerForm.Text = "Experimental Feature";
                disclaimerForm.BackColor = Color.FromArgb(32, 32, 32);
                disclaimerForm.ForeColor = Color.White;
                disclaimerForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                disclaimerForm.MaximizeBox = false;
                disclaimerForm.MinimizeBox = false;
                disclaimerForm.Size = new Size(800, 800);
                disclaimerForm.StartPosition = FormStartPosition.CenterParent;

                var message = new Label
                {
                    Text = "Please note that the Text-to-Code feature is currently experimental.\n\n" +
                           "It works best with simple prompts and may produce unexpected results.\n\n" +
                           "If you stumble upon an error, try debug button.\n\n" +
                           "We might improve this feature in the future.",
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    Padding = new Padding(20),
                    Font = new Font("Segoe UI", 10f),
                    ForeColor = Color.White,
                    BackColor = Color.FromArgb(32, 32, 32)
                };

                var okButton = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    BackColor = Color.FromArgb(60, 60, 60),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(100, 35),
                    Font = new Font("Segoe UI", 9f)
                };

                // Add hover effects for the button
                okButton.MouseEnter += (s, e) => okButton.BackColor = Color.FromArgb(70, 70, 70);
                okButton.MouseLeave += (s, e) => okButton.BackColor = Color.FromArgb(60, 60, 60);

                // Center the button at the bottom
                okButton.Location = new Point(
                    (disclaimerForm.ClientSize.Width - okButton.Width) / 2,
                    disclaimerForm.ClientSize.Height - okButton.Height - 20);

                disclaimerForm.Controls.Add(message);
                disclaimerForm.Controls.Add(okButton);

                okButton.BringToFront();
                disclaimerForm.ShowDialog(this);
            }
        }

        // Add this method to handle image upload
        private void UploadButton_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";
                openFileDialog.Title = "Select an Image";
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Store the full path
                        selectedImagePath = openFileDialog.FileName;

                        // Read the file and convert to Base64
                        byte[] imageBytes = File.ReadAllBytes(selectedImagePath);
                        selectedImageBase64 = Convert.ToBase64String(imageBytes);

                        // Create the data URI
                        string fileExtension = Path.GetExtension(selectedImagePath).ToLower();
                        string mimeType;
                        
                        switch (fileExtension)
                        {
                            case ".png":
                                mimeType = "image/png";
                                break;
                            case ".jpg":
                            case ".jpeg":
                                mimeType = "image/jpeg";
                                break;
                            case ".bmp":
                                mimeType = "image/bmp";
                                break;
                            default:
                                mimeType = "image/png";
                                break;
                        }

                        // Store the complete data URI
                        selectedImageBase64 = $"data:{mimeType};base64,{selectedImageBase64}";

                        // Update UI to show selected file
                        promptTextBox.Text = $"Image ready: {Path.GetFileName(selectedImagePath)}";

                        // Update button states
                        uploadButton.BackColor = Color.FromArgb(60, 60, 60);  // Grey
                        submitButton.BackColor = Color.FromArgb(60, 120, 60); // Green
                        submitButton.Enabled = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading image: {ex.Message}", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        selectedImageBase64 = null;
                        selectedImagePath = null;
                    }
                }
            }
        }

        // Add cleanup in form's Dispose or closing event
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (polycountSlider != null && !polycountSlider.IsDisposed)
            {
                polycountSlider.Dispose();
                polycountSlider = null;
            }
        }

        private void AboutButton_Click(object sender, EventArgs e)
        {
            // If there's an existing about form, bring it to front
            if (_aboutForm != null && !_aboutForm.IsDisposed)
            {
                _aboutForm.Focus();
                return;
            }

            _aboutForm = new DarkForm
            {
                Text = "About",
                Size = new Size(800, 800),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent
            };

            // Calculate position to center the about form relative to the main form
            int x = this.Location.X + (this.Width - _aboutForm.Width) / 2;
            int y = this.Location.Y + (this.Height - _aboutForm.Height) / 2;
            _aboutForm.Location = new Point(x, y);

            _aboutForm.Deactivate += (s, args) => {
                if (_aboutForm != null && !_aboutForm.IsDisposed)
                {
                    _aboutForm.Close();
                    _aboutForm.Dispose();
                    _aboutForm = null;
                }
            };

            // Add controls first
            var message = new Label
            {
                Text = "RhinoM8 is your AI companion for Rhino 8, designed to convert natural language prompts " +
                      "and images into 3D models—no specialized coding required.\n\n" +
                      "Your version: RhinoM8 v0.1\n\n" +
                      "Developed with love by Differential, RhinoM8 is fully open source. " +
                      "Check out our GitHub repo and join the community.\n\n" +
                      "RhinoM8 is experimental software provided AS IS without any warranty. By using this tool, you acknowledge all risks and agree that you are solely responsible for any API costs, data loss, or other damages that may occur.",
                Location = new Point(20, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f),
                MaximumSize = new Size(740, 0)
            };

            // Calculate button positions to spread them equally
            int totalWidth = 800 - 40;
            int buttonWidth = 150;
            int spacing = (totalWidth - (3 * buttonWidth)) / 4;
            int startX = 20;
            int buttonY = 550;  // Moved up from 650

            var githubButton = new Button
            {
                Text = "GitHub",
                Location = new Point(startX + spacing, buttonY),
                Size = new Size(buttonWidth, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };

            var linkedinButton = new Button
            {
                Text = "LinkedIn",
                Location = new Point(startX + buttonWidth + (spacing * 2), buttonY),
                Size = new Size(buttonWidth, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };

            var websiteButton = new Button
            {
                Text = "Differential",
                Location = new Point(startX + (buttonWidth * 2) + (spacing * 3), buttonY),
                Size = new Size(buttonWidth, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White
            };

            foreach (var button in new[] { githubButton, linkedinButton, websiteButton })
            {
                button.MouseEnter += (s, e) => button.BackColor = Color.FromArgb(70, 70, 70);
                button.MouseLeave += (s, e) => button.BackColor = Color.FromArgb(60, 60, 60);
            }

            githubButton.Click += (s, e) => OpenUrl("https://github.com/differential-studio/RhinoM8");
            linkedinButton.Click += (s, e) => OpenUrl("https://www.linkedin.com/company/differential-studio");
            websiteButton.Click += (s, e) => OpenUrl("https://www.differential.studio/");

            _aboutForm.Controls.AddRange(new Control[] { message, githubButton, linkedinButton, websiteButton });
            
            // Show the form relative to the owner
            _aboutForm.Show(this);
            
            // Set position again after showing (sometimes necessary due to Windows DPI scaling)
            _aboutForm.Location = new Point(x, y);
            
            _aboutForm.Activate();
            _aboutForm.Focus();
        }

        private void OpenUrl(string url)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                DarkForm.ShowDialog($"Unable to open link: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ShowCodeViewer()
        {
            if (_codeViewer == null || _codeViewer.IsDisposed)
            {
                _codeViewer = new CodeViewerForm(lastGeneratedScript, sliderPanel, this);
            }
            
            if (!_codeViewer.Visible)
            {
                _codeViewer.Show();  // Show independently
            }
            _codeViewer.BringToFront();
        }
    }
}
