using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using RgbCore;

namespace RgbApp
{
    /// <summary>
    /// Iconos dibujados de los presets. Cada preset usa el icono de su efecto o uno elegido por el usuario;
    /// se pintan con el color del preset, en arcoíris si el efecto es multicolor o en gris si está apagado.
    /// </summary>
    internal static class PresetIcons
    {
        private static readonly Color Outline = Color.FromArgb(70, 0, 0, 0);
        private static readonly Color Gray = Color.FromArgb(120, 120, 120);

        public const string Auto = "";

        /// <summary>Iconos que se pueden elegir, en el orden del selector.</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> Catalog = new List<KeyValuePair<string, string>>
        {
            Pair(Auto, "Según el efecto"),
            Pair("wave", "Onda"),
            Pair("cycle", "Ciclo"),
            Pair("gradient", "Degradado"),
            Pair("comet", "Cometa"),
            Pair("comet2", "Doble cometa"),
            Pair("candle", "Vela"),
            Pair("aurora", "Aurora"),
            Pair("scanner", "Escáner"),
            Pair("chase", "Persecución"),
            Pair("storm", "Tormenta"),
            Pair("rings", "Anillos"),
            Pair("bulb", "Bombilla"),
            Pair("bolt", "Rayo"),
            Pair("heart", "Corazón"),
            Pair("star", "Estrella"),
            Pair("sparkle", "Destello"),
            Pair("flame", "Llama"),
            Pair("drop", "Gota"),
            Pair("snowflake", "Copo de nieve"),
            Pair("leaf", "Hoja"),
            Pair("sun", "Sol"),
            Pair("moon", "Luna"),
            Pair("music", "Música"),
            Pair("gamepad", "Mando"),
            Pair("power", "Apagado"),
        };

        private static KeyValuePair<string, string> Pair(string k, string v) => new KeyValuePair<string, string>(k, v);

        public static void Draw(Graphics g, Preset p, RectangleF r) => Draw(g, p, p.Icon, r);

        /// <summary>Dibuja el icono indicado (o el del efecto si iconKey está vacío) con los colores del preset.</summary>
        public static void Draw(Graphics g, Preset p, string iconKey, RectangleF r)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            string key = string.IsNullOrEmpty(iconKey) ? EffectIcon(p.Effect) : iconKey;
            using (var brush = TintBrush(p, r))
            {
                switch (key)
                {
                    case "wave": DrawWave(g, r, brush); break;
                    case "cycle": DrawCycle(g, r, p); break;
                    case "gradient": DrawGradientBar(g, r, brush); break;
                    case "comet": DrawComet(g, r, p); break;
                    case "comet2": DrawDoubleComet(g, r, p); break;
                    case "candle": DrawCandle(g, r, brush); break;
                    case "aurora": DrawAurora(g, r, p); break;
                    case "scanner": DrawScanner(g, r, brush); break;
                    case "chase": DrawChase(g, r, p); break;
                    case "storm": DrawStorm(g, r, brush); break;
                    case "rings": DrawRings(g, r, brush); break;
                    case "bulb": DrawBulb(g, r, brush); break;
                    case "bolt": DrawBolt(g, Inset(r, 0.2f, 0), brush); break;
                    case "bolt2":
                        DrawBolt(g, new RectangleF(r.X, r.Y + r.Height * 0.1f, r.Width * 0.5f, r.Height * 0.8f), brush);
                        DrawBolt(g, new RectangleF(r.X + r.Width * 0.5f, r.Y + r.Height * 0.1f, r.Width * 0.5f, r.Height * 0.8f), brush);
                        break;
                    case "heart": FillOutlined(g, Heart(r), brush); break;
                    case "star": FillOutlined(g, Star(r, 5, 0.42f), brush); break;
                    case "sparkle": FillOutlined(g, Star(r, 4, 0.3f), brush); break;
                    case "flame": FillOutlined(g, Flame(r), brush); break;
                    case "drop": FillOutlined(g, Drop(r), brush); break;
                    case "snowflake": DrawSnowflake(g, r, brush); break;
                    case "leaf": DrawLeaf(g, r, brush); break;
                    case "sun": DrawSun(g, r, brush); break;
                    case "moon": DrawMoon(g, r, brush); break;
                    case "music": DrawMusic(g, r, brush); break;
                    case "gamepad": DrawGamepad(g, r, brush); break;
                    default: DrawPower(g, r); break;
                }
            }
        }

