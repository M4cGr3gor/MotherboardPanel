# Notas de desarrollo

Guía técnica para compilar, probar y modificar Motherboard Panel. Para el uso y la instalación, ver el [README](README.md).

## Convenciones

- Todo el texto (interfaz, comentarios, documentación) está en **español**.
- Los scripts `.ps1` se guardan en **UTF-8 con BOM**: sin él, PowerShell 5.1 estropea las tildes.
- En el repositorio solo hay código propio. Los binarios de terceros y los resultados de compilación (`build\`, `bin\`, `obj\`, `*.exe`) no se versionan; después de compilar se pueden borrar de `src\`.
- Los parches de la pantalla (`vendor\Parches\*.diff`) usan saltos de línea LF, como el código original en GitHub.

## Estructura

```
Instalar\                         instalador/desinstalador (PowerShell 5.1) y su lanzador .cmd
src\MotherboardPanel\             programa de bandeja (C++20 + Win32), build.ps1, tests\tests.cpp
src\RGBControl\                   luces: RgbApp (ventana, C#), RgbCore (lógica, C#), RgbCore.Tests (xUnit),
                                  RgbHelper (efectos avanzados, C++), build.ps1, tools\
vendor\Parches\                   parches 1-6 de turing-smart-screen-python y LEEME.txt
vendor\Temas\                     temas BloquesColor y BloquesColorPestanas y sus generadores
tests\                            pruebas Python de los parches 4, 5 y 6 y pruebas de comportamiento del panel
```

El paquete de instalación se monta aparte, con los binarios junto al script (ver el README).

## Herramientas

- Visual Studio Build Tools con C++ (los scripts cargan `vcvars64` por su cuenta, con `vswhere`).
- SDK de .NET 8 o posterior.
- Python 3 (3.14 en el desarrollo original) para la pantalla y las pruebas Python.
- git.

## Compilar y probar

```powershell
src\MotherboardPanel\build.ps1 -Test     # panel + 54 comprobaciones  → src\MotherboardPanel\Motherboard Panel.exe
src\RGBControl\build.ps1 -Test           # RGB Helper (73 comprobaciones C++) + RGB Control (37 pruebas C#) → src\RGBControl\*.exe
```

- `src\RGBControl\build.ps1` elimina la variable de entorno `Platform` que define `vcvars64`. Si no lo hiciera, `dotnet` compilaría en `bin\x64\…` y se copiaría un `.exe` antiguo.
- Si RGB Helper está en marcha, el script le pide que se cierre antes de sustituir su `.exe` (evento `RGBControl.Exit`).
- `src\RGBControl\tools\screenshot-ui.ps1` captura la ventana de RGB Control (opción `-Demo` con presets de ejemplo) para revisar el diseño sin tocar las luces. `tools\make-icon.ps1` genera el icono.

**Pruebas Python.** Necesitan un entorno con el `requirements.txt` de la pantalla:

```powershell
python tests\test_motherboard_panel.py
python tests\test_memory.py
python tests\test_ram_temperature.py
```

**Pruebas de comportamiento del panel** (`tests\pruebas-panel.ps1`, 13 pruebas). Solo funcionan con Motherboard Panel **desinstalado** y necesitan instalaciones de prueba en `build\`:

- `build\prueba-oficial`: pantalla oficial 3.10.0 + "Programa corregido", con `REVISION: SIMU` y `HW_SENSORS: PYTHON` en `config.yaml`.
- `build\prueba-helper`: copia de `RGB Helper.exe` y `RGB Control.exe`, sin `helper.cfg`.
- `build\prueba-panel`: `Motherboard Panel.exe` con un `Motherboard Panel.ini` (`[Rutas] Pantalla=…`, `Luces=…`) que apunta a las dos anteriores.

## Recompilar la pantalla

Pasos completos en [`vendor/Parches/LEEME.txt`](vendor/Parches/LEEME.txt). En resumen:

1. Partir del commit `2b33ab4f00a096916dd6a1174441a53a7ec33b03` de turing-smart-screen-python y aplicar los parches 1-6 en orden con `git apply`.
2. Crear un entorno virtual con `pip install -r requirements.txt` y ejecutar
   `pyinstaller --noconfirm --distpath build\dist --workpath build\work turing-system-monitor.spec`.
3. En `build\dist\turing-system-monitor`: borrar `res\themes\--Theme examples` y `config.yaml`, y escribir `version.txt` (`3.10.0+main-20260904+fixes.N`).
4. El "Programa corregido" son solo los archivos de `dist` que no existen o cambian (por MD5) respecto a la versión portátil oficial 3.10.0.
5. Probar en modo simulado sobre una copia (oficial + corregido) antes de reinstalar.

## Temas

`vendor\Temas\make_theme.py` genera **BloquesColor** (orden CPU, GPU, Red, RAM, Disco) y `make_theme_pestanas.py` genera **BloquesColorPestanas**, el tema por defecto: pestañas de color, orden CPU, GPU, RAM, Disco C:, Red, y la temperatura de cada módulo de RAM (datos `RamTemperature1/2` del parche 6). Se ejecutan con el Python de la pantalla (Pillow, PyYAML) y usan las fuentes de `res\fonts` del programa de la pantalla:

```powershell
python make_theme_pestanas.py <carpeta de salida>
```

Las posiciones se calculan desde cada tarjeta (`set_layout`): al cambiar el orden, BloquesColor debe seguir saliendo idéntico.

## Instalador (`Instalar\Instalar Motherboard Panel.ps1`)

Carpeta elegible, por defecto `C:\Program Files\Motherboard panel` (`Pantalla\`, `Luces\`, `Motherboard Panel.exe`, `Desinstalar.ps1`). Solo acepta una carpeta nueva, vacía o de Motherboard Panel, porque al desinstalar se borra entera. Fuera de Archivos de programa quita los permisos heredados.

1. Detiene todo y **desinstala PawnIO** si existe: el instalador oficial de la pantalla lo cambiaría por la 2.0.1, y su desinstalador deja la 2.2.0 cargada (error 1072 hasta reiniciar).
2. Instala la pantalla oficial en silencio (`/NOICONS /COMPONENTS=program,config`), copia encima el "Programa corregido" y los temas propios, borra lo sobrante (temas oficiales, fuentes que no se usan, fotos de `res\docs` y restos de Python 3.13) y configura el tema y la red.
3. LibreHardwareMonitor 0.9.6 en `Pantalla\external\LibreHardwareMonitor` (comprobado por MD5).
4. PawnIO 2.2.0 en `C:\Program Files\PawnIO`, con su instalador (firma verificada). Antes detiene el driver con `sc stop`; si no, la 2.2.0 no se carga hasta reiniciar.
5. Luces y panel: clave `HKLM\SOFTWARE\Motherboard Panel` (InstallDir), acceso directo, tarea programada y desinstalador registrado.

**Desinstalación:** "Aplicaciones instaladas" → *Motherboard Panel - pantalla USB y luces RGB* (`HKLM\...\Uninstall\MotherboardPanel`, ejecuta `Desinstalar.ps1 -Accion Desinstalar`). Quita todo, también PawnIO. Si el driver sigue cargado, deja una tarea de un solo uso, *"Motherboard Panel - limpieza de PawnIO"* (SYSTEM, al arrancar), que termina la limpieza y se borra a sí misma.

## Funcionamiento

- **Arranque:** tarea programada "Motherboard Panel" (al iniciar sesión, 3 s de retraso, nivel más alto). Si el panel se abre sin elevar, ejecuta la tarea y se cierra. La pantalla necesita administrador (LibreHardwareMonitor + PawnIO); las luces se arrancan como el usuario normal (token del Explorador). Al iniciarse, el panel arranca la pantalla y, si existe `helper.cfg` y `HKCU\Software\RGBControl\HelperAutostart` no es 0, también RGB Helper. Así no se espera a la cola `Run` de Windows, que procesa los programas de uno en uno (hasta 30 s cada uno) y tardaba más de 1,5 minutos.
- **Icono:** como el panel va elevado, se permite el mensaje `TaskbarCreated` (`ChangeWindowMessageFilterEx`), y cada vigilancia vuelve a crear el icono si ha desaparecido.
- **Mutex de presencia** `MotherboardPanel`: mientras existe, la pantalla (parche 4) y RGB Helper ocultan su propio icono.
- **Pantalla:** cierre limpio con `WM_CLOSE` a la ventana oculta `turingEventWndClass`. Vigilancia cada 2 s, como máximo 3 reinicios en 5 minutos. Tras cerrar el configurador sin guardar, se vuelve a arrancar a los 8 s.
- **Datos:** `%LOCALAPPDATA%\RGB Control\` (`presets.json`, `helper.cfg`), `%LOCALAPPDATA%\Motherboard Panel\registro.log` (máx. 1 MB) y `Pantalla\log.log` (máx. 1 MB).
- **Memoria esperada:** pantalla ~30 MB (parche 5), panel ~2 MB, RGB Helper ~1-3 MB.

## Luces: el controlador ITE IT5711

Protocolo basado en la documentación de OpenRGB (controlador Gigabyte RGB Fusion 2 USB).

- **Dispositivo:** HID `048D:5711`, colección con UsagePage `0xFF89` y Usage `0xCC`. Se usan *feature reports* de 64 bytes con ID `0xCC`. `0x60` y `0x61` devuelven la información y la calibración (orden de bytes de color por conector).
- **Efectos del chip:** se programan por zona y se aplican con `0x28`. Se graban en la flash con `0x47 1` + `0x5E`, y así se mantienen sin programa y tras reiniciar. El "arcoíris de fábrica" se obtiene poniendo una onda en la zona 2. En la X870 GAMING X WIFI7, el efecto *Wave* básico (6) no está soportado.
- **Modo LED a LED** (efectos avanzados): `0x48 0` (sin control de Windows), **reinicio de los registros de efectos (`0x20`–`0x27`, `0x90`–`0x92` + aplicar)**, `0x31 0`, `0x32` (efectos de serie desactivados) y `0x34` (tamaño de tira). Sin el reinicio, tras encender o reanudar el equipo el chip sigue con su efecto guardado e ignora los colores. Los colores van en paquetes de hasta 19 LEDs a las cabeceras `0x58`, `0x59` y `0x62` (D_LED1-3).
- **Iluminación dinámica de Windows:** si está activa, pisa los colores. RGB Control la desactiva (`HKCU\Software\Microsoft\Lighting\AmbientLightingEnabled = 0` y `0x48 0`).
- **RGB Control ↔ RGB Helper:** RGB Control escribe `helper.cfg` (`clave=valor`, con el efecto ya resuelto y los paquetes `enter`/`restore` precalculados) y avisa con los eventos `RGBControl.Reload`, `RGBControl.Exit` (cerrar sin restaurar) y `RGBControl.Stop` (cerrar devolviendo el efecto normal). Mutex de instancia única: `RGBControl.Helper`. `helper.cfg` solo existe con un efecto avanzado activo. `HKCU\...\Run\RGBControlHelper` queda como respaldo del arranque; si el ayudante ya está en marcha, la segunda copia se cierra al instante.
- **Calibración:** `LedCounts` (D_LED1-3) y `LedCountsCalibrated` en `presets.json`. Sin calibrar se usan 32 LEDs por conector.

## Trampas conocidas

- **Registro virtualizado:** las aplicaciones empaquetadas (MSIX) y los procesos que lanzan pueden ver una copia virtualizada y antigua de `HKCU`. Para saber el contenido real, consúltalo con `reg query` desde una consola normal. Los programas de Motherboard Panel sí ven el registro real.
- PowerShell convierte `$null` en `""` al llamar a funciones nativas: usa `[NullString]::Value`. Una sola instancia CIM no tiene `.Count`: envuélvela con `@()`. `$args` es una variable reservada.
- Con `ctypes`, declara `argtypes`/`restype` (sin ellos, `SetProcessWorkingSetSize` no hace nada).
- La pantalla con `HW_SENSORS: LHM` se cierra si no es administrador: para pruebas sin elevar, usa `PYTHON` + `SIMU`.
- Sin elevar no se pueden leer las tareas de SYSTEM: `schtasks` responde "Acceso denegado" si la tarea existe y "no se encuentra" si no existe.
