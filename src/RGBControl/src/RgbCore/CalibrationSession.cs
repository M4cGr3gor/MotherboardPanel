using System;

namespace RgbCore
{
    /// <summary>
    /// Estado de "Calibrar LEDs…": cuántos LEDs tiene cada conector ARGB (D_LED1, D_LED2, D_LED3).
    /// Se recorren los conectores de uno en uno; en el actual se encienden n LEDs, el último en rojo.
    /// </summary>
    public sealed class CalibrationSession
    {
        public const int MaxCount = 120;
        private const int StartCount = 8;
        private const uint Green = 0x00FF00, Red = 0xFF0000;

        private readonly int[] counts = new int[3];   // valor elegido (aunque el conector esté vacío)
        private readonly bool[] empty = new bool[3];

        public CalibrationSession(int[] savedCounts)
        {
            for (int i = 0; i < 3; i++)
            {
                int saved = savedCounts != null && i < savedCounts.Length ? savedCounts[i] : 0;
                counts[i] = saved > 0 ? Math.Min(saved, MaxCount) : StartCount;
            }
        }

        /// <summary>Conector actual: 0 = D_LED1, 1 = D_LED2, 2 = D_LED3.</summary>
        public int Connector { get; private set; }

        /// <summary>LEDs del conector actual (0 si está marcado como vacío).</summary>
        public int Count => empty[Connector] ? 0 : counts[Connector];

        public bool IsEmpty => empty[Connector];

        public bool IsLast => Connector == 2;

        public void Increase() => counts[Connector] = Math.Min(MaxCount, counts[Connector] + 1);

        public void Decrease() => counts[Connector] = Math.Max(1, counts[Connector] - 1);

        /// <summary>Marca o desmarca "No hay nada conectado"; al desmarcar se recupera el último valor.</summary>
        public void SetEmpty(bool isEmpty) => empty[Connector] = isEmpty;

        public bool Next()
        {
            if (IsLast)
                return false;
            Connector++;
            return true;
        }

        public bool Previous()
        {
            if (Connector == 0)
                return false;
            Connector--;
            return true;
        }

        /// <summary>Colores 0xRRGGBB para un conector: en el actual, n LEDs verdes con el último rojo; el resto apagado.</summary>
        public uint[] Frame(int connector, int length)
        {
            var frame = new uint[length];
            if (connector != Connector)
                return frame;
            int n = Math.Min(Count, length);
            for (int i = 0; i < n; i++)
                frame[i] = i == n - 1 ? Red : Green;
            return frame;
        }

        /// <summary>LEDs de cada conector (0 en los marcados como vacíos).</summary>
        public int[] Result()
        {
            var result = new int[3];
            for (int i = 0; i < 3; i++)
                result[i] = empty[i] ? 0 : counts[i];
            return result;
        }
    }
}
