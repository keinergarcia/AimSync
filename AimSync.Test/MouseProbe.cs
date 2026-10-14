using System.Runtime.InteropServices;
using AimSync.Core.Input;

namespace AimSync.Test;

/// <summary>
/// Captura deltas reales del mouse durante N segundos (igual que el InputManager) y estima
/// un MouseMaxDelta con datos, no a ojo.
/// </summary>
internal static class MouseProbe
{
    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint uMilliseconds);

    public static void Run(double seconds = 6)
    {
        Console.WriteLine("=== AimSync Mouse Delta Probe ===");
        Console.WriteLine($"Mueva el mouse de forma NATURAL (una orbita de camara, ajustes finos,");
        Console.WriteLine($"flick rápido) durante ~{seconds:0}s. Capturando a 250 Hz reales...");
        Console.WriteLine();

        timeBeginPeriod(1);
        using (var capture = new MouseCapture())
        {
            var wall = System.Diagnostics.Stopwatch.StartNew();
            var tick = System.Diagnostics.Stopwatch.StartNew();
            long targetTicks = TimeSpan.FromMilliseconds(4).Ticks;
            long sleepThresholdTicks = TimeSpan.TicksPerMillisecond * 2;

            var dxList = new List<int>(4096);
            var dyList = new List<int>(4096);
            long ticks = 0;

            while (wall.ElapsedMilliseconds < (long)(seconds * 1000))
            {
                tick.Restart();
                capture.TryTakeDeltas(out int dx, out int dy);
                if (dx != 0) dxList.Add(Math.Abs(dx));
                if (dy != 0) dyList.Add(Math.Abs(dy));
                ticks++;

                long remaining = targetTicks - tick.ElapsedTicks;
                if (remaining > sleepThresholdTicks)
                    Thread.Sleep(1);
                while (tick.ElapsedTicks < targetTicks)
                    Thread.SpinWait(1);
            }

            long realMs = wall.ElapsedMilliseconds;
            double hz = ticks / (realMs / 1000d);

        PrintAxis("X", dxList);
        PrintAxis("Y", dyList);

        int dxP90 = Percentile(dxList, 90);
        int dyP90 = Percentile(dyList, 90);
        int dxP99 = Percentile(dxList, 99);
        int dyP99 = Percentile(dyList, 99);
        int recX = RoundUpTo(dxP90, 10);
        int recY = RoundUpTo(dyP90, 10);
        int rec = Math.Max(recX, recY);

        Console.WriteLine();
        Console.WriteLine("Estadisticas del tick (250 Hz):");
        Console.WriteLine($"  ticks totales = {ticks}  (~{hz:0} Hz reales)");
        Console.WriteLine($"  |dX| P90={dxP90}  P99={dxP99}  max={InspectMax(dxList):0}");
        Console.WriteLine($"  |dY| P90={dyP90}  P99={dyP99}  max={InspectMax(dyList):0}");
        Console.WriteLine();
        Console.WriteLine("Interpretacion: el stick llega a FULL DEFLEXION cuando (Δ × Sens) == MouseMaxDelta.");
        Console.WriteLine("Con deltas tipicos de P90, Sens=1x solo daria deflexion ~0.5-0.9;");
        Console.WriteLine("por eso los presets 2x/4x/8x existen y el MaxDelta es calibrable.");
        Console.WriteLine();
        Console.WriteLine($"RECOMENDACION MouseMaxDelta (basado en P90 x2 redondeado): {rec}");
            Console.WriteLine($"   (X recomendado={recX}, Y recomendado={recY})");
        }

        timeEndPeriod(1);
    }

    private static int InspectMax(List<int> values) => values.Count == 0 ? 0 : values.Max();

    private static void PrintAxis(string axis, List<int> values)
    {
        if (values.Count == 0)
        {
            Console.WriteLine($"Eje {axis}: sin movimiento capturado.");
            return;
        }

        var sorted = values.OrderBy(v => v).ToArray();
        int p50 = sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * 0.50))];
        int p90 = sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * 0.90))];
        int p99 = sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * 0.99))];
        Console.WriteLine($"Eje {axis}: n={sorted.Length}  p50={p50}  p90={p90}  p99={p99}  max={sorted[^1]}");
    }

    private static int Percentile(List<int> values, double p)
    {
        if (values.Count == 0)
            return 0;
        var sorted = values.OrderBy(v => v).ToArray();
        return sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * p / 100d))];
    }

    private static int RoundUpTo(int value, int step) => ((value + step - 1) / step) * step;
}