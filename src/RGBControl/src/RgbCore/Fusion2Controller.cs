using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RgbCore
{
    /// <summary>Tipos de efecto que el chip ITE reproduce por sí solo.</summary>
    public enum EffectType : byte
    {
        Off = 0,
        Static = 1,
        Pulse = 2,
        Flash = 3,
        ColorCycle = 4,
        Wave = 6,
        Random = 8,
        Wave1 = 9,
        Wave2 = 10,
        Wave3 = 11,
        Wave4 = 12,
    }

    /// <summary>Parámetros crudos de un efecto tal como los entiende el chip.</summary>
    public sealed class EffectParams
    {
        public EffectType Type = EffectType.Static;
        public byte MaxBrightness = 255;
        public byte MinBrightness = 0;
        public uint Color0;            // 0xRRGGBB
        public uint Color1;            // 0xRRGGBB
        public ushort Period0, Period1, Period2, Period3;
        public byte Param0, Param1, Param2, Param3;
    }

    /// <summary>
    /// Protocolo del controlador Gigabyte RGB Fusion 2 (ITE IT5711, 048D:5711).
    /// Basado en el trabajo de ingeniería inversa del proyecto OpenRGB.
    /// </summary>
    public sealed class Fusion2Controller : IDisposable
    {
        public const ushort Vid = 0x048D;
        public const ushort Pid = 0x5711;
        private const byte ReportId = 0xCC;

        // Índices de zona en la X870 GAMING X WIFI7 (según OpenRGB).
        public const int ZoneChipsetAccent = 2;  // LED3
        public const int ZoneLedC = 4;           // LED5, conector RGB 12 V
        public const int ZoneArgb1 = 5;          // D_LED1
        public const int ZoneArgb2 = 6;          // D_LED2
        public const int ZoneArgb3 = 7;          // D_LED3
        public static readonly int[] ArgbZones = { ZoneArgb1, ZoneArgb2, ZoneArgb3 };

        private const uint AllZonesMask = 0x07FF;

        /// <summary>Cabeceras del modo LED a LED de D_LED1, D_LED2 y D_LED3 (mismo orden que LedCounts).</summary>
        public static readonly byte[] DirectHeaders = { 0x58, 0x59, 0x62 };
        /// <summary>Orden de bytes de color por defecto (el que tiene este equipo).</summary>
        public const uint DefaultByteOrder = 0x00010002;

        private readonly HidDevice dev;           // null en modo grabación
        private readonly Action<byte[]> sendPacket;
        private uint pendingMask;

        public string FirmwareName { get; private set; }
        public bool SupportsPersistence { get; private set; }

        private Fusion2Controller(HidDevice dev, Action<byte[]> sendPacket)
        {
            this.dev = dev;
            this.sendPacket = sendPacket;
        }

        /// <summary>
        /// Controlador sin dispositivo que, en vez de enviar, añade cada paquete de 64 bytes a <paramref name="sink"/>.
        /// Sirve para precalcular los paquetes que reenviará el ayudante.
        /// </summary>
        public static Fusion2Controller CreateRecorder(List<byte[]> sink) =>
            new Fusion2Controller(null, p => sink.Add((byte[])p.Clone()));

        /// <summary>Busca y abre el controlador. Devuelve null si no está.</summary>
        public static Fusion2Controller Open()
        {
            var all = HidDevice.Enumerate(Vid, Pid);
            var ctrl = all.FirstOrDefault(d => d.UsagePage == 0xFF89 && d.Usage == 0xCC);
            foreach (var d in all.Where(d => d != ctrl))
                d.Dispose();
            if (ctrl == null)
                return null;

            var c = new Fusion2Controller(ctrl, ctrl.SetFeature);
            c.ReadInfo();
            return c;
        }

        private void ReadInfo()
        {
            byte[] r = Query(0x60);
            FirmwareName = System.Text.Encoding.ASCII.GetString(r, 12, 28).TrimEnd('\0');
            SupportsPersistence = (r[11] & 0x01) != 0;
        }

        /// <summary>Activa o desactiva el modo LampArray (Iluminación dinámica de Windows).</summary>
        public void SetLampArrayMode(bool enable) => Send(0x48, (byte)(enable ? 1 : 0));

        /// <summary>Modo "beat" (sincronía con audio por hardware).</summary>
        public void SetBeatMode(bool enable) => Send(0x31, (byte)(enable ? 1 : 0));

        /// <summary>Pone todas las zonas a cero y aplica.</summary>
        public void Reset()
        {
            for (byte reg = 0x20; reg <= 0x27; reg++)
                Send(reg, 0, 0);
            for (byte reg = 0x90; reg <= 0x92; reg++)
                Send(reg, 0, 0);
            Send(0x28, 0xFF, 0x07);
        }

        /// <summary>
        /// Habilita (true) o deshabilita (false) los efectos de serie en los conectores ARGB.
        /// Deshabilitados, los conectores solo muestran colores enviados LED a LED.
        /// </summary>
        public void SetArgbBuiltinEffects(bool enable)
        {
            const byte allHeaders = 0x01 | 0x02 | 0x08 | 0x10 | 0x20 | 0x40;
            Send(0x32, enable ? (byte)0 : allHeaders);
            Thread.Sleep(50);
        }

        /// <summary>Número de LEDs por conector ARGB, en las divisiones que entiende el chip (0=32, 1=64, 2=256...).</summary>
        public void SetLedCountDivisions(byte d1, byte d2, byte d3, byte d4, byte d5, byte d6)
        {
            var b = new byte[64];
            b[0] = ReportId;
            b[1] = 0x34;
            b[2] = (byte)((d2 << 4) | d1);
            b[3] = (byte)((d4 << 4) | d3);
            b[4] = (byte)((d6 << 4) | d5);
            sendPacket(b);
        }

        /// <summary>Programa un efecto en una zona (índice 0-10) o en todas (-1). Queda pendiente hasta Apply().</summary>
        public void SetEffect(int zone, EffectParams p)
        {
            var b = new byte[64];
            b[0] = ReportId;
            uint mask;
            if (zone < 0)
            {
                mask = AllZonesMask;
                b[1] = 0x20;
            }
            else if (zone < 8)
            {
                mask = 1u << zone;
                b[1] = (byte)(0x20 + zone);
            }
            else
            {
                mask = 1u << zone;
                b[1] = (byte)(0x90 + zone - 8);
            }
            pendingMask |= mask;

            WriteU32(b, 2, mask);
            b[11] = (byte)p.Type;
            b[12] = p.MaxBrightness;
            b[13] = p.MinBrightness;
            WriteU32(b, 14, p.Color0 & 0xFFFFFF);
            WriteU32(b, 18, p.Color1 & 0xFFFFFF);
            WriteU16(b, 22, p.Period0);
            WriteU16(b, 24, p.Period1);
            WriteU16(b, 26, p.Period2);
            WriteU16(b, 28, p.Period3);
            b[30] = p.Param0;
            b[31] = p.Param1;
            b[32] = p.Param2;
            b[33] = p.Param3;
            sendPacket(b);
        }

        /// <summary>Aplica los efectos programados desde el último Apply().</summary>
        public void Apply()
        {
            var b = new byte[64];
            b[0] = ReportId;
            b[1] = 0x28;
            WriteU32(b, 2, pendingMask);
            sendPacket(b);
            pendingMask = 0;
        }

        /// <summary>
        /// Modo directo: envía colores LED a LED a un conector ARGB (header 0x58, 0x59, 0x62...).
        /// byteOrder es el valor de calibración del conector (posición de B, G y R en cada trío).
        /// </summary>
        public void SetStripColors(byte header, uint[] colors, uint byteOrder)
        {
            int boB = (int)(byteOrder & 0xFF), boG = (int)((byteOrder >> 8) & 0xFF), boR = (int)((byteOrder >> 16) & 0xFF);
            int sent = 0, k = 0;
            while (k < colors.Length)
            {
                int n = Math.Min(19, colors.Length - k);
                var b = new byte[64];
                b[0] = ReportId;
                b[1] = header;
                WriteU16(b, 2, (ushort)sent);
                b[4] = (byte)(n * 3);
                for (int i = 0; i < n; i++, k++)
                {
                    int off = 5 + i * 3;
                    b[off + boR] = (byte)(colors[k] >> 16);
                    b[off + boG] = (byte)(colors[k] >> 8);
                    b[off + boB] = (byte)colors[k];
                }
                sent += n * 3;
                sendPacket(b);
            }
        }

        /// <summary>Activa que el chip reproduzca al arrancar lo guardado y graba el estado actual en su flash.</summary>
        public void SaveToFlash()
        {
            Send(0x47, 1);
            Thread.Sleep(20);
            Send(0x5E, 0);
            Thread.Sleep(20);
        }

        /// <summary>Envía un comando corto 0xCC a b c.</summary>
        public void Send(byte a, byte b = 0, byte c = 0)
        {
            var buf = new byte[64];
            buf[0] = ReportId;
            buf[1] = a;
            buf[2] = b;
            buf[3] = c;
            sendPacket(buf);
        }

        public byte[] Query(byte cmd)
        {
            if (dev == null)
                throw new InvalidOperationException("Un controlador en modo grabación no puede leer del chip.");
            Send(cmd);
            return dev.GetFeature(ReportId);
        }

        /// <summary>División de tamaño de tira del chip para un número de LEDs (0 = 32, 1 = 64, 2 = 256).</summary>
        public static byte LedCountDivision(int count) => (byte)(count <= 32 ? 0 : count <= 64 ? 1 : 2);

        /// <summary>
        /// Prepara el modo LED a LED: quita el control de Windows, el modo beat y los efectos de serie,
        /// y fija el tamaño de tira de D_LED1, D_LED2 y D_LED3 según <paramref name="ledCounts"/>.
        /// </summary>
        public void EnterDirectMode(int[] ledCounts)
        {
            SetLampArrayMode(false);
            // Sin reiniciar los registros de efectos, tras encender o reanudar el chip sigue
            // reproduciendo su efecto guardado e ignora los colores LED a LED.
            Reset();
            SetBeatMode(false);
            SetArgbBuiltinEffects(false);
            SetLedCountDivisions(LedCountDivision(ledCounts[0]), LedCountDivision(ledCounts[1]), LedCountDivision(ledCounts[2]), 0, 0, 0);
        }

        /// <summary>Orden de bytes de color de D_LED1, D_LED2 y D_LED3 leído de la calibración del chip.</summary>
        public uint[] ReadByteOrders()
        {
            byte[] info = Query(0x60);
            byte[] cal = Query(0x61);
            return new[]
            {
                ValidByteOrder(BitConverter.ToUInt32(info, 44)),
                ValidByteOrder(BitConverter.ToUInt32(info, 48)),
                ValidByteOrder(BitConverter.ToUInt32(cal, 4)),
            };
        }

        /// <summary>Un orden válido coloca R, G y B en posiciones 0-2 distintas; si no, se usa el por defecto.</summary>
        private static uint ValidByteOrder(uint v)
        {
            uint b = v & 0xFF, g = (v >> 8) & 0xFF, r = (v >> 16) & 0xFF;
            bool ok = (v >> 24) == 0 && r < 3 && g < 3 && b < 3 && r != g && r != b && g != b;
            return ok ? v : DefaultByteOrder;
        }

        private static void WriteU16(byte[] b, int off, ushort v)
        {
            b[off] = (byte)v;
            b[off + 1] = (byte)(v >> 8);
        }

        private static void WriteU32(byte[] b, int off, uint v)
        {
            b[off] = (byte)v;
            b[off + 1] = (byte)(v >> 8);
            b[off + 2] = (byte)(v >> 16);
            b[off + 3] = (byte)(v >> 24);
        }

        public void Dispose() => dev?.Dispose();
    }
}
