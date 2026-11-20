using System.Drawing;
using System.Windows.Forms;
using RgbCore;

namespace RgbApp
{
    /// <summary>Cuadrícula con los iconos incorporados; devuelve la clave elegida en SelectedIcon.</summary>
    internal sealed class IconPickerForm : Form
    {
        public string SelectedIcon { get; private set; }

        public IconPickerForm(Preset preset, Color accent, Color selectedBg)
        {
            Text = "Elegir icono";
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.White;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            SelectedIcon = preset.Icon ?? PresetIcons.Auto;

            var grid = new FlowLayoutPanel
            {
                AutoSize = true,
                MaximumSize = new Size(6 * 96, 0),
                Padding = new Padding(12),
                BackColor = Color.White,
            };
            var tips = new ToolTip();

            foreach (var entry in PresetIcons.Catalog)
            {
                string key = entry.Key;
                var cell = new Panel { Size = new Size(88, 84), Margin = new Padding(4), Cursor = Cursors.Hand, BackColor = Color.White };
                cell.Paint += (s, e) =>
                {
                    bool current = key == (SelectedIcon ?? "");
                    if (current)
                    {
                        using (var bg = new SolidBrush(selectedBg))
                            e.Graphics.FillRectangle(bg, cell.ClientRectangle);
                        using (var pen = new Pen(accent, 2))
                            e.Graphics.DrawRectangle(pen, 1, 1, cell.Width - 3, cell.Height - 3);
                    }
                    PresetIcons.Draw(e.Graphics, preset, key, new RectangleF((cell.Width - 32) / 2f, 10, 32, 32));
                    TextRenderer.DrawText(e.Graphics, entry.Value, Font, new Rectangle(2, 48, cell.Width - 4, 32),
                        Color.FromArgb(70, 70, 70), TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
                };
                cell.Click += (s, e) =>
                {
                    SelectedIcon = key;
                    DialogResult = DialogResult.OK;
                };
                tips.SetToolTip(cell, entry.Value);
                grid.Controls.Add(cell);
            }
            Controls.Add(grid);
        }
    }
}
