using System.Numerics;

namespace IndicadoresMovimiento.Core;

/// <summary>
/// Una lectura de sensores expresada en los ejes del dispositivo que la genera:
/// X hacia la derecha de la pantalla, Y hacia la parte de arriba de la pantalla
/// y Z saliendo de la pantalla hacia quien la mira.
/// </summary>
/// <param name="Timestamp">Instante de la lectura en segundos (reloj monótono de la fuente).</param>
/// <param name="SpecificForce">
/// Aceleración medida por el acelerómetro, gravedad incluida, en m/s². Con el dispositivo en reposo
/// apunta hacia ARRIBA y mide unos 9,8 m/s² (convención de Android y de la especificación W3C).
/// </param>
/// <param name="LinearAcceleration">
/// Aceleración sin gravedad en m/s², si la fuente ya la calcula con fusión de sensores (p. ej. el iPhone).
/// </param>
/// <param name="AngularVelocity">Velocidad angular en rad/s (regla de la mano derecha), si hay giroscopio.</param>
/// <param name="FlatRightAxis">
/// Eje del dispositivo que apunta a la derecha de lo que se ve en pantalla. Solo se usa cuando el
/// dispositivo está tumbado; si es nulo se toma +X.
/// </param>
public readonly record struct MotionSample(
    double Timestamp,
    Vector3 SpecificForce,
    Vector3? LinearAcceleration = null,
    Vector3? AngularVelocity = null,
    Vector3? FlatRightAxis = null)
{
    /// <summary>Gravedad estándar en m/s².</summary>
    public const float StandardGravity = 9.80665f;
}
