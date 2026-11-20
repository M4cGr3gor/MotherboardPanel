using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using RgbCore;

namespace RgbApp
{
    public sealed class MainForm : Form
    {
        // Tema claro al estilo de Windows 11; el acento es el color de acento del sistema.
        private static readonly Color Bg = Color.FromArgb(243, 243, 243);
        private static readonly Color Card = Color.White;
        private static readonly Color Border = Color.FromArgb(229, 229, 229);
        private static readonly Color ButtonBg = Color.FromArgb(251, 251, 251);
        private static readonly Color Text1 = Color.FromArgb(27, 27, 27);
        private static readonly Color Text2 = Color.FromArgb(96, 96, 96);
        private static readonly Color Success = Color.FromArgb(15, 123, 15);
        private static readonly Color Danger = Color.FromArgb(196, 43, 28);
        private static readonly Color Accent = SystemAccent();
        private static readonly Color SelectedBg = Blend(Accent, Color.White, 0.12f);

        private readonly PresetStore store = PresetStore.Default();

        private ListBox list;
        private Button btnNew, btnDuplicate, btnDelete, btnApply, btnCalibrate;
        private TextBox txtName;
        private ComboBox cmbEffect;
        private Button btnColor, btnIcon, btnBackground;
        private TrackBar trkBrightness, trkSpeed, trkColors, trkTrail, trkDensity;
        private Label lblBrightness, lblSpeed, lblColors, lblTrail, lblDensity, lblHint, lblStatus;
        private ColorListEditor colorList;
        private RadioButton rbClockwise, rbCounter;
        private CheckBox chkAutostart;
        private Control rowIcon, rowColor, rowColorList, rowDirection, rowBackground, rowBrightness, rowSpeed, rowColors, rowTrail, rowDensity;
        private readonly ToolTip tips = new ToolTip();
        private readonly HelperControl helper = new HelperControl();

        /// <summary>Mensaje con el que una segunda copia de la ventana pide traer esta al frente.</summary>
        public static readonly int ShowMessage = NativeMethods.RegisterWindowMessage("RGBControl.Show");

        /// <summary>Posición en el desplegable de la cabecera "Avanzados".</summary>
        private const int AdvancedHeaderIndex = 7;
        private static readonly string[] DefaultGradient = { "#8000FF", "#0040FF", "#00E0FF" };

        private static string AppDir => AppDomain.CurrentDomain.BaseDirectory;
        private static string HelperExePath => Path.Combine(AppDir, HelperControl.HelperExeName);
        private bool loading;
        private int dragIndex = -1;
        private Point dragStart;
        private int dragFrom = -1;   // preset que se está arrastrando (-1 = ninguno)
        private int dropIndex = -1;  // dónde caería si se suelta ahora
        private GhostForm ghost;
        private Point ghostOffset;

        public MainForm()
        {
            Text = "RGB Control";
            BackColor = Bg;
            ForeColor = Text1;
            Font = new Font("Segoe UI", 10f);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(900, 790);
            MinimumSize = new Size(800, 640);
            Icon = AppIcon.Create();

            BuildUi();

            try
            {
                store.Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo leer ni crear presets.json:\n" + ex.Message, "RGB Control",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            RefreshList(store.LastApplied);
            SetStatus(store.LastApplied != null ? "Último preset aplicado: " + store.LastApplied : "Elige un preset y pulsa Aplicar.", Text2);
        }

        private Preset Selected => list.SelectedItem as Preset;

        // ---------------------------------------------------------------- interfaz

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(16),
                BackColor = Bg,
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 350));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            Controls.Add(root);

