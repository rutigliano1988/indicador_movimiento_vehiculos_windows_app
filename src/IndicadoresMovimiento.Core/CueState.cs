namespace IndicadoresMovimiento.Core;

/// <summary>
/// Movimiento del vehículo ya filtrado, listo para animar los puntos.
/// </summary>
/// <param name="Lateral">Aceleración lateral en m/s². Positiva cuando el coche gira (o se desplaza) hacia la derecha.</param>
/// <param name="Longitudinal">Aceleración longitudinal en m/s². Positiva al acelerar, negativa al frenar.</param>
/// <param name="Activity">De 0 a 1: cuánto movimiento de vehículo se ha detectado recientemente.</param>
/// <param name="HasData">Falso si la fuente no está enviando datos.</param>
public readonly record struct CueState(double Lateral, double Longitudinal, double Activity, bool HasData);
