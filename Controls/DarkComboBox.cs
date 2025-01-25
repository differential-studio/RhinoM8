using System;
using System.Drawing;
using System.Windows.Forms;

namespace RhinoM8
{
    public class DarkComboBox : ComboBox
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

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            // Draw the background
            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
            {
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(60, 60, 60)), e.Bounds);
            }
            else
            {
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(45, 45, 45)), e.Bounds);
            }

            // Draw the item text
            if (e.Index >= 0)
            {
                using (var brush = new SolidBrush(Color.White))
                {
                    e.Graphics.DrawString(
                        this.Items[e.Index].ToString(),
                        e.Font,
                        brush,
                        e.Bounds.X + 5,
                        e.Bounds.Y + 2
                    );
                }
            }

            // Draw focus rectangle if needed
            if ((e.State & DrawItemState.Focus) == DrawItemState.Focus)
            {
                e.DrawFocusRectangle();
            }
        }
    }
} 