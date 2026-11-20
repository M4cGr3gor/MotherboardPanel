using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using RgbCore;

namespace RgbApp
{
    /// <summary>
    /// Fila de muestras de color (2-8). Clic en una muestra: la selecciona y abre el selector de color.
    /// Botones: + añadir, − quitar la seleccionada, ◀ ▶ moverla.
    /// </summary>
    internal sealed class ColorListEditor : UserControl
    {
        // Margin deja sitio al anillo de selección, que se dibuja por fuera de la muestra.
        private const int Swatch = 30, Gap = 6, Margin0 = 4;
        private readonly Panel swatches;
        private readonly Button btnAdd, btnRemove, btnLeft, btnRight;
        private List<string> colors = new List<string>();
        private int selected;
        private readonly Color accent;

        public event EventHandler ColorsChanged;

        public ColorListEditor(Color accent, Color border, Color buttonBg)
        {
            this.accent = accent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Color.White;

            var layout = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
            swatches = new Panel { Width = Preset.MaxColors * (Swatch + Gap) + 2 * Margin0, Height = Swatch + 2 * Margin0, Margin = new Padding(0, 0, 0, 6), Cursor = Cursors.Hand };
            swatches.Paint += PaintSwatches;
            swatches.MouseClick += SwatchClick;
            typeof(Panel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(swatches, true);

            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            Button Make(string text, string tip, Action onClick)
            {
                var b = new Button
                {
                    Text = text, Width = 34, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = buttonBg,
                    Margin = new Padding(0, 0, 5, 0), Cursor = Cursors.Hand,
                };
                b.FlatAppearance.BorderColor = border;
                b.Click += (s, e) => onClick();
                new ToolTip().SetToolTip(b, tip);
                return b;
            }
            btnAdd = Make("+", "Añadir un color", AddColor);
            btnRemove = Make("−", "Quitar el color seleccionado", RemoveColor);
            btnLeft = Make("◀", "Mover el color seleccionado a la izquierda", () => MoveSelected(-1));
            btnRight = Make("▶", "Mover el color seleccionado a la derecha", () => MoveSelected(+1));
            buttons.Controls.AddRange(new Control[] { btnAdd, btnRemove, btnLeft, btnRight });

            layout.Controls.Add(swatches);
            layout.Controls.Add(buttons);
            Controls.Add(layout);
        }

        /// <summary>Colores #RRGGBB. Al asignar se normaliza a 2-8 entradas.</summary>
        public List<string> Colors
        {
            get => new List<string>(colors);
            set
            {
                colors = (value ?? new List<string>()).Take(Preset.MaxColors).ToList();
                while (colors.Count < Preset.MinColors)
                    colors.Add(colors.Count > 0 ? colors[0] : "#FFFFFF");
                selected = Math.Min(selected, colors.Count - 1);
                RefreshState();
            }
        }

        private void RefreshState()
        {
            btnAdd.Enabled = colors.Count < Preset.MaxColors;
            btnRemove.Enabled = colors.Count > Preset.MinColors;
            btnLeft.Enabled = selected > 0;
            btnRight.Enabled = selected < colors.Count - 1;
            swatches.Invalidate();
        }

        private void Changed()
        {
            RefreshState();
            ColorsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void PaintSwatches(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            for (int i = 0; i < colors.Count; i++)
            {
                var r = new Rectangle(Margin0 + i * (Swatch + Gap), Margin0, Swatch, Swatch);
                using (var br = new SolidBrush(ToColor(colors[i])))
                    g.FillEllipse(br, r);
                using (var pen = new Pen(Color.FromArgb(70, 0, 0, 0)))
                    g.DrawEllipse(pen, r);
                if (i == selected)
                    using (var pen = new Pen(accent, 2.5f))
                        g.DrawEllipse(pen, r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2);
            }
        }

        private void SwatchClick(object sender, MouseEventArgs e)
        {
            int i = (e.X - Margin0) / (Swatch + Gap);
            if (i < 0 || i >= colors.Count)
                return;
            selected = i;
            RefreshState();
            using (var dlg = new ColorDialog { FullOpen = true, Color = ToColor(colors[i]) })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
                    return;
                colors[i] = "#" + ((dlg.Color.R << 16) | (dlg.Color.G << 8) | dlg.Color.B).ToString("X6");
            }
            Changed();
        }

        private void AddColor()
        {
            if (colors.Count >= Preset.MaxColors)
                return;
            colors.Insert(selected + 1, colors[selected]);
            selected++;
            Changed();
        }

        private void RemoveColor()
        {
            if (colors.Count <= Preset.MinColors)
                return;
            colors.RemoveAt(selected);
            selected = Math.Min(selected, colors.Count - 1);
            Changed();
        }

        private void MoveSelected(int delta)
        {
            int target = selected + delta;
            if (target < 0 || target >= colors.Count)
                return;
            string c = colors[selected];
            colors[selected] = colors[target];
            colors[target] = c;
            selected = target;
            Changed();
        }

        private static Color ToColor(string hex)
        {
            uint v;
            string s = (hex ?? "").TrimStart('#');
            if (!uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v))
                v = 0xFFFFFF;
            return Color.FromArgb((int)(v >> 16) & 255, (int)(v >> 8) & 255, (int)v & 255);
        }
    }
}
