using System.Drawing;
using System.Windows.Forms;

namespace RgbApp
{
    /// <summary>
    /// Ventana semitransparente sin bordes que muestra una imagen bajo el ratón al arrastrar.
    /// No recibe el foco ni el ratón, para que el arrastre siga llegando a la lista de debajo.
    /// </summary>
    internal sealed class GhostForm : Form
    {
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        public GhostForm(Image image)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Opacity = 0.75;
            BackgroundImage = image;
            BackgroundImageLayout = ImageLayout.None;
            ClientSize = image.Size;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }
    }
}