        private static string EffectIcon(PresetEffect e)
        {
            switch (e)
            {
                case PresetEffect.Rainbow: return "wave";
                case PresetEffect.ColorCycle: return "cycle";
                case PresetEffect.Breathing: return "rings";
                case PresetEffect.Static: return "bulb";
                case PresetEffect.Flash: return "bolt";
                case PresetEffect.DoubleFlash: return "bolt2";
                case PresetEffect.GradientStatic: return "gradient";
                case PresetEffect.GradientRotate: return "wave";
                case PresetEffect.CustomCycle: return "cycle";
                case PresetEffect.RainbowAdjustable: return "wave";
                case PresetEffect.Comet: return "comet";
                case PresetEffect.BreathingMulti: return "rings";
                case PresetEffect.Twinkle: return "sparkle";
                case PresetEffect.Fire: return "flame";
                case PresetEffect.Candle: return "candle";
                case PresetEffect.Heartbeat: return "heart";
                case PresetEffect.Aurora: return "aurora";
                case PresetEffect.Scanner: return "scanner";
                case PresetEffect.Chase: return "chase";
                case PresetEffect.DoubleComet: return "comet2";
                case PresetEffect.Storm: return "storm";
                default: return "power";
            }
        }

        public static Color ToColor(uint v) => Color.FromArgb((int)(v >> 16) & 255, (int)(v >> 8) & 255, (int)v & 255);

        public static Color Hue(float h)
        {
            h = h - (float)Math.Floor(h);
            float r = Clamp01(Math.Abs(h * 6 - 3) - 1), gr = Clamp01(2 - Math.Abs(h * 6 - 2)), b = Clamp01(2 - Math.Abs(h * 6 - 4));
            return Color.FromArgb((int)(r * 255), (int)(gr * 255), (int)(b * 255));
        }

        private static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;

        private static RectangleF Inset(RectangleF r, float fx, float fy) =>
            new RectangleF(r.X + r.Width * fx, r.Y + r.Height * fy, r.Width * (1 - 2 * fx), r.Height * (1 - 2 * fy));

