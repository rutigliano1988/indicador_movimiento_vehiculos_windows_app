namespace IndicadoresMovimiento.Core;

/// <summary>Cuándo se muestran los puntos.</summary>
public enum DisplayMode
{
    /// <summary>Solo cuando se detecta movimiento de vehículo (como en el iPhone).</summary>
    Automatic,

    /// <summary>Siempre visibles.</summary>
    Always,
}

public sealed class CueRenderOptions
{
    /// <summary>Desplazamiento de los puntos (en píxeles independientes del DPI) por cada m/s².</summary>
    public double Gain { get; set; } = 18;

    /// <summary>Desplazamiento máximo en píxeles independientes del DPI.</summary>
    public double MaxOffset { get; set; } = 56;

    /// <summary>Rigidez del muelle (rad/s) que suaviza el movimiento de los puntos.</summary>
    public double Stiffness { get; set; } = 14;

    public DisplayMode Mode { get; set; } = DisplayMode.Automatic;
}

/// <summary>
/// Calcula, fotograma a fotograma, dónde dibujar los puntos y con qué opacidad.
/// Los puntos se desplazan en el sentido contrario a la aceleración, igual que el cuerpo:
/// al acelerar bajan, al frenar suben, en una curva a la derecha se van a la izquierda.
/// </summary>
public sealed class CueAnimator
{
    private const double MaxSubstep = 1.0 / 240;

    private double _velocityX;
    private double _velocityY;

    /// <summary>Desplazamiento horizontal en píxeles independientes del DPI (positivo = a la derecha).</summary>
    public double OffsetX { get; private set; }

    /// <summary>Desplazamiento vertical en píxeles independientes del DPI (positivo = hacia abajo).</summary>
    public double OffsetY { get; private set; }

    /// <summary>Opacidad de 0 a 1.</summary>
    public double Opacity { get; private set; }

    public void Reset()
    {
        OffsetX = OffsetY = Opacity = 0;
        _velocityX = _velocityY = 0;
    }

    /// <param name="state">Movimiento del vehículo.</param>
    /// <param name="dt">Tiempo transcurrido desde el fotograma anterior (s).</param>
    /// <param name="options">Opciones de dibujo.</param>
    /// <param name="enabled">Si es falso, los puntos se desvanecen.</param>
    public void Step(in CueState state, double dt, CueRenderOptions options, bool enabled = true)
    {
        if (!(dt > 0)) return;
        dt = Math.Min(dt, 0.1);

        double targetX = 0, targetY = 0;
        if (state.HasData && enabled)
        {
            targetX = SoftClamp(-options.Gain * state.Lateral, options.MaxOffset);
            targetY = SoftClamp(options.Gain * state.Longitudinal, options.MaxOffset);
        }

        // Muelle críticamente amortiguado: sigue al objetivo sin rebotes y sin saltos.
        var w = options.Stiffness;
        var steps = (int)Math.Ceiling(dt / MaxSubstep);
        var h = dt / steps;
        for (var i = 0; i < steps; i++)
        {
            _velocityX += (w * w * (targetX - OffsetX) - 2 * w * _velocityX) * h;
            OffsetX += _velocityX * h;
            _velocityY += (w * w * (targetY - OffsetY) - 2 * w * _velocityY) * h;
            OffsetY += _velocityY * h;
        }

        double targetOpacity;
        if (!enabled) targetOpacity = 0;
        else if (options.Mode == DisplayMode.Always) targetOpacity = 1;
        else targetOpacity = state.HasData ? state.Activity : 0;

        var tau = targetOpacity > Opacity ? 0.35 : 1.0;
        Opacity += (targetOpacity - Opacity) * (1 - Math.Exp(-dt / tau));
        if (targetOpacity == 0 && Opacity < 0.003) Opacity = 0;
    }

    /// <summary>Lineal para valores pequeños y saturación suave al acercarse al máximo.</summary>
    internal static double SoftClamp(double value, double max) =>
        max <= 0 ? 0 : max * Math.Tanh(value / max);
}
