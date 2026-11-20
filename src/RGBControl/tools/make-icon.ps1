# Genera src\RgbApp\app.ico: tres círculos de luz roja, verde y azul que se suman (blanco en el centro)
# sobre un cuadrado redondeado con degradado violeta. Varios tamaños, PNG dentro de ICO.
param([string]$Out = (Join-Path $PSScriptRoot '..\src\RgbApp\app.ico'), [string]$PreviewPng)

Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;

public static class RgbIconRenderer
{
    public static Bitmap Render(int size)
    {
        const int ss = 4; // supermuestreo para bordes suaves
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        double s = size;
        double radius = s * 0.22;            // esquinas del fondo
        double cr = s * 0.235;               // radio de cada círculo de luz
        double cx = s / 2, cy = s * 0.52, d = s * 0.135;
        double[][] centers =
        {
            new[] { cx, cy - d },                          // rojo arriba
            new[] { cx - d * 0.94, cy + d * 0.62 },        // verde abajo izquierda
            new[] { cx + d * 0.94, cy + d * 0.62 },        // azul abajo derecha
        };

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            double a = 0, r = 0, g = 0, b = 0;
            for (int sy = 0; sy < ss; sy++)
            for (int sx = 0; sx < ss; sx++)
            {
                double px = x + (sx + 0.5) / ss, py = y + (sy + 0.5) / ss;
                if (!InsideRoundedRect(px, py, s, radius))
                    continue;

                // Fondo: degradado diagonal de violeta oscuro a índigo
                double t = (px + py) / (2 * s);
                double br = Lerp(32, 70, t), bg = Lerp(18, 40, t), bb = Lerp(64, 140, t);

                // Luz: cada círculo suma su canal, con un halo suave alrededor
                double lr = Light(px, py, centers[0], cr), lg = Light(px, py, centers[1], cr), lb = Light(px, py, centers[2], cr);
                double pr = Math.Min(255, br * (1 - lr) + 255 * lr + 40 * lg * lb);
                double pg = Math.Min(255, bg * (1 - lg) + 255 * lg + 20 * lr * lb);
                double pb = Math.Min(255, bb * (1 - lb) + 255 * lb + 20 * lr * lg);
                // Sumas de luz: rojo+verde=amarillo, etc.
                pr = Math.Min(255, pr + 255 * lr);
                pg = Math.Min(255, pg + 255 * lg + 110 * lb); // azul algo celeste para que destaque del fondo
                pb = Math.Min(255, pb + 255 * lb);

                a += 1; r += pr; g += pg; b += pb;
            }
            if (a == 0) continue;
            int n = ss * ss;
            bmp.SetPixel(x, y, Color.FromArgb((int)(255 * a / n), (int)(r / a), (int)(g / a), (int)(b / a)));
        }
        return bmp;
    }

    // Intensidad 0..1: llena dentro del círculo, con un halo que cae fuera.
    private static double Light(double x, double y, double[] c, double r)
    {
        double dist = Math.Sqrt((x - c[0]) * (x - c[0]) + (y - c[1]) * (y - c[1]));
        if (dist <= r) return 0.5;
        double halo = r * 0.35;
        return dist >= r + halo ? 0 : 0.5 * (1 - (dist - r) / halo) * 0.5;
    }

    private static bool InsideRoundedRect(double x, double y, double s, double rad)
    {
        double qx = Math.Max(Math.Max(rad - x, x - (s - rad)), 0);
        double qy = Math.Max(Math.Max(rad - y, y - (s - rad)), 0);
        return qx * qx + qy * qy <= rad * rad;
    }

    private static double Lerp(double a, double b, double t) { return a + (b - a) * t; }
}
'@

$sizes = 16, 24, 32, 48, 64, 256
$pngs = @()
foreach ($s in $sizes) {
    $bmp = [RgbIconRenderer]::Render($s)
    if ($PreviewPng -and $s -eq 256) { $bmp.Save($PreviewPng, [System.Drawing.Imaging.ImageFormat]::Png) }
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()
    $bmp.Dispose()
}

$fs = [System.IO.File]::Create($Out)
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
