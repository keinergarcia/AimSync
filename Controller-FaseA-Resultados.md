# AimSync - Fase A: Mando fisico (resultados)

Fecha: 2026-10-08

## Estado objetivo
Modulo para mando fisico (GameSir en modo XInput): deteccion + representacion visual en tiempo real. Reutiliza el mando virtual ViGEm existente.

## Pruebas (separadas por categoria)

### 1. Simuladas / automatizadas (no requieren hardware) - EJECUTADAS: TODAS PASAN
Ejecutar: `dotnet run --project AimSync.Test -- --controllersim`

| ID  | Prueba                                  | Resultado |
|-----|-----------------------------------------|-----------|
| C1  | Deteccion de conexion                   | PASS      |
| C2  | Deteccion de desconexion                | PASS      |
| C3  | Evento DevicesChanged                   | PASS      |
| C4  | Seleccion de mando (slot)               | PASS      |
| C5  | Normalizacion de ejes                   | PASS      |
| C6  | Normalizacion de gatillos               | PASS      |
| C7  | Mapeo de botones ABXY/LB/RB             | PASS      |
| C8  | Direccion de cruceta con diagonales     | PASS      |
| C9  | Nombre real de Windows (probe)          | PASS      |
| C10 | Nombre generico sin probe               | PASS      |
| C11 | Estado Error si Windows reporta problema| PASS      |
| C12 | Fuente no disponible no rompe           | PASS      |
| C13 | Estado se publica para el slot seleccionado | PASS   |
| C14 | Prefiere la clase XInput para el nombre | PASS      |

### 2. Sonda hardware (mando fisico GameSir conectado) - EJECUTADA
Ejecutar: `dotnet run --project AimSync.Test -- --controllerprobe` (o `--controllerprobe --watch`).

Ejecutada con el **GameSir conectado en modo XInput**:

| Comprobacion                                   | Resultado |
|------------------------------------------------|-----------|
| API de entrada `XInput (xinput1_4)` disponible | PASS      |
| Deteccion del mando (slot 0 activo)            | PASS      |
| Nombre real de Windows                         | PASS - "Controlador de Xbox 360 para Windows" |
| VID/PID identifica GameSir                     | PASS - 0x3537 / 0x1096 |
| Lectura en vivo (movimiento del stick izq.)    | PASS - LX vario de 0 a -833 / 352; LY 1248-2064 |
| Botones / gatillos / cruceta                   | PENDIENTE - no se pulsaron durante la captura |

> Observacion de hardware: en reposo el eje Y del stick izquierdo no es 0
> (rondaba LY ~= 1648). Conviene un valor muerto (deadzone) en fases posteriores.
>
> Observacion: Windows expone el GameSir en **varias** entradas (1 de clase
> XInput + varias HID). Por eso el nombre se toma de la entrada de clase
> XInput (XnaComposite); ver correccion mas abajo.

### 3. Validacion de la vista (WPF) y de la cadena ViewModel - PASADA
- `ControllerView.xaml` carga, se mide y no lanza excepciones (check de `StaticResource` y bindings en runtime) via proyecto de humo temporal.
- Cadena real ViewModel (`ControllerViewModel`) + `PhysicalControllerMonitor` + hardware: `api=XInput (xinput1_4)`, `available=True`, `device='Controlador de Xbox 360 para Windows'`, `slot=0`, `status=Conectado`, `devices=1`.
- La app completa arranca sin errores.

## Correcciones aplicadas durante la validacion
- `ControllerView.xaml`: `StringFormat=X={0}` (invalido en MarkupExtension) -> `'X {0}'` (error MC3042).
- Asignacion de nombre: el probe devolvia N entradas (XInput + HID) y XInput
  M conectadas, por lo que `probed.Count != connectedSlots.Count` y se caia al
  nombre generico. Ahora `WindowsGameDevice.IsXInput` marca la clase
  XnaComposite y el monitor prefiere esas entradas para el nombre (test C14).
- Sonda `--watch`: `Console.KeyAvailable` lanza si la entrada esta redirigida;
  ahora, si la entrada no es interactiva, muestrea ~8 s y termina sola.