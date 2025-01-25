using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace RhinoM8
{
    public class DarkForm : Form
    {
        public DarkForm()
        {
            // Explicitly remove the icon
            this.ShowIcon = false;  // This will hide the icon in the title bar
            this.Icon = null;       // This ensures no icon is inherited
            
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.BackColor = Color.FromArgb(32, 32, 32);
                this.ForeColor = Color.White;
                
                SetStyle(ControlStyles.SupportsTransparentBackColor, true);
                
                this.HandleCreated += (s, e) => 
                {
                    DwmApi.UseImmersiveDarkMode(this.Handle, true);
                    DwmApi.SetDarkTitleBar(this.Handle);
                };
            }
        }

        public static DialogResult ShowDialog(string message, string title = "RhinoM8", 
            MessageBoxButtons buttons = MessageBoxButtons.OK, 
            MessageBoxIcon icon = MessageBoxIcon.Information)
        {
            var form = new DarkForm
            {
                Text = title,
                Size = new Size(400, 200),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false
            };

            var label = new Label
            {
                Text = message,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                AutoSize = true,
                MaximumSize = new Size(360, 0),
                Location = new Point(20, 20)
            };

            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(10)
            };

            // Add buttons based on MessageBoxButtons parameter
            if (buttons == MessageBoxButtons.OK || buttons == MessageBoxButtons.OKCancel)
            {
                var okButton = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Size = new Size(80, 30),
                    BackColor = Color.FromArgb(60, 60, 60),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                buttonPanel.Controls.Add(okButton);
            }

            if (buttons == MessageBoxButtons.OKCancel || buttons == MessageBoxButtons.YesNo)
            {
                var cancelButton = new Button
                {
                    Text = buttons == MessageBoxButtons.OKCancel ? "Cancel" : "No",
                    DialogResult = buttons == MessageBoxButtons.OKCancel ? DialogResult.Cancel : DialogResult.No,
                    Size = new Size(80, 30),
                    BackColor = Color.FromArgb(60, 60, 60),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(10, 0, 0, 0)
                };
                buttonPanel.Controls.Add(cancelButton);
            }

            if (buttons == MessageBoxButtons.YesNo)
            {
                var yesButton = new Button
                {
                    Text = "Yes",
                    DialogResult = DialogResult.Yes,
                    Size = new Size(80, 30),
                    BackColor = Color.FromArgb(60, 60, 60),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                buttonPanel.Controls.Add(yesButton);
            }

            form.Controls.Add(label);
            form.Controls.Add(buttonPanel);

            return form.ShowDialog();
        }
    }

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
} 