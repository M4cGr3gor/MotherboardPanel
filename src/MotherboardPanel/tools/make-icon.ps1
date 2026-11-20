# Genera app.ico de Motherboard Panel: un chip de placa base (con patillas) sobre un cuadrado redondeado
# gris pizarra, y una franja con los colores del arcoíris debajo (las luces). Varios tamaños, PNG dentro de ICO.
param([string]$Out = (Join-Path $PSScriptRoot '..\app.ico'), [string]$PreviewPng)

Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class PanelIconRenderer
{
    public static Bitmap Render(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        float s = size;
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Fondo: cuadrado redondeado con degradado de pizarra a casi negro.
            using (var path = RoundedRect(0, 0, s, s, s * 0.22f))
            using (var bg = new LinearGradientBrush(new PointF(0, 0), new PointF(s, s), Color.FromArgb(52, 62, 80), Color.FromArgb(18, 22, 30)))
                g.FillPath(bg, path);

            // Chip: cuadrado central con patillas a los cuatro lados.
            float chip = s * 0.42f, cx = (s - chip) / 2, cy = s * 0.16f;
            float pin = Math.Max(1f, s * 0.045f), pinLen = s * 0.08f;
            using (var pinBrush = new SolidBrush(Color.FromArgb(200, 205, 215)))
            {
                for (int i = 0; i < 3; i++)
                {
                    float o = chip * (0.22f + 0.28f * i) - pin / 2;
                    g.FillRectangle(pinBrush, cx + o, cy - pinLen, pin, pinLen);          // arriba
                    g.FillRectangle(pinBrush, cx + o, cy + chip, pin, pinLen);            // abajo
                    g.FillRectangle(pinBrush, cx - pinLen, cy + o, pinLen, pin);          // izquierda
                    g.FillRectangle(pinBrush, cx + chip, cy + o, pinLen, pin);            // derecha
                }
            }
            using (var chipPath = RoundedRect(cx, cy, chip, chip, s * 0.05f))
            using (var chipBrush = new LinearGradientBrush(new PointF(cx, cy), new PointF(cx + chip, cy + chip), Color.FromArgb(70, 80, 98), Color.FromArgb(34, 40, 52)))
            using (var edge = new Pen(Color.FromArgb(150, 160, 178), Math.Max(1f, s * 0.025f)))
            {
                g.FillPath(chipBrush, chipPath);
                g.DrawPath(edge, chipPath);
            }
            // Punto de luz en el chip.
            float dot = chip * 0.3f;
            using (var dotBrush = new SolidBrush(Color.FromArgb(120, 230, 255)))
                g.FillEllipse(dotBrush, cx + (chip - dot) / 2, cy + (chip - dot) / 2, dot, dot);

            // Franja arcoíris (las luces).
            float bx = s * 0.14f, by = s * 0.74f, bw = s * 0.72f, bh = Math.Max(2f, s * 0.11f);
            using (var bar = RoundedRect(bx, by, bw, bh, bh / 2))
            using (var rainbow = new LinearGradientBrush(new PointF(bx, 0), new PointF(bx + bw, 0), Color.Red, Color.Violet))
            {
                var blend = new ColorBlend();
                blend.Colors = new[] { Color.FromArgb(255, 70, 70), Color.FromArgb(255, 190, 40), Color.FromArgb(90, 230, 90), Color.FromArgb(50, 200, 255), Color.FromArgb(180, 110, 255) };
                blend.Positions = new[] { 0f, 0.25f, 0.5f, 0.75f, 1f };
                rainbow.InterpolationColors = blend;
                g.FillPath(rainbow, bar);
            }
        }
        return bmp;
    }

    private static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath();
        float d = Math.Min(r * 2, Math.Min(w, h));
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
'@

$sizes = 16, 24, 32, 48, 64, 256
$pngs = @()
foreach ($s in $sizes) {
    $bmp = [PanelIconRenderer]::Render($s)
    if ($PreviewPng -and $s -eq 256) { $bmp.Save($PreviewPng, [System.Drawing.Imaging.ImageFormat]::Png) }
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()
    $bmp.Dispose()
}

$fs = [System.IO.File]::Create((Resolve-Path -LiteralPath (Split-Path $Out)).Path + '\' + (Split-Path $Out -Leaf))
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $dim = $(if ($s -ge 256) { 0 } else { $s })
    $bw.Write([byte]$dim); $bw.Write([byte]$dim)
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Close()
Write-Output "Icono creado: $Out"
