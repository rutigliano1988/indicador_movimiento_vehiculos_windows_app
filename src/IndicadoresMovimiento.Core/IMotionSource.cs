namespace IndicadoresMovimiento.Core;

/// <summary>Una fuente de lecturas de movimiento (sensor del ordenador, móvil o simulación).</summary>
public interface IMotionSource : IDisposable
{
    /// <summary>Nombre para mostrar (en español).</summary>
    string DisplayName { get; }

    /// <summary>Si el equipo tiene lo necesario para usar esta fuente.</summary>
    bool IsAvailable { get; }

    bool IsRunning { get; }

    /// <summary>Procesador que recibe las lecturas de esta fuente.</summary>
    MotionProcessor Processor { get; }

    /// <summary>Estado actual explicado para el usuario.</summary>
    string StatusText { get; }

    Task StartAsync();

    Task StopAsync();
}