        private static Brush TintBrush(Preset p, RectangleF r)
        {
            if (p.Effect == PresetEffect.Rainbow || p.Effect == PresetEffect.ColorCycle || p.Effect == PresetEffect.RainbowAdjustable)
                return RainbowBrush(r);
            if (p.Effect == PresetEffect.Fire)
            {
                // Fuego: de amarillo (arriba) a rojo (abajo).
                var fire = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), Color.Gold, Color.Red, 90f);
                fire.InterpolationColors = new ColorBlend
                {
                    Colors = new[] { Color.Gold, Color.OrangeRed, Color.Red },
                    Positions = new[] { 0f, 0.5f, 1f },
                };
                return fire;
            }
            if (Preset.UsesColorList(p.Effect))
                return ListBrush(p, r);
            if (p.Effect == PresetEffect.Off)
                return new SolidBrush(Gray);
            return new SolidBrush(ToColor(p.ColorValue));
        }

        /// <summary>Degradado lineal con la lista de colores del preset.</summary>
        private static LinearGradientBrush ListBrush(Preset p, RectangleF r)
        {
            uint[] colors = p.ColorValues();
            var br = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), Color.Black, Color.White, 0f);
            br.InterpolationColors = new ColorBlend
            {
                Colors = colors.Select(ToColor).ToArray(),
                Positions = colors.Select((c, i) => i / (float)(colors.Length - 1)).ToArray(),
            };
            return br;
        }

        /// <summary>Color a lo largo de un icono circular: lista del preset, arcoíris o color único.</summary>
        private static Color ArcColor(Preset p, float fraction)
        {
            if (Preset.UsesColorList(p.Effect))
            {
                uint[] colors = p.ColorValues();
                float x = Math.Max(0, Math.Min(0.999f, fraction)) * (colors.Length - 1);
                int i = (int)x;
                Color a = ToColor(colors[i]), b = ToColor(colors[Math.Min(i + 1, colors.Length - 1)]);
                float f = x - i;
                return Color.FromArgb((int)(a.R + (b.R - a.R) * f), (int)(a.G + (b.G - a.G) * f), (int)(a.B + (b.B - a.B) * f));
            }
            if (p.Effect == PresetEffect.Off)
                return Gray;
            if (!Preset.UsesColor(p.Effect))
                return Hue(fraction);
            return ToColor(p.ColorValue);
        }

        private static LinearGradientBrush RainbowBrush(RectangleF r)
        {
            var br = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), Color.Red, Color.Magenta, 0f);
            br.InterpolationColors = new ColorBlend
            {
                Colors = new[] { Color.Red, Color.Orange, Color.Gold, Color.LimeGreen, Color.DeepSkyBlue, Color.Blue, Color.Magenta },
                Positions = new[] { 0f, 0.17f, 0.33f, 0.5f, 0.67f, 0.83f, 1f },
            };
            return br;
        }

        private static void FillOutlined(Graphics g, GraphicsPath path, Brush brush)
        {
            using (path)
            {
                g.FillPath(brush, path);
                using (var pen = new Pen(Outline, 1) { LineJoin = LineJoin.Round })
                    g.DrawPath(pen, path);
            }
        }

        // ------------------------------------------------------------ iconos de efecto

        /// <summary>Onda senoidal.</summary>
        private static void DrawWave(Graphics g, RectangleF r, Brush brush)
        {
            var pts = new PointF[33];
            for (int i = 0; i < pts.Length; i++)
            {
                float t = i / (float)(pts.Length - 1);
                pts[i] = new PointF(r.X + t * r.Width, r.Y + r.Height / 2 - (float)Math.Sin(t * Math.PI * 2) * r.Height * 0.3f);
            }
            using (var pen = new Pen(brush, r.Width * 0.13f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, pts);
        }

        /// <summary>Dos flechas circulares con los colores del preset (o arcoíris) a lo largo del círculo.</summary>
        private static void DrawCycle(Graphics g, RectangleF r, Preset p)
        {
            float w = r.Width * 0.12f;
            var c = new RectangleF(r.X + w, r.Y + w, r.Width - 2 * w, r.Height - 2 * w);
            for (int arc = 0; arc < 2; arc++)
            {
                float start = arc * 180 + 20;
                const int steps = 24;
                for (int i = 0; i < steps; i++)
                {
                    float a = start + i * 140f / steps;
                    using (var pen = new Pen(ArcColor(p, (a - 20) / 320f), w))
                        g.DrawArc(pen, c, a, 140f / steps + 1);
                }
                double end = (start + 140) * Math.PI / 180;
                var tip = new PointF(c.X + c.Width / 2 + (float)Math.Cos(end) * c.Width / 2, c.Y + c.Height / 2 + (float)Math.Sin(end) * c.Height / 2);
                double tangent = end + Math.PI / 2;
                float s = w * 1.6f;
                var head = new[]
                {
                    new PointF(tip.X + (float)Math.Cos(tangent) * s, tip.Y + (float)Math.Sin(tangent) * s),
                    new PointF(tip.X + (float)Math.Cos(end) * s, tip.Y + (float)Math.Sin(end) * s),
                    new PointF(tip.X - (float)Math.Cos(end) * s, tip.Y - (float)Math.Sin(end) * s),
                };
                using (var br = new SolidBrush(ArcColor(p, (start + 120) / 320f)))
                    g.FillPolygon(br, head);
            }
        }

        /// <summary>Barra redondeada con el degradado del preset.</summary>
        private static void DrawGradientBar(Graphics g, RectangleF r, Brush brush)
        {
            var bar = new RectangleF(r.X, r.Y + r.Height * 0.3f, r.Width, r.Height * 0.4f);
            float rad = bar.Height;
            var path = new GraphicsPath();
            path.AddArc(bar.X, bar.Y, rad, rad, 90, 180);
            path.AddArc(bar.Right - rad, bar.Y, rad, rad, 270, 180);
            path.CloseFigure();
            FillOutlined(g, path, brush);
        }

        /// <summary>Doble cometa: dos bolas enfrentadas con estela, con los dos primeros colores.</summary>
        private static void DrawDoubleComet(Graphics g, RectangleF r, Preset p)
        {
            uint[] colors = p.ColorValues();
            float w = r.Width * 0.12f;
            var c = new RectangleF(r.X + w, r.Y + w, r.Width - 2 * w, r.Height - 2 * w);
            for (int k = 0; k < 2; k++)
            {
                Color color = ToColor(colors[k]);
                float start = k * 180f + 40;
                const int steps = 8;
                for (int i = 0; i < steps; i++)
                    using (var pen = new Pen(Color.FromArgb((int)(70 + 170 * i / (float)steps), color), w * (0.4f + 0.6f * i / steps)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawArc(pen, c, start + i * 13, 14);
                double a = (start + steps * 13) * Math.PI / 180;
                float hx = c.X + c.Width / 2 + (float)Math.Cos(a) * c.Width / 2, hy = c.Y + c.Height / 2 + (float)Math.Sin(a) * c.Height / 2;
                var ball = new GraphicsPath();
                ball.AddEllipse(hx - w, hy - w, 2 * w, 2 * w);
                using (var br = new SolidBrush(color))
                    FillOutlined(g, ball, br);
            }
        }

        /// <summary>Vela: cirio claro con una llama del color del preset.</summary>
        private static void DrawCandle(Graphics g, RectangleF r, Brush brush)
        {
            var body = new RectangleF(r.X + r.Width * 0.32f, r.Y + r.Height * 0.45f, r.Width * 0.36f, r.Height * 0.55f);
            using (var wax = new SolidBrush(Color.FromArgb(235, 228, 210)))
                g.FillRectangle(wax, body);
            using (var pen = new Pen(Outline))
                g.DrawRectangle(pen, body.X, body.Y, body.Width, body.Height);
            FillOutlined(g, Drop(new RectangleF(r.X + r.Width * 0.36f, r.Y, r.Width * 0.28f, r.Height * 0.42f)), brush);
        }

        /// <summary>Aurora: tres franjas onduladas con los colores del preset.</summary>
        private static void DrawAurora(Graphics g, RectangleF r, Preset p)
        {
            uint[] colors = p.ColorValues();
            for (int band = 0; band < 3; band++)
            {
                var pts = new PointF[17];
                for (int i = 0; i < pts.Length; i++)
                {
                    float t = i / (float)(pts.Length - 1);
                    pts[i] = new PointF(r.X + t * r.Width,
                        r.Y + r.Height * (0.25f + band * 0.25f) - (float)Math.Sin(t * Math.PI * 2 + band) * r.Height * 0.1f);
                }
                using (var pen = new Pen(Color.FromArgb(200, ToColor(colors[band % colors.Length])), r.Height * 0.14f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawCurve(pen, pts);
            }
        }

        /// <summary>Escáner: barra con un punto brillante y su estela.</summary>
        private static void DrawScanner(Graphics g, RectangleF r, Brush brush)
        {
            var bar = new RectangleF(r.X, r.Y + r.Height * 0.38f, r.Width, r.Height * 0.24f);
            using (var track = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                g.FillRectangle(track, bar);
            for (int i = 3; i >= 0; i--)
            {
                float size = bar.Height * (1.6f - i * 0.25f);
                float x = r.X + r.Width * (0.72f - i * 0.16f);
                var dot = new GraphicsPath();
                dot.AddEllipse(x - size / 2, r.Y + r.Height / 2 - size / 2, size, size);
                if (i == 0)
                    FillOutlined(g, dot, brush);
                else
                    using (dot)
                    using (var fade = new SolidBrush(Color.FromArgb(150 - i * 35, 255, 255, 255)))
                    {
                        g.FillPath(brush, dot);
                        g.FillPath(fade, dot);
                    }
            }
        }

        /// <summary>Persecución: fila de puntos alternos con los colores del preset.</summary>
        private static void DrawChase(Graphics g, RectangleF r, Preset p)
        {
            uint[] colors = p.ColorValues();
            float d = r.Width / 5;
            for (int i = 0; i < 5; i++)
            {
                var dot = new GraphicsPath();
                float size = i % 2 == 0 ? d * 0.9f : d * 0.45f;
                dot.AddEllipse(r.X + i * d + (d - size) / 2, r.Y + r.Height / 2 - size / 2, size, size);
                Color color = i % 2 == 0 ? ToColor(colors[(i / 2) % colors.Length]) : Color.FromArgb(200, 200, 200);
                using (var br = new SolidBrush(color))
                    FillOutlined(g, dot, br);
            }
        }

        /// <summary>Tormenta: nube gris con un rayo del color del preset.</summary>
        private static void DrawStorm(Graphics g, RectangleF r, Brush brush)
        {
            using (var cloud = new GraphicsPath { FillMode = FillMode.Winding })
            using (var gray = new SolidBrush(Color.FromArgb(150, 150, 160)))
            {
                cloud.AddEllipse(r.X + r.Width * 0.05f, r.Y + r.Height * 0.2f, r.Width * 0.45f, r.Height * 0.4f);
                cloud.AddEllipse(r.X + r.Width * 0.3f, r.Y + r.Height * 0.05f, r.Width * 0.5f, r.Height * 0.5f);
                cloud.AddEllipse(r.X + r.Width * 0.55f, r.Y + r.Height * 0.22f, r.Width * 0.4f, r.Height * 0.38f);
                g.FillPath(gray, cloud);
            }
            DrawBolt(g, new RectangleF(r.X + r.Width * 0.35f, r.Y + r.Height * 0.45f, r.Width * 0.35f, r.Height * 0.55f), brush);
        }

        /// <summary>Cometa: una bola con estela que se desvanece en arco.</summary>
        private static void DrawComet(Graphics g, RectangleF r, Preset p)
        {
            Color head = ArcColor(p, 0);
            float w = r.Width * 0.14f;
            var c = new RectangleF(r.X + w, r.Y + w, r.Width - 2 * w, r.Height - 2 * w);
            const int steps = 16;
            for (int i = 0; i < steps; i++)
            {
                int alpha = (int)(70 + 170 * i / (float)steps);
                using (var pen = new Pen(Color.FromArgb(alpha, head), w * (0.4f + 0.6f * i / steps)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawArc(pen, c, 120 + i * 13, 14);
            }
            double a = (120 + steps * 13) * Math.PI / 180;
            float hx = c.X + c.Width / 2 + (float)Math.Cos(a) * c.Width / 2, hy = c.Y + c.Height / 2 + (float)Math.Sin(a) * c.Height / 2;
            float hr = w * 1.1f;
            var ball = new GraphicsPath();
            ball.AddEllipse(hx - hr, hy - hr, 2 * hr, 2 * hr);
            using (var br = new SolidBrush(head))
                FillOutlined(g, ball, br);
        }

        /// <summary>Punto central con anillos que se desvanecen.</summary>
        private static void DrawRings(Graphics g, RectangleF r, Brush brush)
        {
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            float[] radii = { 0.47f, 0.33f };
            int[] alpha = { 90, 170 };
            float pw = r.Width * 0.07f;
            for (int i = 0; i < radii.Length; i++)
            {
                float rad = r.Width * radii[i];
                using (var pen = new Pen(brush, pw))
                using (var mask = new SolidBrush(Color.FromArgb(255 - alpha[i], 255, 255, 255)))
                {
                    g.DrawEllipse(pen, cx - rad, cy - rad, 2 * rad, 2 * rad);
                    using (var fade = new Pen(mask, pw))
                        g.DrawEllipse(fade, cx - rad, cy - rad, 2 * rad, 2 * rad);
                }
            }
            float inner = r.Width * 0.18f;
            var dot = new GraphicsPath();
            dot.AddEllipse(cx - inner, cy - inner, 2 * inner, 2 * inner);
            FillOutlined(g, dot, brush);
        }

        /// <summary>Bombilla.</summary>
        private static void DrawBulb(Graphics g, RectangleF r, Brush brush)
        {
            float bw = r.Width * 0.62f;
            var glass = new RectangleF(r.X + (r.Width - bw) / 2, r.Y + r.Height * 0.04f, bw, bw);
            var path = new GraphicsPath();
            path.AddArc(glass, 135, 270);
            float neckY = glass.Bottom + r.Height * 0.06f;
            path.AddLine(glass.Right - bw * 0.2f, neckY, glass.X + bw * 0.2f, neckY);
            path.CloseFigure();
            FillOutlined(g, path, brush);

            float baseW = bw * 0.5f;
            float bx = r.X + (r.Width - baseW) / 2;
            float by = glass.Bottom + r.Height * 0.1f;
            using (var pen = new Pen(Gray, r.Height * 0.07f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pen, bx, by, bx + baseW, by);
                g.DrawLine(pen, bx + baseW * 0.15f, by + r.Height * 0.11f, bx + baseW * 0.85f, by + r.Height * 0.11f);
            }
        }

        /// <summary>Rayo.</summary>
        private static void DrawBolt(Graphics g, RectangleF r, Brush brush)
        {
            PointF P(float x, float y) => new PointF(r.X + x * r.Width, r.Y + y * r.Height);
            var path = new GraphicsPath();
            path.AddPolygon(new[] { P(0.62f, 0f), P(0.1f, 0.58f), P(0.46f, 0.58f), P(0.34f, 1f), P(0.9f, 0.4f), P(0.54f, 0.4f) });
            FillOutlined(g, path, brush);
        }

        /// <summary>Símbolo de apagado.</summary>
        private static void DrawPower(Graphics g, RectangleF r)
        {
            float w = r.Width * 0.11f;
            var c = new RectangleF(r.X + r.Width * 0.12f, r.Y + r.Height * 0.16f, r.Width * 0.76f, r.Height * 0.76f);
            using (var pen = new Pen(Gray, w) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawArc(pen, c, -55, 290);
                g.DrawLine(pen, r.X + r.Width / 2, r.Y + r.Height * 0.06f, r.X + r.Width / 2, r.Y + r.Height * 0.5f);
            }
        }

        // ------------------------------------------------------------ iconos decorativos

        private static GraphicsPath Heart(RectangleF r)
        {
            PointF P(float x, float y) => new PointF(r.X + x * r.Width, r.Y + y * r.Height);
            var path = new GraphicsPath();
            path.AddBezier(P(0.5f, 0.95f), P(0.15f, 0.7f), P(-0.08f, 0.4f), P(0.1f, 0.2f));
            path.AddBezier(P(0.1f, 0.2f), P(0.25f, 0.03f), P(0.45f, 0.1f), P(0.5f, 0.28f));
            path.AddBezier(P(0.5f, 0.28f), P(0.55f, 0.1f), P(0.75f, 0.03f), P(0.9f, 0.2f));
            path.AddBezier(P(0.9f, 0.2f), P(1.08f, 0.4f), P(0.85f, 0.7f), P(0.5f, 0.95f));
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath Star(RectangleF r, int points, float innerRatio)
        {
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2 + (points == 5 ? r.Height * 0.04f : 0);
            float outer = r.Width / 2, inner = outer * innerRatio;
            var pts = new PointF[points * 2];
            for (int i = 0; i < pts.Length; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / points;
                float rad = i % 2 == 0 ? outer : inner;
                pts[i] = new PointF(cx + (float)Math.Cos(a) * rad, cy + (float)Math.Sin(a) * rad);
            }
            var path = new GraphicsPath();
            path.AddPolygon(pts);
            return path;
        }

        private static GraphicsPath Flame(RectangleF r)
        {
            PointF P(float x, float y) => new PointF(r.X + x * r.Width, r.Y + y * r.Height);
            var path = new GraphicsPath();
            path.AddBezier(P(0.5f, 0.0f), P(0.62f, 0.25f), P(0.92f, 0.4f), P(0.85f, 0.7f));
            path.AddBezier(P(0.85f, 0.7f), P(0.78f, 1.0f), P(0.22f, 1.0f), P(0.15f, 0.7f));
            path.AddBezier(P(0.15f, 0.7f), P(0.1f, 0.5f), P(0.25f, 0.4f), P(0.3f, 0.3f));
            path.AddBezier(P(0.3f, 0.3f), P(0.36f, 0.45f), P(0.42f, 0.45f), P(0.45f, 0.4f));
            path.AddBezier(P(0.45f, 0.4f), P(0.5f, 0.3f), P(0.45f, 0.15f), P(0.5f, 0.0f));
            path.CloseFigure();
            return path;
        }

        private static GraphicsPath Drop(RectangleF r)
        {
            PointF P(float x, float y) => new PointF(r.X + x * r.Width, r.Y + y * r.Height);
            var path = new GraphicsPath();
            path.AddBezier(P(0.5f, 0.0f), P(0.6f, 0.25f), P(0.85f, 0.45f), P(0.85f, 0.65f));
            path.AddBezier(P(0.85f, 0.65f), P(0.85f, 0.88f), P(0.68f, 1.0f), P(0.5f, 1.0f));
            path.AddBezier(P(0.5f, 1.0f), P(0.32f, 1.0f), P(0.15f, 0.88f), P(0.15f, 0.65f));
            path.AddBezier(P(0.15f, 0.65f), P(0.15f, 0.45f), P(0.4f, 0.25f), P(0.5f, 0.0f));
            path.CloseFigure();
            return path;
        }

        private static void DrawSnowflake(Graphics g, RectangleF r, Brush brush)
        {
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, len = r.Width * 0.46f;
            using (var pen = new Pen(brush, r.Width * 0.08f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                for (int i = 0; i < 6; i++)
                {
                    double a = i * Math.PI / 3 - Math.PI / 2;
                    float ex = cx + (float)Math.Cos(a) * len, ey = cy + (float)Math.Sin(a) * len;
                    g.DrawLine(pen, cx, cy, ex, ey);
                    float bx = cx + (float)Math.Cos(a) * len * 0.6f, by = cy + (float)Math.Sin(a) * len * 0.6f;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        double b = a + s * Math.PI / 4;
                        g.DrawLine(pen, bx, by, bx + (float)Math.Cos(b) * len * 0.3f, by + (float)Math.Sin(b) * len * 0.3f);
                    }
                }
            }
        }

        private static void DrawLeaf(Graphics g, RectangleF r, Brush brush)
        {
            PointF P(float x, float y) => new PointF(r.X + x * r.Width, r.Y + y * r.Height);
            var path = new GraphicsPath();
            path.AddBezier(P(0.12f, 0.88f), P(0.0f, 0.4f), P(0.4f, 0.05f), P(0.92f, 0.08f));
            path.AddBezier(P(0.92f, 0.08f), P(0.95f, 0.6f), P(0.6f, 0.98f), P(0.12f, 0.88f));
            path.CloseFigure();
            FillOutlined(g, path, brush);
            using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), r.Width * 0.05f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(pen, P(0.12f, 0.88f), P(0.72f, 0.28f));
        }

        private static void DrawSun(Graphics g, RectangleF r, Brush brush)
        {
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
            using (var pen = new Pen(brush, r.Width * 0.08f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    g.DrawLine(pen, cx + (float)Math.Cos(a) * r.Width * 0.34f, cy + (float)Math.Sin(a) * r.Width * 0.34f,
                        cx + (float)Math.Cos(a) * r.Width * 0.48f, cy + (float)Math.Sin(a) * r.Width * 0.48f);
                }
            float rad = r.Width * 0.24f;
            var path = new GraphicsPath();
            path.AddEllipse(cx - rad, cy - rad, 2 * rad, 2 * rad);
            FillOutlined(g, path, brush);
        }

        private static void DrawMoon(Graphics g, RectangleF r, Brush brush)
        {
            var outer = Inset(r, 0.08f, 0.08f);
            var cut = new RectangleF(outer.X + outer.Width * 0.35f, outer.Y - outer.Height * 0.12f, outer.Width, outer.Height);
            using (var region = new Region(EllipsePath(outer)))
            using (var cutPath = EllipsePath(cut))
            {
                region.Exclude(cutPath);
                g.FillRegion(brush, region);
            }
        }

        private static GraphicsPath EllipsePath(RectangleF r)
        {
            var p = new GraphicsPath();
            p.AddEllipse(r);
            return p;
        }

        private static void DrawMusic(Graphics g, RectangleF r, Brush brush)
        {
            PointF P(float x, float y) => new PointF(r.X + x * r.Width, r.Y + y * r.Height);
            var heads = new GraphicsPath();
            heads.AddEllipse(new RectangleF(P(0.08f, 0.7f), new SizeF(r.Width * 0.32f, r.Height * 0.24f)));
            heads.AddEllipse(new RectangleF(P(0.58f, 0.6f), new SizeF(r.Width * 0.32f, r.Height * 0.24f)));
            FillOutlined(g, heads, brush);
            using (var pen = new Pen(brush, r.Width * 0.09f) { LineJoin = LineJoin.Round })
                g.DrawLines(pen, new[] { P(0.36f, 0.8f), P(0.36f, 0.12f), P(0.86f, 0.03f), P(0.86f, 0.7f) });
        }

        private static void DrawGamepad(Graphics g, RectangleF r, Brush brush)
        {
            var body = new RectangleF(r.X, r.Y + r.Height * 0.22f, r.Width, r.Height * 0.56f);
            float rad = body.Height * 0.5f;
            var path = new GraphicsPath();
            path.AddArc(body.X, body.Y, rad * 2, rad * 2, 90, 180);
            path.AddArc(body.Right - rad * 2, body.Y, rad * 2, rad * 2, 270, 180);
            path.CloseFigure();
            FillOutlined(g, path, brush);

            using (var white = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
            {
                float u = r.Width * 0.07f, cx = body.X + body.Width * 0.27f, cy = body.Y + body.Height / 2;
                g.FillRectangle(white, cx - u * 1.5f, cy - u / 2, u * 3, u);
                g.FillRectangle(white, cx - u / 2, cy - u * 1.5f, u, u * 3);
                float bx = body.X + body.Width * 0.72f;
                g.FillEllipse(white, bx - u * 1.6f, cy - u / 2, u * 1.2f, u * 1.2f);
                g.FillEllipse(white, bx + u * 0.4f, cy - u / 2, u * 1.2f, u * 1.2f);
            }
        }
    }
}
