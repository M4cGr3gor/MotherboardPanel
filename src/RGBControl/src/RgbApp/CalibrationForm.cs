using System;
using System.Drawing;
using System.Windows.Forms;
using RgbCore;

namespace RgbApp
{
    /// <summary>
    /// Asistente "Calibrar LEDs": conector por conector, enciende n LEDs (el último en rojo) mientras el
    /// usuario ajusta n con − y +. Al guardar escribe los LEDs en presets.json.
    /// </summary>
    internal sealed class CalibrationForm : Form
    {
        private static readonly Color Text1 = Color.FromArgb(27, 27, 27);
        private static readonly Color Text2 = Color.FromArgb(96, 96, 96);
        private static readonly Color Border = Color.FromArgb(229, 229, 229);
        private static readonly Color ButtonBg = Color.FromArgb(251, 251, 251);
        private static readonly Color Danger = Color.FromArgb(196, 43, 28);

        private readonly PresetStore store;
        private readonly CalibrationSession session;
        private readonly Color accent;
        private Fusion2Controller controller;
        private uint[] byteOrders;

        private Label lblConnector, lblCount, lblError;
        private Button btnMinus, btnPlus, btnPrevious, btnNext;
        private CheckBox chkEmpty;
        private bool updating;

        public CalibrationForm(PresetStore store, Color accent)
        {
            this.store = store;
            this.accent = accent;
            session = new CalibrationSession(store.LedCounts);

            Text = "Calibrar LEDs";
            Font = new Font("Segoe UI", 10f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.White;
            ForeColor = Text1;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            KeyPreview = true;
            KeyDown += OnKeyDown;

            BuildUi();
        }

        private void BuildUi()
        {
            var root = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(20, 16, 20, 16),
                BackColor = Color.White,
            };
            Controls.Add(root);

            lblConnector = new Label { AutoSize = true, Font = new Font("Segoe UI Semibold", 12f), Margin = new Padding(0, 0, 0, 6) };
            root.Controls.Add(lblConnector);

            root.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(440, 0),
                ForeColor = Text2,
                Margin = new Padding(0, 0, 0, 14),
                Text = "Ajusta hasta que el LED rojo sea el último de un ventilador o de la tira. " +
                       "Si en este conector hay varias cosas, usa la que tenga más LEDs.",
            });

            var counter = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 8) };
            btnMinus = SmallButton("−", (s, e) => Change(session.Decrease));
            btnPlus = SmallButton("+", (s, e) => Change(session.Increase));
            foreach (var b in new[] { btnMinus, btnPlus })
            {
                b.AutoSize = false;
                b.Size = new Size(44, 40);
                b.Font = new Font("Segoe UI Semibold", 14f);
            }
            lblCount = new Label
            {
                AutoSize = false,
                Size = new Size(140, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Semibold", 16f),
            };
            counter.Controls.AddRange(new Control[] { btnMinus, lblCount, btnPlus });
            root.Controls.Add(counter);

            chkEmpty = new CheckBox { Text = "No hay nada conectado en este conector", AutoSize = true, Margin = new Padding(0, 0, 0, 10) };
            chkEmpty.CheckedChanged += (s, e) =>
            {
                if (!updating)
                    Change(() => session.SetEmpty(chkEmpty.Checked));
            };
            root.Controls.Add(chkEmpty);

            lblError = new Label { AutoSize = true, MaximumSize = new Size(440, 0), ForeColor = Danger, Visible = false, Margin = new Padding(0, 0, 0, 8) };
            root.Controls.Add(lblError);

            var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
            btnPrevious = SmallButton("Anterior", (s, e) => Change(() => session.Previous()));
            btnNext = new Button
            {
                AutoSize = true,
                MinimumSize = new Size(110, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = accent,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0),
                Padding = new Padding(6, 3, 6, 3),
            };
            btnNext.FlatAppearance.BorderSize = 0;
            btnNext.Click += (s, e) => NextOrSave();
            var btnCancel = SmallButton("Cancelar", (s, e) => Close());
            buttons.Controls.AddRange(new Control[] { btnPrevious, btnNext, btnCancel });
            root.Controls.Add(buttons);
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
                Margin = new Padding(0, 0, 6, 0),
                Padding = new Padding(6, 3, 6, 3),
            };
            b.FlatAppearance.BorderColor = Border;
            b.Click += onClick;
            return b;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                controller = Fusion2Controller.Open()
                             ?? throw new InvalidOperationException("No se encontró el controlador RGB de la placa (ITE 048D:5711).");
                byteOrders = store.ByteOrders ?? controller.ReadByteOrders();
                store.ByteOrders = byteOrders;
                // 256 LEDs por conector para poder encender cualquier cantidad mientras se calibra.
                controller.EnterDirectMode(new[] { 256, 256, 256 });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Calibrar LEDs", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }
            UpdateView(sendFrame: true);
        }

        private void Change(Action change)
        {
            change();
            UpdateView(sendFrame: true);
        }

        private void Change(Func<bool> change)
        {
            change();
            UpdateView(sendFrame: true);
        }

        private void UpdateView(bool sendFrame)
        {
            updating = true;
            lblConnector.Text = "Conector " + (session.Connector + 1) + " de 3 (D_LED" + (session.Connector + 1) + ")";
            lblCount.Text = session.IsEmpty ? "—" : session.Count + " LEDs";
            chkEmpty.Checked = session.IsEmpty;
            btnMinus.Enabled = btnPlus.Enabled = !session.IsEmpty;
            btnPrevious.Enabled = session.Connector > 0;
            btnNext.Text = session.IsLast ? "Guardar" : "Siguiente";
            updating = false;

            if (sendFrame)
                SendFrame();
        }

        private void SendFrame()
        {
            if (controller == null)
                return;
            try
            {
                for (int i = 0; i < 3; i++)
                    controller.SetStripColors(Fusion2Controller.DirectHeaders[i], session.Frame(i, CalibrationSession.MaxCount), byteOrders[i]);
                lblError.Visible = false;
            }
            catch (Exception ex)
            {
                lblError.Text = "No se pudo enviar a la placa: " + ex.Message;
                lblError.Visible = true;
            }
        }

        private void NextOrSave()
        {
            if (session.Next())
            {
                UpdateView(sendFrame: true);
                return;
            }
            store.LedCounts = session.Result();
            store.LedCountsCalibrated = true;
            try
            {
                store.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudieron guardar los LEDs: " + ex.Message, "Calibrar LEDs", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            DialogResult = DialogResult.OK;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (session.IsEmpty)
                return;
            if (e.KeyCode == Keys.Add || e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Right)
                Change(session.Increase);
            else if (e.KeyCode == Keys.Subtract || e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Left)
                Change(session.Decrease);
            else
                return;
            e.Handled = true;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            controller?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
