using System;
using System.Windows.Forms;
using System.Drawing;
using System.Text.RegularExpressions;
using Rhino;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;

namespace RhinoM8
{
    public class CodeViewerForm : DarkForm
    {
        private RichTextBox codeTextBox;
        private Panel lineNumbersPanel;
        private Button runButton;
        private Timer syntaxHighlightTimer;
        private bool isUserTyping = false;
        private ListBox historyListBox;
        private SliderPanel _sliderPanel;
        private PersistentPromptForm _parentForm;
        private Panel previewPanel;  // For mesh preview
        private DarkComboBox providerFilterComboBox;
        private DarkComboBox typeFilterComboBox;
        private TextBox searchBox;
        private List<object> allHistoryItems; // Cache for all items
        private List<object> currentFilteredItems;  // Store the current filtered items

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string pszSubIdList);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);

        private void SetDarkScrollBar(IntPtr handle)
        {
            const int WM_SYSCOLORCHANGE = 0x0015;
            
            // Set dark theme
            SetWindowTheme(handle, "DarkMode_Explorer", null);
            
            // Force a visual refresh
            SendMessage(handle, WM_SYSCOLORCHANGE, IntPtr.Zero, IntPtr.Zero);
        }

        public CodeViewerForm(string code, SliderPanel sliderPanel, PersistentPromptForm parentForm)
        {
            _sliderPanel = sliderPanel;
            _parentForm = parentForm;
            InitializeComponents(code);
            LoadCodeHistory();
            
            // Set form properties
            this.ShowInTaskbar = false;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimizeBox = true;
            this.MaximizeBox = true;
            this.Size = new Size(1600, 1000);
            this.MinimumSize = new Size(800, 600);
            
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

        public new void Show()
        {
            // Show independently instead of using Rhino's window handle
            base.Show();
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

        private void InitializeComponents(string code)
        {
            this.Text = "Library";
            this.BackColor = Color.FromArgb(32, 32, 32);
            this.ForeColor = Color.White;

            // Create main container panel
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                BackColor = Color.FromArgb(32, 32, 32)
            };

            // Configure row styles with adjusted proportions
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 75F));  // Code/Preview panel gets 75%
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F)); // Filter panel height increased from 35F to 60F
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));  // History gets 25%
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F)); // Run button fixed height

            // Create a container panel for code and preview
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(32, 32, 32)
            };

            // Code panel with line numbers
            var codePanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(32, 32, 32)
            };

            // Line numbers
            lineNumbersPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 40,
                BackColor = Color.FromArgb(40, 40, 40)
            };
            lineNumbersPanel.Paint += LineNumbersPanel_Paint;

            // Code text box
            codeTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Text = code,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.LightGray,
                Font = new Font("Consolas", 11f),
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both,
                AcceptsTab = true
            };
            // Set dark scrollbars
            codeTextBox.HandleCreated += (s, e) => SetDarkScrollBar(codeTextBox.Handle);

            // History list box
            historyListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f),
                IntegralHeight = false,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 40
            };
            // Set dark scrollbars
            historyListBox.HandleCreated += (s, e) => SetDarkScrollBar(historyListBox.Handle);
            historyListBox.DrawItem += HistoryListBox_DrawItem;
            historyListBox.MouseClick += HistoryListBox_MouseClick;
            historyListBox.SelectedIndexChanged += HistoryListBox_SelectedIndexChanged;

            // Run button
            runButton = new Button
            {
                Text = "Bake",
                Dock = DockStyle.Fill,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(60, 120, 60),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f)
            };

            // Create preview panel for meshes (initially hidden)
            previewPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                Visible = false
            };

            // Create filter panel
            var filterPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 45,  // Increased from 35 to 45
                BackColor = Color.FromArgb(40, 40, 40),
                Margin = new Padding(0),
                Padding = new Padding(5)
            };
            filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            filterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));

            // Provider filter
            providerFilterComboBox = new DarkComboBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            providerFilterComboBox.Items.AddRange(new object[] { 
                "All Providers", 
                "Meshy-3D",
                "Image-to-3D",
                "OpenAI", 
                "Claude", 
                "Grok"
            });
            providerFilterComboBox.SelectedIndex = 0;
            providerFilterComboBox.SelectedIndexChanged += (s, e) => RefreshFilteredList();

            // Type filter
            typeFilterComboBox = new DarkComboBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            typeFilterComboBox.Items.AddRange(new object[] { 
                "All Types", 
                "Text-to-Code (experimental, works only for simple geometries)",  // Match the exact group name
                "Text-to-3D" 
            });
            typeFilterComboBox.SelectedIndex = 0;
            typeFilterComboBox.SelectedIndexChanged += (s, e) => RefreshFilteredList();

            // Search box
            searchBox = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Text = "Search..."  // Default text
            };

            // Add placeholder behavior
            searchBox.GotFocus += (s, e) => 
            {
                if (searchBox.Text == "Search...")
                {
                    searchBox.Text = "";
                    searchBox.ForeColor = Color.White;
                }
            };

            searchBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(searchBox.Text))
                {
                    searchBox.Text = "Search...";
                    searchBox.ForeColor = Color.Gray;
                }
            };

            // Set initial color for placeholder
            searchBox.ForeColor = Color.Gray;

            // Update the TextChanged event to ignore placeholder text
            searchBox.TextChanged += (s, e) => 
            {
                if (searchBox.Text != "Search...")
                    RefreshFilteredList();
            };

            // Add controls to filter panel
            filterPanel.Controls.Add(providerFilterComboBox, 0, 0);
            filterPanel.Controls.Add(typeFilterComboBox, 1, 0);
            filterPanel.Controls.Add(searchBox, 2, 0);

            // Add filter panel above history list
            mainPanel.Controls.Add(filterPanel, 0, 1);
            mainPanel.Controls.Add(historyListBox, 0, 2);

            // Adjust row styles to accommodate filter panel
            mainPanel.RowStyles[1] = new RowStyle(SizeType.Absolute, 45F);
            mainPanel.RowStyles[2] = new RowStyle(SizeType.Percent, 25F);

            // Add controls to code panel
            codePanel.Controls.Add(codeTextBox);
            codePanel.Controls.Add(lineNumbersPanel);

            // Add panels to content container
            contentPanel.Controls.Add(previewPanel);  // Add preview panel first (will be behind)
            contentPanel.Controls.Add(codePanel);     // Add code panel second (will be in front)

            // Add all elements to the main panel
            mainPanel.Controls.Add(contentPanel, 0, 0);     // Content panel in first row
            mainPanel.Controls.Add(filterPanel, 0, 1);       // Filter panel in second row
            mainPanel.Controls.Add(historyListBox, 0, 2);    // History in third row
            mainPanel.Controls.Add(runButton, 0, 3);         // Run button in fourth row

            // Add main panel to form
            this.Controls.Add(mainPanel);

            // Set up event handlers
            codeTextBox.VScroll += (s, e) => lineNumbersPanel.Invalidate();
            codeTextBox.TextChanged += CodeTextBox_TextChanged;
            codeTextBox.KeyDown += CodeTextBox_KeyDown;
            runButton.Click += RunButton_Click;
            
            // Setup syntax highlighting timer
            syntaxHighlightTimer = new Timer { Interval = 500 };
            syntaxHighlightTimer.Tick += (s, e) => 
            {
                if (!isUserTyping)
                {
                    HighlightSyntax();
                }
            };
            syntaxHighlightTimer.Start();

            // Initial syntax highlighting
            HighlightSyntax();

            // Update the history list box to show both types of entries
            LoadAllHistory();
        }

        public void LoadCodeHistory()
        {
            LoadAllHistory();
        }

        public void LoadAllHistory()
        {
            // Clear the cache first
            allHistoryItems = null;
            
            // Reload from plugin
            allHistoryItems = RhinoM8Plugin.Instance.GetAllHistory().ToList();
            RhinoApp.WriteLine($"Loaded {allHistoryItems.Count} total history items");
            RefreshFilteredList();
        }

        private void RefreshFilteredList()
        {
            if (allHistoryItems == null) return;

            var filteredItems = allHistoryItems.AsEnumerable();

            // Apply provider filter
            if (providerFilterComboBox.SelectedItem.ToString() != "All Providers")
            {
                filteredItems = filteredItems.Where(item =>
                {
                    if (item is CodeHistoryEntry codeEntry)
                        return codeEntry.Provider == providerFilterComboBox.SelectedItem.ToString();
                    if (item is MeshHistoryEntry meshEntry)
                        return meshEntry.Provider == providerFilterComboBox.SelectedItem.ToString();
                    return false;
                });
            }

            // Apply type filter
            if (typeFilterComboBox.SelectedItem.ToString() != "All Types")
            {
                string selectedType = typeFilterComboBox.SelectedItem.ToString();
                filteredItems = filteredItems.Where(item => 
                {
                    if (item is CodeHistoryEntry)
                        return selectedType == "Text-to-Code (experimental, works only for simple geometries)";
                    if (item is MeshHistoryEntry)
                        return selectedType == "Text-to-3D";
                    return false;
                });
            }

            // Apply search filter
            var searchText = searchBox.Text.Trim().ToLower();
            if (!string.IsNullOrEmpty(searchText) && searchText != "search...")
            {
                filteredItems = filteredItems.Where(item =>
                {
                    if (item is CodeHistoryEntry codeEntry)
                        return codeEntry.Prompt.ToLower().Contains(searchText) ||
                               codeEntry.Code.ToLower().Contains(searchText);
                    if (item is MeshHistoryEntry meshEntry)
                        return meshEntry.Prompt.ToLower().Contains(searchText);
                    return false;
                });
            }

            // Store the filtered list
            currentFilteredItems = filteredItems.ToList();

            // Update list box
            historyListBox.BeginUpdate();
            historyListBox.Items.Clear();

            foreach (var item in currentFilteredItems)
            {
                string displayText;
                if (item is CodeHistoryEntry codeEntry)
                {
                    displayText = $"{codeEntry.Timestamp:g} - {codeEntry.Provider} - {codeEntry.Prompt.Substring(0, Math.Min(50, codeEntry.Prompt.Length))}...";
                }
                else if (item is MeshHistoryEntry meshEntry)
                {
                    displayText = $"{meshEntry.Timestamp:g} - {meshEntry.Provider} - {meshEntry.Prompt.Substring(0, Math.Min(50, meshEntry.Prompt.Length))}...";
                }
                else
                {
                    continue;
                }

                historyListBox.Items.Add(displayText);
            }
            historyListBox.EndUpdate();

            // Debug output
            RhinoApp.WriteLine($"Loaded {historyListBox.Items.Count} items in history list");
        }

        private void HistoryListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (historyListBox.SelectedIndex >= 0 && currentFilteredItems != null)
            {
                var entry = currentFilteredItems[historyListBox.SelectedIndex];

                if (entry is CodeHistoryEntry codeEntry)
                {
                    // Show code editor, hide preview
                    codeTextBox.Visible = true;
                    lineNumbersPanel.Visible = true;
                    previewPanel.Visible = false;

                    // Format and show the code
                    string formattedCode = $"# Prompt: {codeEntry.Prompt}\n# Generated by: {codeEntry.Provider}\n# Date: {codeEntry.Timestamp}\n\n{codeEntry.Code}";
                    UpdateCode(formattedCode);
                    
                    // Update parent form and sliders
                    _parentForm?.UpdateFromHistory(codeEntry);
                    _sliderPanel?.UpdateFromScript(codeEntry.Code);
                    
                    // Show slider panel without affecting CodeViewer focus
                    if (_sliderPanel != null && !_sliderPanel.Visible)
                    {
                        _sliderPanel.Show();
                        this.Activate();
                        this.Focus();
                    }
                }
                else if (entry is MeshHistoryEntry meshEntry)
                {
                    // Show preview, hide code editor
                    codeTextBox.Visible = false;
                    lineNumbersPanel.Visible = false;
                    previewPanel.Visible = true;

                    // Show mesh preview
                    ShowMeshPreview(meshEntry);

                    // Hide sliders for mesh entries
                    _sliderPanel?.Hide();
                }
            }
        }

        private void LineNumbersPanel_Paint(object sender, PaintEventArgs e)
        {
            var startLine = codeTextBox.GetLineFromCharIndex(codeTextBox.GetCharIndexFromPosition(new Point(0, 0)));
            var endLine = codeTextBox.GetLineFromCharIndex(codeTextBox.GetCharIndexFromPosition(
                new Point(0, codeTextBox.Height))) + 1;

            for (int i = startLine; i <= endLine; i++)
            {
                var lineY = codeTextBox.GetPositionFromCharIndex(codeTextBox.GetFirstCharIndexFromLine(i)).Y;
                if (lineY >= 0)
                {
                    e.Graphics.DrawString(
                        (i + 1).ToString(),
                        codeTextBox.Font,
                        Brushes.Gray,
                        lineNumbersPanel.Width - 25,
                        lineY,
                        new StringFormat { Alignment = StringAlignment.Far }
                    );
                }
            }
        }

        private void CodeTextBox_TextChanged(object sender, EventArgs e)
        {
            isUserTyping = true;
            lineNumbersPanel.Invalidate();
            syntaxHighlightTimer.Stop();
            syntaxHighlightTimer.Start();
        }

        private void CodeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                // Auto-indent
                int currentLine = codeTextBox.GetLineFromCharIndex(codeTextBox.SelectionStart);
                if (currentLine > 0)
                {
                    string prevLine = codeTextBox.Lines[currentLine - 1];
                    string indent = Regex.Match(prevLine, @"^\s*").Value;
                    if (prevLine.TrimEnd().EndsWith(":"))
                    {
                        indent += "    ";
                    }
                    if (!string.IsNullOrEmpty(indent))
                    {
                        BeginInvoke(new Action(() =>
                        {
                            codeTextBox.SelectedText = indent;
                        }));
                    }
                }
            }
            else if (e.KeyCode == Keys.Tab)
            {
                e.Handled = true;
                codeTextBox.SelectedText = "    ";
            }
        }

        private void HighlightSyntax()
        {
            if (codeTextBox.Text.Length == 0) return;

            isUserTyping = false;
            int selectionStart = codeTextBox.SelectionStart;
            int selectionLength = codeTextBox.SelectionLength;

            // Store the current text
            string text = codeTextBox.Text;

            // Keywords to highlight
            string[] pythonKeywords = new[] {
                "def", "class", "import", "from", "as", "return", "if", "else", "elif",
                "for", "while", "in", "try", "except", "finally", "with", "True", "False",
                "None", "and", "or", "not", "is", "lambda"
            };

            // Clear existing formatting
            codeTextBox.SelectionStart = 0;
            codeTextBox.SelectionLength = text.Length;
            codeTextBox.SelectionColor = Color.LightGray;

            // Highlight strings
            foreach (Match match in Regex.Matches(text, @"("".*?""|'.*?')", RegexOptions.Multiline))
            {
                codeTextBox.SelectionStart = match.Index;
                codeTextBox.SelectionLength = match.Length;
                codeTextBox.SelectionColor = Color.FromArgb(206, 145, 120); // String color
            }

            // Highlight keywords
            foreach (string keyword in pythonKeywords)
            {
                foreach (Match match in Regex.Matches(text, $@"\b{keyword}\b"))
                {
                    codeTextBox.SelectionStart = match.Index;
                    codeTextBox.SelectionLength = match.Length;
                    codeTextBox.SelectionColor = Color.FromArgb(86, 156, 214); // Keyword color
                }
            }

            // Highlight functions
            foreach (Match match in Regex.Matches(text, @"\b\w+(?=\s*\()"))
            {
                codeTextBox.SelectionStart = match.Index;
                codeTextBox.SelectionLength = match.Length;
                codeTextBox.SelectionColor = Color.FromArgb(220, 220, 170); // Function color
            }

            // Highlight comments
            foreach (Match match in Regex.Matches(text, @"#.*$", RegexOptions.Multiline))
            {
                codeTextBox.SelectionStart = match.Index;
                codeTextBox.SelectionLength = match.Length;
                codeTextBox.SelectionColor = Color.FromArgb(87, 166, 74); // Comment color
            }

            // Restore the selection
            codeTextBox.SelectionStart = selectionStart;
            codeTextBox.SelectionLength = selectionLength;
        }

        private void RunButton_Click(object sender, EventArgs e)
        {
            try
            {
                var doc = RhinoDoc.ActiveDoc;
                if (doc == null) return;

                if (historyListBox.SelectedIndex >= 0)
                {
                    var allHistory = RhinoM8Plugin.Instance.GetAllHistory().ToList();
                    var entry = allHistory[historyListBox.SelectedIndex];

                    if (entry is CodeHistoryEntry codeEntry)
                    {
                        // Execute Python code
                        string script = codeTextBox.Text;
                        _sliderPanel?.UpdateFromScript(script);

                        var py = Rhino.Runtime.PythonScript.Create();
                        if (py != null)
                        {
                            doc.Objects.UnselectAll();
                            py.ExecuteScript(script);
                            doc.Views.Redraw();
                        }
                    }
                    else if (entry is MeshHistoryEntry meshEntry)
                    {
                        if (File.Exists(meshEntry.FilePath))
                        {
                            // Use Rhino's import command to load the GLB
                            RhinoApp.RunScript($"_-Import \"{meshEntry.FilePath}\" _Enter", false);
                            doc.Views.Redraw();
                            RhinoApp.WriteLine("Mesh baked successfully");
                        }
                        else
                        {
                            MessageBox.Show("Model file not found", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DarkForm.ShowDialog($"Error executing code: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void UpdateCode(string newCode)
        {
            if (codeTextBox.Text != newCode)
            {
                codeTextBox.Text = newCode;
                HighlightSyntax();
            }
        }

        private void HistoryListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            e.DrawBackground();
            
            var allHistory = RhinoM8Plugin.Instance.GetAllHistory().ToList();
            var entry = allHistory[e.Index];
            bool isMeshEntry = entry is MeshHistoryEntry;

            // Draw icon based on type
            string icon = isMeshEntry ? "🔲" : "📝";
            
            // Draw icon based on type
            var iconRect = new Rectangle(
                e.Bounds.X + 3,
                e.Bounds.Y + 2,
                20,
                e.Bounds.Height - 4
            );

            using (var brush = new SolidBrush(e.ForeColor))
            {
                // Draw icon
                e.Graphics.DrawString(icon, historyListBox.Font, brush, iconRect);

                // Draw text with offset for icon
                var textRect = new Rectangle(
                    e.Bounds.X + 25,  // Offset for icon
                    e.Bounds.Y,
                    e.Bounds.Width - 50,  // Leave room for X button
                    e.Bounds.Height
                );

                e.Graphics.DrawString(
                    historyListBox.Items[e.Index].ToString(),
                    historyListBox.Font,
                    brush,
                    textRect,
                    StringFormat.GenericDefault
                );

                // Draw the "X" button
                if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
                {
                    var xButtonRect = new Rectangle(
                        e.Bounds.Right - 25,
                        e.Bounds.Y + 2,
                        20,
                        e.Bounds.Height - 4
                    );
                    using (var xBrush = new SolidBrush(Color.FromArgb(200, 80, 80)))
                    using (var font = new Font("Segoe UI", 9f))
                    {
                        e.Graphics.DrawString("×", font, xBrush, xButtonRect);
                    }
                }
            }

            e.DrawFocusRectangle();
        }

        private void HistoryListBox_MouseClick(object sender, MouseEventArgs e)
        {
            // Get the index of the item clicked
            int index = historyListBox.IndexFromPoint(e.Location);
            if (index < 0) return;

            // Calculate if the click was on the "X" button
            var itemRect = historyListBox.GetItemRectangle(index);
            var xButtonRect = new Rectangle(
                itemRect.Right - 25,
                itemRect.Y + 2,
                20,
                itemRect.Height - 4
            );

            if (xButtonRect.Contains(e.Location))
            {
                // Remove the item from both the ListBox and the history
                var history = RhinoM8Plugin.Instance.GetAllHistory().ToList();
                var entryToRemove = history[index];
                
                // Remove from plugin history
                RhinoM8Plugin.Instance.RemoveFromHistory(entryToRemove);
                
                // Reload the history list
                LoadCodeHistory();
            }
        }

        private void ShowMeshPreview(MeshHistoryEntry meshEntry)
        {
            // Clear existing preview panel
            previewPanel.Controls.Clear();

            // Add info label
            var infoLabel = new Label
            {
                Text = $"Prompt: {meshEntry.Prompt}\nGenerated by: {meshEntry.Provider}\nDate: {meshEntry.Timestamp}",
                Dock = DockStyle.Top,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f),
                AutoSize = true,
                Padding = new Padding(10)
            };
            previewPanel.Controls.Add(infoLabel);

            // Add preview image if available
            if (!string.IsNullOrEmpty(meshEntry.ThumbnailPath) && File.Exists(meshEntry.ThumbnailPath))
            {
                try
                {
                    var pictureBox = new PictureBox
                    {
                        Dock = DockStyle.Fill,
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Image = Image.FromFile(meshEntry.ThumbnailPath)
                    };
                    previewPanel.Controls.Add(pictureBox);
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"Error loading thumbnail: {ex.Message}");
                    ShowDefaultPreviewMessage();
                }
            }
            else
            {
                ShowDefaultPreviewMessage();
            }
        }

        private void ShowDefaultPreviewMessage()
        {
            var previewLabel = new Label
            {
                Text = "Preview not available.\nClick 'Bake' to add the mesh to your Rhino document.",
                Dock = DockStyle.Fill,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 12f),
                TextAlign = ContentAlignment.MiddleCenter
            };
            previewPanel.Controls.Add(previewLabel);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            
            if (this.Visible)
            {
                // Refresh the history when form becomes visible
                LoadCodeHistory();
            }
        }
    }
} 