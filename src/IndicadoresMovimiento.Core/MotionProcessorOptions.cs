namespace IndicadoresMovimiento.Core;

/// <summary>Parámetros de <see cref="MotionProcessor"/>. Se pueden cambiar en caliente.</summary>
public sealed class MotionProcessorOptions
{
    /// <summary>
    /// Constante de tiempo (s) con la que se estima la vertical cuando solo hay acelerómetro.
    /// Más larga = las curvas largas se notan mejor, pero cuesta más recuperarse si se mueve el dispositivo.
    /// </summary>
    public double GravityTimeConstant { get; set; } = 8.0;

    /// <summary>Constante de tiempo (s) de la vertical cuando además hay giroscopio.</summary>
    public double GravityTimeConstantWithGyro { get; set; } = 20.0;

    /// <summary>Constante de tiempo (s) cuando la fuente ya separa gravedad y aceleración (fusión de sensores).</summary>
    public double FusedGravityTimeConstant { get; set; } = 0.15;

    /// <summary>
    /// Si la fuerza medida se desvía de la vertical estimada más de este ángulo, se asume que
    /// el dispositivo se ha girado (no que el coche acelere) y la vertical se reajusta deprisa.
    /// </summary>
    public double ReorientationAngleDegrees { get; set; } = 25.0;

    /// <summary>
    /// Desviación instantánea (sin filtrar) a partir de la cual se asume un giro del dispositivo de inmediato.
    /// Ningún coche frena o gira tan fuerte en conducción normal (45° equivale a 1 g).
    /// </summary>
    public double ReorientationImmediateAngleDegrees { get; set; } = 45.0;

    /// <summary>El reajuste rápido termina cuando la desviación baja de este ángulo.</summary>
    public double ReorientationSettledDegrees { get; set; } = 3.0;

    /// <summary>Constante de tiempo (s) del reajuste rápido de la vertical.</summary>
    public double ReorientationTimeConstant { get; set; } = 0.3;

    /// <summary>
    /// Tras un reajuste rápido, durante este tiempo (s) la vertical sigue corrigiéndose a ritmo intermedio
    /// para eliminar el pequeño error que queda.
    /// </summary>
    public double RecoverySeconds { get; set; } = 3.0;

    /// <summary>Constante de tiempo (s) de la vertical durante la recuperación.</summary>
    public double RecoveryTimeConstant { get; set; } = 1.2;

    /// <summary>Constante de tiempo (s) de cada una de las dos etapas de suavizado (quita vibraciones).</summary>
    public double SmoothingTimeConstant { get; set; } = 0.08;

    /// <summary>Tiempo (s) para detectar que empieza el movimiento del vehículo.</summary>
    public double ActivityAttack { get; set; } = 0.2;

    /// <summary>Tiempo (s) que tarda en darse por terminado el movimiento.</summary>
    public double ActivityRelease { get; set; } = 6.0;

    /// <summary>Aceleración horizontal (m/s²) por debajo de la cual se considera que no hay movimiento.</summary>
    public double ActivityLow { get; set; } = 0.25;

    /// <summary>Aceleración horizontal (m/s²) a partir de la cual los puntos se muestran del todo.</summary>
    public double ActivityHigh { get; set; } = 0.7;

    /// <summary>Si no llegan muestras durante este tiempo (s), se considera que la fuente no tiene datos.</summary>
    public double StaleAfterSeconds { get; set; } = 1.0;

    /// <summary>Invierte izquierda/derecha (p. ej. si se viaja de espaldas a la marcha).</summary>
    public bool InvertLateral { get; set; }

    /// <summary>Invierte delante/detrás.</summary>
    public bool InvertLongitudinal { get; set; }
}