            root.Controls.Add(BuildPresetPanel(), 0, 0);
            root.Controls.Add(BuildEditorPanel(), 1, 0);
            var bottom = BuildBottomBar();
            root.Controls.Add(bottom, 0, 1);
            root.SetColumnSpan(bottom, 2);
        }

        private Control BuildPresetPanel()
        {
            var panel = new CardPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(12),
                Margin = new Padding(0, 0, 12, 0),
            };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0), BackColor = Card };
            var title = Title("Presets");
            title.Margin = new Padding(0, 0, 0, 2);
            header.Controls.Add(title);
            header.Controls.Add(new Label { Text = "Arrastra tus presets para ordenarlos", AutoSize = true, ForeColor = Text2, Font = new Font("Segoe UI", 8.5f), Margin = new Padding(0, 0, 0, 8) });
            panel.Controls.Add(header, 0, 0);

            list = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Card,
                ForeColor = Text1,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 52,
                IntegralHeight = false,
            };
            list.DrawItem += DrawPresetItem;
            list.SelectedIndexChanged += (s, e) => LoadEditor();
            list.DoubleClick += (s, e) => ApplySelected();
            list.AllowDrop = true;
            list.MouseDown += ListMouseDown;
            list.MouseMove += ListMouseMove;
            list.DragOver += ListDragOver;
            list.DragDrop += ListDragDrop;
            list.GiveFeedback += (s, e) => MoveGhost();
            list.DragLeave += (s, e) =>
            {
                dropIndex = -1;
                list.Invalidate();
            };
            panel.Controls.Add(list, 0, 1);

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0), BackColor = Card, WrapContents = false };
            btnNew = SmallButton("Nuevo", (s, e) => NewPreset());
            btnDuplicate = SmallButton("Duplicar", (s, e) => DuplicatePreset());
            btnDelete = SmallButton("Eliminar", (s, e) => DeletePreset());
            buttons.Controls.AddRange(new Control[] { btnNew, btnDuplicate, btnDelete });
            panel.Controls.Add(buttons, 0, 2);
            return panel;
        }

        private Control BuildEditorPanel()
        {
            var card = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(1) };
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Card,
                Padding = new Padding(16, 12, 16, 12),
                AutoScroll = true,
            };
            card.Controls.Add(panel);

            panel.Controls.Add(Title("Ajustes del preset"));

            txtName = new TextBox { BackColor = Card, ForeColor = Text1, BorderStyle = BorderStyle.FixedSingle, Width = 320 };
            txtName.TextChanged += (s, e) => OnEdited(nameChanged: true);
            panel.Controls.Add(Row("Nombre", txtName));

            cmbEffect = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 22 };
            foreach (PresetEffect fx in Enum.GetValues(typeof(PresetEffect)))
            {
                if ((int)fx == AdvancedHeaderIndex)
                    cmbEffect.Items.Add(new EffectItem(null));
                cmbEffect.Items.Add(new EffectItem(fx));
            }
            cmbEffect.DrawItem += DrawEffectItem;
            cmbEffect.SelectedIndexChanged += (s, e) =>
            {
                // La cabecera de grupo no se puede elegir: se salta al primer efecto avanzado.
                if (cmbEffect.SelectedIndex == AdvancedHeaderIndex)
                {
                    cmbEffect.SelectedIndex = AdvancedHeaderIndex + 1;
                    return;
                }
                OnEdited();
            };
            panel.Controls.Add(Row("Efecto", cmbEffect));

            btnIcon = new Button { Width = 320, Height = 44, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, Cursor = Cursors.Hand, BackColor = Card, Text = "            Cambiar icono…" };
            btnIcon.FlatAppearance.BorderColor = Border;
            btnIcon.Paint += (s, e) =>
            {
                if (Selected != null)
                    PresetIcons.Draw(e.Graphics, Selected, new RectangleF(10, (btnIcon.Height - 28) / 2f, 28, 28));
            };
            btnIcon.Click += (s, e) => PickIcon();
            rowIcon = Row("Icono", btnIcon);
            panel.Controls.Add(rowIcon);

            btnColor =new Button { Width = 320, Height = 36, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, Cursor = Cursors.Hand };
            btnColor.FlatAppearance.BorderColor = Border;
            btnColor.Click += (s, e) => PickColor();
            rowColor = Row("Color", btnColor);
            panel.Controls.Add(rowColor);

            colorList = new ColorListEditor(Accent, Border, ButtonBg);
            colorList.ColorsChanged += (s, e) => OnEdited();
            rowColorList = Row("Colores", colorList);
            panel.Controls.Add(rowColorList);

            rbClockwise = new RadioButton { Text = "Horario", AutoSize = true, Margin = new Padding(0, 0, 16, 0) };
            rbCounter = new RadioButton { Text = "Antihorario", AutoSize = true };
            rbClockwise.CheckedChanged += (s, e) => { if (rbClockwise.Checked) OnEdited(); };
            rbCounter.CheckedChanged += (s, e) => { if (rbCounter.Checked) OnEdited(); };
            var dirPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            dirPanel.Controls.AddRange(new Control[] { rbClockwise, rbCounter });
            rowDirection = Row("Sentido", dirPanel);
            panel.Controls.Add(rowDirection);

            btnBackground = new Button { Width = 320, Height = 36, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft, Cursor = Cursors.Hand };
            btnBackground.FlatAppearance.BorderColor = Border;
            btnBackground.Click += (s, e) => PickBackground();
            rowBackground = Row("Color de fondo", btnBackground);
            panel.Controls.Add(rowBackground);

            trkBrightness = Slider(0, 100, 10);
            lblBrightness = ValueLabel();
            trkBrightness.ValueChanged += (s, e) => OnEdited();
            rowBrightness = Row("Brillo", SliderWithLabel(trkBrightness, lblBrightness));
            panel.Controls.Add(rowBrightness);

            trkSpeed = Slider(1, 10, 1);
            lblSpeed = ValueLabel();
            trkSpeed.ValueChanged += (s, e) => OnEdited();
            rowSpeed = Row("Velocidad", SliderWithLabel(trkSpeed, lblSpeed));
            panel.Controls.Add(rowSpeed);

            trkColors = Slider(2, 7, 1);
            lblColors = ValueLabel();
            trkColors.ValueChanged += (s, e) => OnEdited();
            rowColors = Row("Colores del ciclo", SliderWithLabel(trkColors, lblColors));
            panel.Controls.Add(rowColors);

            trkTrail = Slider(1, 10, 1);
            lblTrail = ValueLabel();
            trkTrail.ValueChanged += (s, e) => OnEdited();
            rowTrail = Row("Longitud de estela", SliderWithLabel(trkTrail, lblTrail));
            panel.Controls.Add(rowTrail);

            trkDensity = Slider(1, 10, 1);
            lblDensity = ValueLabel();
            trkDensity.ValueChanged += (s, e) => OnEdited();
            rowDensity = Row("Densidad", SliderWithLabel(trkDensity, lblDensity));
            panel.Controls.Add(rowDensity);

            lblHint = new Label { AutoSize = true, ForeColor = Text2, MaximumSize = new Size(480, 0), Margin = new Padding(0, 12, 0, 0) };
            panel.Controls.Add(lblHint);
            return card;
        }

        private Control BuildBottomBar()
        {
            var bar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Margin = new Padding(0, 12, 0, 0) };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            lblStatus = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Text2, AutoEllipsis = true };
            bar.Controls.Add(lblStatus, 0, 0);

            btnCalibrate = SmallButton("Calibrar LEDs…", (s, e) => Calibrate());
            btnCalibrate.Anchor = AnchorStyles.Left;
            btnCalibrate.Margin = new Padding(12, 0, 0, 0);
            bar.Controls.Add(btnCalibrate, 1, 0);

            chkAutostart = new CheckBox
            {
                Text = "Iniciar RGB Helper con Windows",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(12, 0, 16, 0),
                Checked = helper.AutostartPreference,
            };
            chkAutostart.CheckedChanged += (s, e) => AutostartChanged();
            bar.Controls.Add(chkAutostart, 2, 0);

            btnApply = new Button
            {
                Text = "Aplicar",
                Width = 180,
                Height = 44,
                FlatStyle = FlatStyle.Flat,
                BackColor = Accent,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 11f),
                Cursor = Cursors.Hand,
                Margin = new Padding(0),
            };
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.FlatAppearance.MouseOverBackColor = Blend(Accent, Color.White, 0.85f);
            btnApply.Click += (s, e) => ApplySelected();
            bar.Controls.Add(btnApply, 3, 0);
            return bar;
        }

        private Label Title(string text) =>
            new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI Semibold", 12f), ForeColor = Text1, Margin = new Padding(0, 0, 0, 10) };

        private Control Row(string caption, Control editor)
        {
            var row = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 4) };
            row.Controls.Add(new Label { Text = caption, AutoSize = true, ForeColor = Text2, Margin = new Padding(0, 0, 0, 4) });
            row.Controls.Add(editor);
            return row;
        }

        private static TrackBar Slider(int min, int max, int tick) =>
            new TrackBar { Minimum = min, Maximum = max, TickFrequency = tick, Width = 260, BackColor = Card, AutoSize = false, Height = 32 };

        private Label ValueLabel() =>
            new Label { AutoSize = false, Width = 70, Height = 32, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Text1 };

        private static Control SliderWithLabel(TrackBar trk, Label lbl)
        {
            var p = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            p.Controls.Add(trk);
            p.Controls.Add(lbl);
            return p;
        }

        private Button SmallButton(string text, EventHandler onClick)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = ButtonBg,
                ForeColor = Text1,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 5, 0),
                Padding = new Padding(3, 2, 3, 2),
            };
            b.FlatAppearance.BorderColor = Border;
            b.Click += onClick;
            return b;
        }

        private void DrawPresetItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
                return;
            var g = e.Graphics;
            RenderPresetItem(g, (Preset)list.Items[e.Index], e.Bounds, (e.State & DrawItemState.Selected) != 0);

            // Separador entre los presets del programa y los del usuario.
            if (e.Index == FirstUserIndex && e.Index > 0)
                using (var pen = new Pen(Color.FromArgb(175, 175, 175)))
                    g.DrawLine(pen, e.Bounds.X + 10, e.Bounds.Top, e.Bounds.Right - 10, e.Bounds.Top);

            if (dragFrom < 0)
                return;
            // Mientras se arrastra: el preset de origen se atenúa y una línea marca dónde caerá.
            if (e.Index == dragFrom)
                using (var veil = new SolidBrush(Color.FromArgb(170, 255, 255, 255)))
                    g.FillRectangle(veil, e.Bounds);
            if (e.Index == dropIndex && dropIndex != dragFrom)
            {
                int y = dropIndex < dragFrom ? e.Bounds.Top : e.Bounds.Bottom - 3;
                using (var line = new SolidBrush(Accent))
                    g.FillRectangle(line, e.Bounds.X + 6, y, e.Bounds.Width - 12, 3);
            }
        }

        /// <summary>Dibuja un preset de la lista (también se usa para la imagen "fantasma" al arrastrar).</summary>
        private void RenderPresetItem(Graphics g, Preset p, Rectangle bounds, bool selected)
        {

            using (var bg = new SolidBrush(selected ? SelectedBg : Card))
                g.FillRectangle(bg, bounds);
            if (selected)
                using (var bar = new SolidBrush(Accent))
                    g.FillRectangle(bar, bounds.X, bounds.Y + 12, 3, bounds.Height - 24);

            var icon = new RectangleF(bounds.X + 14, bounds.Y + 12, 28, 28);
            PresetIcons.Draw(g, p, icon);

            float x = icon.Right + 14;
            float w = bounds.Right - x - 6;
            bool applied = p.Name == store.LastApplied;
            using (var fmt = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                using (var f1 = new Font("Segoe UI", 10f, applied ? FontStyle.Bold : FontStyle.Regular))
                using (var b1 = new SolidBrush(Text1))
                    g.DrawString(p.Name, f1, b1, new RectangleF(x, bounds.Y + 6, w, 22), fmt);
                string sub = (applied ? "Aplicado · " : "") + Summary(p);
                using (var f2 = new Font("Segoe UI", 8.5f))
                using (var b2 = new SolidBrush(applied ? Accent : Text2))
                    g.DrawString(sub, f2, b2, new RectangleF(x, bounds.Y + 28, w, 18), fmt);
            }
        }

        /// <summary>Resumen corto de los ajustes para el subtítulo de la lista.</summary>
        private static string Summary(Preset p)
        {
            if (p.Effect == PresetEffect.Rainbow)
                return "Arcoíris de fábrica";

            string detail;
            int nColors = p.ColorValues().Length;
            switch (p.Effect)
            {
                case PresetEffect.Off: detail = "Luces apagadas"; break;
                case PresetEffect.Static: detail = "Brillo " + p.Brightness + " %"; break;
                case PresetEffect.ColorCycle: detail = p.ColorCount + " colores · velocidad " + p.Speed; break;
                case PresetEffect.GradientStatic: detail = nColors + " colores · brillo " + p.Brightness + " %"; break;
                case PresetEffect.GradientRotate:
                case PresetEffect.CustomCycle:
                case PresetEffect.BreathingMulti:
                case PresetEffect.Aurora:
                case PresetEffect.Chase: detail = nColors + " colores · velocidad " + p.Speed; break;
                case PresetEffect.DoubleComet: detail = "2 colores · velocidad " + p.Speed + " · estela " + p.Trail; break;
                case PresetEffect.Comet:
                case PresetEffect.Scanner: detail = "Velocidad " + p.Speed + " · estela " + p.Trail; break;
                case PresetEffect.Storm: detail = "Frecuencia " + p.Density + " · brillo " + p.Brightness + " %"; break;
                case PresetEffect.Twinkle: detail = nColors + " colores · densidad " + p.Density; break;
                default: detail = "Velocidad " + p.Speed + " · brillo " + p.Brightness + " %"; break;
            }
            string effect = Preset.EffectName(p.Effect);
            string text = string.Equals(effect, p.Name, StringComparison.OrdinalIgnoreCase) ? detail : effect + " · " + detail.ToLowerInvariant();
            return Preset.IsAdvanced(p.Effect) ? "Avanzado · " + text : text;
        }

        // ---------------------------------------------------------------- lógica

        private void RefreshList(string selectName)
        {
            loading = true;
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var p in store.Presets)
                list.Items.Add(p);
            list.EndUpdate();
            loading = false;

            int idx = 0;
            for (int i = 0; i < list.Items.Count; i++)
                if (((Preset)list.Items[i]).Name == selectName)
                    idx = i;
            list.SelectedIndex = list.Items.Count > 0 ? idx : -1;
        }

        private void LoadEditor()
        {
            var p = Selected;
            if (p == null || loading)
                return;

            loading = true;
            txtName.Text = p.Name;
            cmbEffect.SelectedIndex = EffectToIndex(p.Effect);
            trkBrightness.Value = Math.Max(0, Math.Min(100, p.Brightness));
            trkSpeed.Value = Math.Max(1, Math.Min(10, p.Speed));
            trkColors.Value = Math.Max(2, Math.Min(7, p.ColorCount));
            trkTrail.Value = Math.Max(1, Math.Min(10, p.Trail));
            trkDensity.Value = Math.Max(1, Math.Min(10, p.Density));
            colorList.Colors = p.Colors ?? DefaultGradient.ToList();
            rbClockwise.Checked = p.Clockwise;
            rbCounter.Checked = !p.Clockwise;
            loading = false;

            // Los presets del programa conservan su nombre y efecto; sus ajustes sí se pueden cambiar.
            txtName.ReadOnly = p.IsDefault;
            cmbEffect.Enabled = !p.IsDefault;
            btnDelete.Enabled = !p.IsDefault;
            rowIcon.Visible = !p.IsDefault;
            UpdateEditorVisuals(p);
        }

        // ---------------------------------------------------------------- orden de la lista

        /// <summary>Posición del primer preset del usuario: los del programa van siempre delante.</summary>
        private int FirstUserIndex => store.Presets.Count(p => p.IsDefault);

        private void MovePreset(Preset p, int target)
        {
            int from = store.Presets.IndexOf(p);
            target = Math.Max(FirstUserIndex, Math.Min(store.Presets.Count - 1, target));
            if (from < 0 || from == target)
                return;
            store.Presets.RemoveAt(from);
            store.Presets.Insert(target, p);
            SaveStore();
            RefreshList(p.Name);
        }

        private void ListMouseDown(object sender, MouseEventArgs e)
        {
            dragIndex = list.IndexFromPoint(e.Location);
            dragStart = e.Location;
        }

        private void ListMouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || dragIndex < 0 || dragIndex >= list.Items.Count)
                return;
            var p = (Preset)list.Items[dragIndex];
            var drag = SystemInformation.DragSize;
            if (p.IsDefault || (Math.Abs(e.X - dragStart.X) < drag.Width && Math.Abs(e.Y - dragStart.Y) < drag.Height))
                return;

            // Imagen "fantasma" semitransparente del preset que sigue al ratón mientras se arrastra.
            var bounds = list.GetItemRectangle(dragIndex);
            var image = new Bitmap(bounds.Width, bounds.Height);
            using (var g = Graphics.FromImage(image))
            {
                RenderPresetItem(g, p, new Rectangle(Point.Empty, bounds.Size), true);
                using (var pen = new Pen(Accent))
                    g.DrawRectangle(pen, 0, 0, bounds.Width - 1, bounds.Height - 1);
            }
            ghostOffset = new Point(dragStart.X - bounds.X, dragStart.Y - bounds.Y);
            dragFrom = dragIndex;
            dropIndex = dragIndex;
            using (ghost = new GhostForm(image))
            {
                MoveGhost();
                ghost.Show();
                list.Invalidate();
                list.DoDragDrop(p, DragDropEffects.Move);
            }
            ghost = null;
            image.Dispose();
            dragIndex = -1;
            dragFrom = -1;
            dropIndex = -1;
            list.Invalidate();
        }

        private void MoveGhost()
        {
            if (ghost != null)
                ghost.Location = new Point(Cursor.Position.X - ghostOffset.X, Cursor.Position.Y - ghostOffset.Y);
        }

        /// <summary>Posición de destino al soltar en el punto de pantalla dado: nunca entre los presets del programa.</summary>
        private int DropTarget(int screenX, int screenY)
        {
            int target = list.IndexFromPoint(list.PointToClient(new Point(screenX, screenY)));
            if (target < 0)
                target = store.Presets.Count - 1;
            return Math.Max(FirstUserIndex, Math.Min(store.Presets.Count - 1, target));
        }

        private void ListDragOver(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(typeof(Preset)) ? DragDropEffects.Move : DragDropEffects.None;
            int target = DropTarget(e.X, e.Y);
            if (target != dropIndex)
            {
                dropIndex = target;
                list.Invalidate();
            }
        }

        private void ListDragDrop(object sender, DragEventArgs e)
        {
            var p = e.Data.GetData(typeof(Preset)) as Preset;
            if (p == null)
                return;
            MovePreset(p, DropTarget(e.X, e.Y));
        }

        private void PickIcon()
        {
            var p = Selected;
            if (p == null || p.IsDefault)
                return;
            using (var dlg = new IconPickerForm(p, Accent, SelectedBg))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                p.Icon = string.IsNullOrEmpty(dlg.SelectedIcon) ? null : dlg.SelectedIcon;
            }
            SaveStore();
            list.Invalidate();
            btnIcon.Invalidate();
        }

        private void OnEdited(bool nameChanged = false)
        {
            var p = Selected;
            if (p == null || loading)
                return;

            if (nameChanged)
            {
                string name = txtName.Text.Trim();
                if (p.IsDefault || name.Length == 0 || store.Presets.Any(o => o != p && o.Name == name))
                    return; // nombre bloqueado, vacío o repetido: se ignora hasta que sea válido
                if (store.LastApplied == p.Name)
                    store.LastApplied = name;
                p.Name = name;
            }
            if (!p.IsDefault && cmbEffect.SelectedItem is EffectItem item && item.Effect.HasValue)
                p.Effect = item.Effect.Value;
            p.Brightness = trkBrightness.Value;
            p.Speed = trkSpeed.Value;
            p.ColorCount = trkColors.Value;
            p.Trail = trkTrail.Value;
            p.Density = trkDensity.Value;
            p.Clockwise = rbClockwise.Checked;
            if (Preset.UsesColorList(p.Effect) || p.Colors != null)
                p.Colors = colorList.Colors;

            SaveStore();
            list.Invalidate();
            UpdateEditorVisuals(p);
        }

        private void UpdateEditorVisuals(Preset p)
        {
            rowColor.Visible = Preset.UsesColor(p.Effect);
            rowColorList.Visible = Preset.UsesColorList(p.Effect);
            rowDirection.Visible = Preset.UsesDirection(p.Effect);
            rowBackground.Visible = Preset.UsesBackground(p.Effect);
            rowBrightness.Visible = Preset.UsesBrightness(p.Effect);
            rowSpeed.Visible = Preset.UsesSpeed(p.Effect);
            rowColors.Visible = p.Effect == PresetEffect.ColorCycle;
            rowTrail.Visible = Preset.UsesTrail(p.Effect);
            rowDensity.Visible = Preset.UsesDensity(p.Effect);

            // Algunas filas cambian de nombre según el efecto.
            bool storm = p.Effect == PresetEffect.Storm;
            RowCaption(rowDensity).Text = storm ? "Frecuencia de relámpagos" : "Densidad";
            RowCaption(rowColor).Text = storm ? "Color del relámpago"
                : p.Effect == PresetEffect.Comet || p.Effect == PresetEffect.Scanner ? "Color de la luz" : "Color";
            RowCaption(rowColorList).Text = p.Effect == PresetEffect.DoubleComet ? "Colores (se usan los dos primeros)" : "Colores";

            PaintColorButton(btnColor, p.ColorValue, p.Color);
            PaintColorButton(btnBackground, p.BackgroundValue, p.Background);

            btnIcon.Invalidate();
            lblBrightness.Text = trkBrightness.Value + " %";
            lblSpeed.Text = trkSpeed.Value + " / 10";
            lblColors.Text = trkColors.Value.ToString();
            lblTrail.Text = trkTrail.Value + " LEDs";
            lblDensity.Text = trkDensity.Value + " / 10";

            string hint = "";
            if (p.Effect == PresetEffect.Rainbow)
                hint = "Arcoíris de fábrica: su velocidad y su brillo no se pueden cambiar.";
            else if (p.Effect == PresetEffect.ColorCycle)
                hint = "Recorre una paleta de colores fija; aquí eliges cuántos colores usa.";
            if (p.IsDefault)
                hint = (hint.Length > 0 ? hint + "\n\n" : "") + "Preset del programa: puedes cambiar sus ajustes. Para crear otro con distinto nombre o efecto, pulsa «Duplicar» o «Nuevo».";
            lblHint.Text = hint;
            lblHint.Visible = hint.Length > 0;
        }

        private static Label RowCaption(Control row) => (Label)row.Controls[0];

        private static void PaintColorButton(Button b, uint value, string text)
        {
            var c = PresetIcons.ToColor(value);
            b.BackColor = c;
            b.ForeColor = c.GetBrightness() > 0.55f ? Color.Black : Color.White;
            b.Text = "  " + (text ?? "").ToUpperInvariant() + "   (pulsa para cambiar)";
        }

        private void PickBackground()
        {
            var p = Selected;
            if (p == null)
                return;
            using (var dlg = new ColorDialog { FullOpen = true, Color = PresetIcons.ToColor(p.BackgroundValue) })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                p.Background = "#" + ((dlg.Color.R << 16) | (dlg.Color.G << 8) | dlg.Color.B).ToString("X6");
            }
            SaveStore();
            list.Invalidate();
            UpdateEditorVisuals(p);
        }

        /// <summary>Casilla "Iniciar RGB Helper con Windows": si hay un efecto avanzado aplicado, se aplica ya.</summary>
        private void AutostartChanged()
        {
            try
            {
                helper.AutostartPreference = chkAutostart.Checked;
                var applied = store.Presets.FirstOrDefault(x => x.Name == store.LastApplied);
                if (applied == null || !Preset.IsAdvanced(applied.Effect))
                    return;
                if (chkAutostart.Checked)
                    helper.RegisterAutostart(HelperExePath);
                else
                    helper.UnregisterAutostart();
            }
            catch (Exception ex)
            {
                SetStatus("✖ " + ex.Message, Danger);
            }
        }

        private void PickColor()
        {
            var p = Selected;
            if (p == null)
                return;
            using (var dlg = new ColorDialog { FullOpen = true, Color = PresetIcons.ToColor(p.ColorValue) })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                p.ColorValue = (uint)((dlg.Color.R << 16) | (dlg.Color.G << 8) | dlg.Color.B);
            }
            SaveStore();
            list.Invalidate();
            UpdateEditorVisuals(p);
        }

        private void NewPreset()
        {
            var p = new Preset { Name = UniqueName("Mi preset"), Effect = PresetEffect.Static, Color = "#FF0080" };
            store.Presets.Add(p);
            SaveStore();
            RefreshList(p.Name);
            txtName.Focus();
            txtName.SelectAll();
        }

        private void DuplicatePreset()
        {
            var src = Selected;
            if (src == null)
                return;
            var p = src.Clone();
            p.IsDefault = false;
            p.Name = UniqueName(src.Name + " (copia)");
            store.Presets.Add(p);
            SaveStore();
            RefreshList(p.Name);
        }

        private void DeletePreset()
        {
            var p = Selected;
            if (p == null || p.IsDefault)
                return;
            if (MessageBox.Show(this, "¿Eliminar el preset «" + p.Name + "»?", "RGB Control",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            store.Presets.Remove(p);
            SaveStore();
            RefreshList(store.LastApplied);
        }

        private string UniqueName(string baseName)
        {
            string name = baseName;
            for (int i = 2; store.Presets.Any(o => o.Name == name); i++)
                name = baseName + " " + i;
            return name;
        }

        private void SaveStore()
        {
            try
            {
                store.Save();
            }
            catch (Exception ex)
            {
                SetStatus("No se pudieron guardar los presets: " + ex.Message, Danger);
            }
        }

        private async void ApplySelected()
        {
            if (Selected != null)
                await ApplyPreset(Selected);
        }

        /// <summary>Aplica un preset (normal: en la placa; avanzado: con el ayudante). Devuelve true si salió bien.</summary>
        private async Task<bool> ApplyPreset(Preset p)
        {
            if (p == null || !btnApply.Enabled)
                return false;

            bool advanced = Preset.IsAdvanced(p.Effect);
            if (advanced && !File.Exists(HelperExePath))
            {
                SetStatus("✖ No se encuentra RGB Helper.exe junto al programa.", Danger);
                return false;
            }

            btnApply.Enabled = false;
            SetStatus("Aplicando «" + p.Name + "»…", Text2);
            var snapshot = p.Clone();

            // Versiones anteriores no guardaban el último preset normal: se toma el aplicado hasta ahora.
            if (store.LastChipPreset == null)
            {
                var previous = store.Presets.FirstOrDefault(x => x.Name == store.LastApplied);
                if (previous != null && !Preset.IsAdvanced(previous.Effect))
                    store.LastChipPreset = previous.Name;
            }
            Preset lastChip = store.Presets.FirstOrDefault(x => x.Name == store.LastChipPreset)?.Clone();
            int[] leds = (int[])store.LedCounts.Clone();
            uint[] orders = store.ByteOrders;
            bool autostart = chkAutostart.Checked;

            try
            {
                bool windowsWasOn = await Task.Run(() =>
                {
                    bool wasOn = WindowsDynamicLighting.IsEnabled();
                    if (wasOn)
                        WindowsDynamicLighting.Disable();

                    if (advanced)
                    {
                        if (orders == null)
                            using (var c = OpenController())
                                orders = c.ReadByteOrders();
                        HelperConfig.Write(Path.Combine(DataFolder.Path, HelperConfig.FileName), HelperConfig.Build(snapshot, leds, orders, lastChip));
                        if (autostart)
                            helper.RegisterAutostart(HelperExePath);
                        else
                            helper.UnregisterAutostart();
                        helper.StartOrReload(HelperExePath);
                    }
                    else
                    {
                        StopHelper();
                        helper.UnregisterAutostart();
                        // Sin helper.cfg no hay efecto avanzado activo: Motherboard Panel no arranca el ayudante.
                        File.Delete(Path.Combine(DataFolder.Path, HelperConfig.FileName));
                        using (var c = OpenController())
                            PresetEngine.Apply(c, snapshot);
                    }
                    return wasOn;
                });

                store.ByteOrders = orders;
                store.LastApplied = p.Name;
                if (!advanced)
                    store.LastChipPreset = p.Name;
                SaveStore();
                list.Invalidate();
                string extra = windowsWasOn ? " (Iluminación dinámica de Windows desactivada)" : "";
                string tip = advanced && !store.LedCountsCalibrated
                    ? " Consejo: usa «Calibrar LEDs…» para que el efecto ocupe cada ventilador entero."
                    : "";
                SetStatus(advanced
                    ? "✔ «" + p.Name + "» aplicado." + extra + tip
                    : "✔ «" + p.Name + "» aplicado y guardado en la placa. Ya puedes cerrar el programa." + extra, Success);
                return true;
            }
            catch (Exception ex)
            {
                SetStatus("✖ " + ex.Message, Danger);
                return false;
            }
            finally
            {
                btnApply.Enabled = true;
            }
        }

        /// <summary>
        /// "Calibrar LEDs…": detiene el ayudante para que no pise la vista en directo, abre el asistente y,
        /// al terminar (guardando o no), vuelve a aplicar el último preset con los LEDs que haya.
        /// </summary>
        private async void Calibrate()
        {
            btnCalibrate.Enabled = false;
            try
            {
                await Task.Run(() => StopHelper());
                bool saved;
                using (var dlg = new CalibrationForm(store, Accent))
                    saved = dlg.ShowDialog(this) == DialogResult.OK;

                var last = store.Presets.FirstOrDefault(x => x.Name == store.LastApplied);
                bool reapplied = last != null && await ApplyPreset(last);
                if (saved)
                {
                    int[] c = store.LedCounts;
                    SetStatus("✔ LEDs guardados (D_LED1: " + c[0] + ", D_LED2: " + c[1] + ", D_LED3: " + c[2] + ")" +
                              (reapplied ? " y «" + last.Name + "» aplicado de nuevo." : "."), Success);
                }
                else if (last == null)
                    SetStatus("Calibración cancelada. Elige un preset y pulsa Aplicar.", Text2);
            }
            finally
            {
                btnCalibrate.Enabled = true;
            }
        }

        private static Fusion2Controller OpenController() =>
            Fusion2Controller.Open() ?? throw new InvalidOperationException("No se encontró el controlador RGB de la placa (ITE 048D:5711).");

        /// <summary>Pide al ayudante que se cierre (sin restaurar) y espera hasta 3 s a que termine.</summary>
        private void StopHelper()
        {
            if (!helper.IsHelperRunning())
                return;
            helper.SignalExit();
            for (int i = 0; i < 60 && helper.IsHelperRunning(); i++)
                Thread.Sleep(50);
        }

        private int EffectToIndex(PresetEffect e) => (int)e < AdvancedHeaderIndex ? (int)e : (int)e + 1;

        private void DrawEffectItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
                return;
            var item = (EffectItem)cmbEffect.Items[e.Index];
            bool header = !item.Effect.HasValue;
            bool selected = !header && (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;

            using (var bg = new SolidBrush(selected ? SelectedBg : (cmbEffect.Enabled ? Card : SystemColors.Control)))
                e.Graphics.FillRectangle(bg, e.Bounds);
            var font = header ? new Font(Font.FontFamily, 8.5f, FontStyle.Bold) : Font;
            var color = header ? Text2 : (cmbEffect.Enabled ? Text1 : SystemColors.GrayText);
            int indent = header || (e.State & DrawItemState.ComboBoxEdit) != 0 ? 4 : 12;
            TextRenderer.DrawText(e.Graphics, item.ToString(), font, new Rectangle(e.Bounds.X + indent, e.Bounds.Y, e.Bounds.Width - indent, e.Bounds.Height),
                color, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            if (header)
                font.Dispose();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == ShowMessage)
            {
                // Otra copia de la ventana (o el ayudante) pide traer esta al frente.
                if (WindowState == FormWindowState.Minimized)
                    WindowState = FormWindowState.Normal;
                Activate();
                return;
            }
            base.WndProc(ref m);
        }

        private void SetStatus(string text, Color color)
        {
            lblStatus.Text = text;
            lblStatus.ForeColor = color;
        }

        private static Color SystemAccent()
        {
            try
            {
                // DWM guarda el color de acento como 0xAABBGGRR.
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    if (key?.GetValue("AccentColor") is int abgr)
                        return Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
                }
            }
            catch
            {
                // Se usa el azul de Windows.
            }
            return Color.FromArgb(0, 103, 192);
        }

        private static Color Blend(Color a, Color b, float amountOfA) =>
            Color.FromArgb(
                (int)(a.R * amountOfA + b.R * (1 - amountOfA)),
                (int)(a.G * amountOfA + b.G * (1 - amountOfA)),
                (int)(a.B * amountOfA + b.B * (1 - amountOfA)));

        /// <summary>Elemento del desplegable de efectos; sin efecto es la cabecera "Avanzados".</summary>
        private sealed class EffectItem
        {
            public readonly PresetEffect? Effect;
            public EffectItem(PresetEffect? e) { Effect = e; }
            public override string ToString() => Effect.HasValue ? Preset.EffectName(Effect.Value) : "AVANZADOS";
        }

        /// <summary>Panel blanco con borde fino, como las tarjetas de Configuración de Windows 11.</summary>
        private sealed class CardPanel : TableLayoutPanel
        {
            public CardPanel()
            {
                BackColor = Card;
                DoubleBuffered = true;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using (var pen = new Pen(Border))
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}
