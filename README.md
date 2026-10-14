# AimSync

AimSync es una herramienta de asistencia para juegos de PC que permite sincronizar y automatizar acciones a través de controladores físicos y virtuales. Combina anti-recoil, macros, turbo y remapeo, con una interfaz WPF moderna.

## ¿Qué es AimSync?

AimSync está diseñado para ayudar en tareas repetitivas de juegos (como gestión de disparos, macros de habilidad o turbo) usando un controlador virtual (ViGEmBus) mientras se monitorean dispositivos físicos de entrada. El proyecto está dividido en tres capas:

- **AimSync.Core**: Lógica de negocio, motores (anti-recoil, macros, turbo, stick), dispositivos, mapeo, diagnóstico y almacenamiento.
- **AimSync.App**: Interfaz WPF (MVVM), vistas, temas, servicios y visualización en tiempo real.
- **AimSync.Test**: Pruebas, utilidades de diagnóstico, fakes e instrumentación para validar el comportamiento.

**Estado actual:** El proyecto está en desarrollo. Puede compilar y ejecutar en entornos Windows, pero aún no está completo y puede contener errores o funcionalidades pendientes.

## Requisitos para ejecutar

### Hardware
- Windows 10/11 (x64 recomendado)
- Controlador físico compatible (XInput u otro detectado por el sistema) — opcional para algunas funciones, requerido para probar integración física
- Dispositivo de entrada (ratón/teclado) si se usan macros/captura de entrada

### Software
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [ViGEmBus](https://github.com/ViGEm/ViGEmBus) — necesario para crear un controlador virtual (ViGEm)
- Visual Studio 2022, Rider o `dotnet` CLI para compilar/ejecutar

### Dependencias del proyecto
Las dependencias se gestionan vía NuGet:
- `SharpDX.XInput`
- `Nefarius.ViGEm.Client`
- `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.DependencyInjection`
- `Serilog.*`
- `CommunityToolkit.Mvvm`
- `xunit`, `coverlet.collector`, `Microsoft.NET.Test.Sdk`

## Estructura del repositorio

```text
AimSync.slnx
AimSync.Core/        # Núcleo: motores, dispositivos, mapeo, diagnóstico
AimSync.App/         # UI WPF + ViewModels + servicios
AimSync.Test/        # Tests, diagnósticos y utilidades
Controller-FaseA-Resultados.md  # Documentación de fase
```

## Cómo compilar

Desde la raíz:

```bash
dotnet restore AimSync.slnx
dotnet build AimSync.slnx --configuration Release
```

O abrir `AimSync.slnx` en Visual Studio/Rider.

## Cómo ejecutar

Aplicación WPF (requiere Windows):

```bash
dotnet run --project AimSync.App --configuration Release
```

## Tests

Ejecutar todos los tests:

```bash
dotnet test AimSync.slnx --configuration Release --logger "console;verbosity=minimal"
```

Algunos tests son diagnósticos/instrumentales (proyectos de consola auxiliares). Requieren permisos/entorno Windows (acceso a entrada, ViGEmBus instalado). En entornos sin hardware/ViGEm pueden fallar — es esperado mientras el proyecto está en desarrollo.

## Funcionalidades principales (actuales)

- Monitoreo de controlador físico (XInput + fuente física)
- Creación y gestión de controlador virtual (ViGEm)
- Anti-recoil configurable (curvas lineales, umbrales, sensibilidad)
- Macros (pasos, timing)
- Turbo
- Motor de stick/mapeo básico
- Captura de teclado/ratón
- Diagnóstico (logger estructurado)
- UI MVVM con vistas: Dashboard, AntiRecoil, Macros, Teclado, Ratón, InputTester, Controller, ComingSoon

## Limitaciones / Errores conocidos

- Proyecto en fase activa: hay funcionalidades marcadas "ComingSoon" y lógica aún por completar
- Requiere ViGEmBus instalado para usar controlador virtual
- Algunos tests dependen de hardware específico (XInput, ratón/teclado) y no son unitarios puros
- Puede haber warnings/estilos pendientes de limpiar
- Comportamiento puede variar según versión de Windows/ViGEm

## Instalación rápida

1. Instalar .NET 9 SDK
2. Instalar ViGEmBus
3. Clonar repositorio: `git clone https://github.com/keinergarcia/AimSync.git`
4. Compilar: `dotnet build AimSync.slnx`
5. Ejecutar App: `dotnet run --project AimSync.App`

## Contribuir

Si encuentras errores o quieres proponer mejoras, abre un issue con:
- Versión de Windows, .NET, ViGEmBus
- Pasos para reproducir
- Logs/resultado esperado vs actual
- Capturas (si aplica)

## Licencia

Revisa el repositorio para detalles de licencia.
