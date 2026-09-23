using System.Numerics;
using System.Text.Json;

namespace IndicadoresMovimiento.Core;

/// <summary>Una lectura enviada por el móvil, ya en la convención de <see cref="MotionSample"/>.</summary>
public readonly record struct PhoneSample(double Timestamp, Vector3 SpecificForce, Vector3? LinearAcceleration);

/// <summary>Un paquete de lecturas enviado por la página web del móvil.</summary>
/// <param name="ScreenAngle">Giro de la pantalla del móvil (0, 90, 180 o 270 grados).</param>
public sealed record PhoneBatch(int ScreenAngle, IReadOnlyList<PhoneSample> Samples)
{
    /// <summary>Convierte una lectura del paquete en una muestra para <see cref="MotionProcessor"/>.</summary>
    public MotionSample ToMotionSample(in PhoneSample sample) =>
        new(sample.Timestamp, sample.SpecificForce, sample.LinearAcceleration, null,
            PhoneBatchParser.FlatRightAxisForScreenAngle(ScreenAngle));
}

/// <summary>
/// Lee el JSON que envía la página del móvil:
/// <c>{"ang":0,"s":[[t,fx,fy,fz],[t,fx,fy,fz,ax,ay,az],...]}</c>
/// (tiempo en segundos, fuerza específica y, opcionalmente, aceleración lineal en m/s²).
/// </summary>
public static class PhoneBatchParser
{
    public const int MaxSamples = 400;
    private const float MaxAbsValue = 200f; // m/s²; valores mayores son errores del sensor

    public static bool TryParse(ReadOnlySpan<byte> json, out PhoneBatch? batch)
    {
        batch = null;
        try
        {
            var reader = new Utf8JsonReader(json, new JsonReaderOptions { MaxDepth = 8 });
            using var document = JsonDocument.ParseValue(ref reader);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;

            var angle = 0;
            if (root.TryGetProperty("ang", out var angleElement) && angleElement.ValueKind == JsonValueKind.Number
                && angleElement.TryGetDouble(out var angleValue) && double.IsFinite(angleValue))
            {
                angle = NormalizeAngle((int)Math.Round(angleValue));
            }

            if (!root.TryGetProperty("s", out var samplesElement) || samplesElement.ValueKind != JsonValueKind.Array)
                return false;
            if (samplesElement.GetArrayLength() > MaxSamples) return false;

            var samples = new List<PhoneSample>(samplesElement.GetArrayLength());
            foreach (var item in samplesElement.EnumerateArray())
            {
                if (TryReadSample(item, out var sample)) samples.Add(sample);
            }

            batch = new PhoneBatch(angle, samples);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Eje del móvil que apunta a la derecha de la imagen según el giro de la pantalla.</summary>
    public static Vector3 FlatRightAxisForScreenAngle(int angle) => NormalizeAngle(angle) switch
    {
        // 90: móvil girado a la izquierda (parte de arriba hacia la izquierda).
        90 => -Vector3.UnitY,
        180 => -Vector3.UnitX,
        270 => Vector3.UnitY,
        _ => Vector3.UnitX,
    };

    private static int NormalizeAngle(int angle)
    {
        var a = ((angle % 360) + 360) % 360;
        return a switch
        {
            < 45 => 0,
            < 135 => 90,
            < 225 => 180,
            < 315 => 270,
            _ => 0,
        };
    }

    private static bool TryReadSample(JsonElement item, out PhoneSample sample)
    {
        sample = default;
        if (item.ValueKind != JsonValueKind.Array) return false;
        var count = item.GetArrayLength();
        if (count != 4 && count != 7) return false;

        Span<double> values = stackalloc double[7];
        var i = 0;
        foreach (var element in item.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Number || !element.TryGetDouble(out var v) || !double.IsFinite(v))
                return false;
            values[i++] = v;
        }

        var force = new Vector3((float)values[1], (float)values[2], (float)values[3]);
        if (!IsReasonable(force)) return false;

        Vector3? linear = null;
        if (count == 7)
        {
            var l = new Vector3((float)values[4], (float)values[5], (float)values[6]);
            if (IsReasonable(l)) linear = l;
        }

        sample = new PhoneSample(values[0], force, linear);
        return true;
    }

    private static bool IsReasonable(Vector3 v) =>
        Math.Abs(v.X) < MaxAbsValue && Math.Abs(v.Y) < MaxAbsValue && Math.Abs(v.Z) < MaxAbsValue;
}
