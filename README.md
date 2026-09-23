<p align="center"><img src="docs/icono.png" width="96" alt=""></p>

# Indicadores de movimiento para Windows

Versión para Windows de la función **«Indicadores de movimiento en vehículos»** del iPhone (*Vehicle Motion Cues*):
unos puntos animados en los bordes de la pantalla que se mueven con el coche para **reducir el mareo** al usar el
ordenador como pasajero.

> Proyecto independiente, sin relación con Apple.

## Cómo funciona

El mareo en el coche aparece cuando lo que ves (una pantalla quieta) no coincide con lo que nota tu cuerpo
(el coche acelera, frena y gira). La aplicación dibuja puntos en los bordes de la pantalla que se desplazan
igual que te empuja el vehículo:

| El coche… | Los puntos… |
|---|---|
| acelera | bajan |
| frena | suben |
| gira a la derecha | se van a la izquierda |
| gira a la izquierda | se van a la derecha |

Los puntos **no tapan el contenido ni interceptan los clics**: están en los bordes y el ratón «los atraviesa».
Por defecto solo aparecen cuando se detecta el movimiento del vehículo y se desvanecen al parar.

## De dónde saca el movimiento

| Fuente | Cuándo usarla |
|---|---|
| **Sensor del ordenador** | Tabletas, convertibles 2 en 1 (Surface, Yoga, etc.) y los portátiles que traen acelerómetro. |
| **Móvil por Wi-Fi** | Si el portátil no tiene sensor, que es lo habitual en portátiles clásicos. El móvil abre una página web y envía sus sensores; **no hay que instalar nada en el móvil**. |
| **Demostración** | Simula un trayecto (acelerar, frenar, curvas y una rotonda) para ver cómo funciona. |

Con la opción **Automática** (la predeterminada) se usa el sensor del ordenador si existe, y el móvil en cuanto se conecta.

## Descargar y abrir

1. En este repositorio, entra en **Actions** → la ejecución más reciente de **Compilar** que esté en verde →
   sección **Artifacts** y descarga:
   - **IndicadoresMovimiento-x64** para la mayoría de ordenadores (Intel o AMD).
   - **IndicadoresMovimiento-arm64** para ordenadores con procesador ARM (por ejemplo, Snapdragon).

   Si hay versiones publicadas, también están en **Releases**.
2. Descomprime el archivo y guarda el `.exe` en una carpeta fija (por ejemplo, *Documentos*).
3. Ábrelo. No necesita instalación ni permisos de administrador.
   Si aparece **«Windows protegió su PC»**, pulsa **Más información → Ejecutar de todas formas**
   (sale porque el ejecutable no está firmado digitalmente).

La aplicación queda como un icono junto al reloj de Windows. Haz clic en él para abrir los ajustes.

**Requisitos:** Windows 10 (versión 1809 o posterior) u 11, de 64 bits.

## Usar el móvil como sensor

1. Pon el móvil y el ordenador en la **misma red Wi-Fi**. En el coche, lo más fácil es conectar el ordenador
   al **punto de acceso personal** («Compartir internet») del propio móvil.
2. En el icono de la aplicación, elige **Usar el móvil como sensor…** y escanea el código QR con la cámara del móvil.
3. El navegador mostrará un **aviso de seguridad**. Es normal: la conexión va directamente del móvil al
   ordenador, cifrada con un certificado propio.
   - **iPhone (Safari):** «Mostrar detalles» → «visitar este sitio web» → «Visitar sitio web».
   - **Android (Chrome):** «Configuración avanzada» → «Acceder a … (sitio no seguro)».
4. Pulsa **Empezar** y **permite el acceso al movimiento**.
5. Deja el móvil **fijo**, con la página abierta y la **pantalla encendida** (mejor si está cargando):
   - de pie en un soporte con la pantalla mirando hacia ti (en vertical u horizontal), o
   - tumbado boca arriba con la parte de arriba hacia la parte delantera del coche.

