using System.Numerics;

namespace IndicadoresMovimiento.Core;

/// <summary>Utilidades numéricas compartidas por el procesado del movimiento.</summary>
internal static class Filters
{
    /// <summary>
    /// Factor de mezcla de un filtro paso bajo de primer orden: para un paso <paramref name="dt"/>
    /// y una constante de tiempo <paramref name="tau"/>, el filtro avanza este tanto por uno hacia la entrada.
    /// </summary>
    public static float Alpha(double dt, double tau) =>
        tau <= 0 ? 1f : (float)(1 - Math.Exp(-dt / tau));

    public static double SmoothStep(double edge0, double edge1, double x)
    {
        if (edge1 <= edge0) return x >= edge1 ? 1 : 0;
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    public static bool IsFinite(Vector3 v) =>
        float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    /// <summary>Ángulo en grados entre dos vectores (0 si alguno es nulo).</summary>
    public static double AngleDegrees(Vector3 a, Vector3 b)
    {
        var la = a.Length();
        var lb = b.Length();
        if (la < 1e-6f || lb < 1e-6f) return 0;
        var cos = Math.Clamp(Vector3.Dot(a, b) / (la * lb), -1f, 1f);
        return Math.Acos(cos) * 180 / Math.PI;
    }
}
