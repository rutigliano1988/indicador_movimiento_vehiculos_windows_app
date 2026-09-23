using System.Numerics;
using IndicadoresMovimiento.Core;

namespace IndicadoresMovimiento.Tests;

/// <summary>
/// Orientación de un dispositivo dentro del coche (ejes del coche: X derecha, Y delante, Z arriba)
/// para generar lecturas de acelerómetro sintéticas.
/// </summary>
internal sealed record DeviceFrame(string Name, Vector3 X, Vector3 Y)
{
    public Vector3 Z => Vector3.Cross(X, Y);

    /// <summary>Portátil sobre las piernas con la pantalla inclinada hacia atrás.</summary>
    public static DeviceFrame Laptop(double tiltDegrees = 20)
    {
        var t = tiltDegrees * Math.PI / 180;
        return new("portátil", Vector3.UnitX, new Vector3(0, (float)Math.Sin(t), (float)Math.Cos(t)));
    }

    /// <summary>Móvil tumbado boca arriba con la parte de arriba hacia delante.</summary>
    public static DeviceFrame PhoneFlat => new("móvil tumbado", Vector3.UnitX, Vector3.UnitY);

    /// <summary>Móvil tumbado, girado 90° a la izquierda (parte de arriba hacia la izquierda).</summary>
    public static DeviceFrame PhoneFlatRotatedLeft => new("móvil tumbado girado", Vector3.UnitY, -Vector3.UnitX);

    /// <summary>Móvil de pie en un soporte, en horizontal con la parte de arriba a la izquierda.</summary>
    public static DeviceFrame PhoneUprightRotatedLeft => new("móvil de pie girado a la izquierda", Vector3.UnitZ, -Vector3.UnitX);

    /// <summary>Móvil de pie en un soporte, en horizontal con la parte de arriba a la derecha.</summary>
    public static DeviceFrame PhoneUprightRotatedRight => new("móvil de pie girado a la derecha", -Vector3.UnitZ, Vector3.UnitX);

    /// <summary>Móvil de pie en vertical mirando a quien lo usa.</summary>
    public static DeviceFrame PhoneUpright => new("móvil de pie", Vector3.UnitX, Vector3.UnitZ);

    public Vector3 ToDevice(Vector3 world) => new(Vector3.Dot(world, X), Vector3.Dot(world, Y), Vector3.Dot(world, Z));

    /// <summary>Lectura del acelerómetro para una aceleración del coche (lateral, longitudinal) en m/s².</summary>
    public MotionSample Sample(double t, double lateral, double longitudinal, bool fused = false, Vector3? flatRight = null)
    {
        var acceleration = new Vector3((float)lateral, (float)longitudinal, 0);
        var force = acceleration + new Vector3(0, 0, MotionSample.StandardGravity);
        return new MotionSample(t, ToDevice(force), fused ? ToDevice(acceleration) : null, null, flatRight);
    }
}

/// <summary>Reloj controlado por la prueba.</summary>
internal sealed class FakeClock
{
    public double Now { get; set; }
    public double Read() => Now;
}