Si Windows pregunta por el **firewall**, pulsa «Permitir acceso». Ojo: Windows solo da ese permiso en el tipo de
red donde se abrió la aplicación por primera vez (normalmente la Wi-Fi de casa, «privada»), y el punto de acceso del
móvil suele quedar como red «pública». Si en el coche el móvil no conecta, pulsa **Permitir la conexión del móvil en
cualquier red** en la ventana del código QR: pide permiso de administrador una vez y deja pasar al móvil en cualquier
red (solo en los puertos de la aplicación, del 47800 al 47809).

## Consejos

- **Ctrl+Alt+M** muestra u oculta los puntos desde cualquier aplicación.
- Si los puntos se mueven al revés (por ejemplo, si viajas de espaldas a la marcha), usa las opciones
  **Invertir** en los ajustes.
- Por defecto los puntos **no salen al compartir pantalla** en videollamadas ni en capturas.
- Funciona mejor si el ordenador (o el móvil) no se mueve respecto al coche.
- En los ajustes puedes cambiar el tamaño, la separación, el color, la opacidad y la sensibilidad de los puntos,
  mostrarlos también arriba y abajo, y hacer que la aplicación se abra al iniciar Windows.

## Privacidad

- Todo funciona **en local**: la aplicación no se conecta a internet ni envía datos a ningún servidor.
- El móvil envía sus lecturas solo al ordenador, por la red local y cifradas (HTTPS). Cada instalación tiene un
  código aleatorio (incluido en el QR) sin el que el ordenador rechaza los datos.
- Los ajustes, el certificado y el registro de errores se guardan en `%LOCALAPPDATA%\IndicadoresMovimiento`.

## Limitaciones conocidas

- Si el portátil no tiene sensor, hace falta el móvil con la página abierta y la pantalla encendida.
- La mayoría de portátiles no tienen giroscopio. Sin él, la vertical se estima promediando el acelerómetro:
  en curvas muy largas los puntos vuelven poco a poco al centro, y tras mover la tapa del portátil tardan unos
  segundos en estabilizarse. Con el móvil (que combina acelerómetro y giroscopio) esto no ocurre.
- En ordenadores de empresa, la política de seguridad puede bloquear ejecutables sin firmar o las conexiones
  entrantes desde el móvil.
- Los puntos no se ven encima de juegos en pantalla completa exclusiva.
- Se asume que miras hacia delante, en el sentido de la marcha.

## Para desarrolladores

Hecho en C# con .NET 10 y WPF.

```
src/
  IndicadoresMovimiento.Core    Cálculo del movimiento (sin dependencias de Windows): filtros, ejes del
                                vehículo, animación de los puntos, ajustes y simulador de conducción.
  IndicadoresMovimiento.Movil   Servidor HTTPS local y la página web para el móvil (wwwroot/movil.html).
  IndicadoresMovimiento.App     Aplicación de Windows: bandeja del sistema, ventanas transparentes con los
                                puntos, sensores de Windows y ventanas de ajustes y de conexión del móvil.
tests/
  IndicadoresMovimiento.Tests   Pruebas del cálculo, la animación, los ajustes y el servidor del móvil.
```

```bash
dotnet test tests/IndicadoresMovimiento.Tests          # funciona también en Linux y macOS
dotnet build src/IndicadoresMovimiento.App             # compila en cualquier sistema; se ejecuta en Windows

# Ejecutable único que no necesita tener .NET instalado:
dotnet publish src/IndicadoresMovimiento.App -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

### Cómo se calcula

1. Las lecturas se pasan a una convención común: fuerza específica en m/s² que, en reposo, apunta hacia arriba.
   Windows y Safari en iPhone la dan con el signo contrario; la página del móvil lo detecta comparando con la
   orientación del dispositivo.
2. La vertical se estima con la gravedad (fusión de sensores si la fuente la ofrece, giroscopio si lo hay o un
   promedio lento del acelerómetro, con reajuste rápido si el dispositivo cambia de posición).
3. La «derecha» del coche es la horizontal contenida en el plano de la pantalla (vale aunque esté girada) y
   «delante» es perpendicular a ambas. Así se separa la aceleración lateral de la longitudinal.
4. Un filtro de unos 2 Hz elimina las vibraciones de la carretera, y un muelle críticamente amortiguado mueve
   los puntos con suavidad en sentido contrario a la aceleración.
