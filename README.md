# Motherboard Panel

Panel para Windows que reúne en un solo icono de la bandeja del sistema dos cosas que normalmente necesitan programas distintos:

- **Luces RGB** de placas base **Gigabyte con el controlador ITE IT5711**, con **RGB Control**: efectos que se guardan en la propia placa y siguen funcionando sin ningún programa abierto, y efectos avanzados (degradados, cometa, fuego, aurora…) con los colores que elijas.
- **Pantalla USB Turing** (Turing Smart Screen y compatibles) con [turing-smart-screen-python](https://github.com/mathoudebine/turing-smart-screen-python), más unos parches propios que corrigen errores, reducen la memoria y añaden la temperatura de la RAM.

Todo está en español.

---

## Contenido

| Parte | Carpeta | Qué es |
|---|---|---|
| **Motherboard Panel** | `src/MotherboardPanel` | Programa de bandeja (C++). Arranca y vigila la pantalla y las luces, con un único icono y menú. Se inicia con Windows mediante una tarea programada. |
| **RGB Control** | `src/RGBControl` | Luces RGB. `RGB Control.exe` (ventana, C#) y `RGB Helper.exe` (efectos avanzados en segundo plano, C++). |
| **Pantalla** | `vendor/Parches`, `vendor/Temas` | Parches sobre turing-smart-screen-python y temas propios (`BloquesColor`, `BloquesColorPestanas`). |
| **Instalador** | `Instalar` | Script que instala o desinstala todo en una carpeta (por defecto `C:\Program Files\Motherboard panel`). |
| **Pruebas** | `tests`, `src/*/tests`, `src/RGBControl/src/RgbCore.Tests` | Pruebas automáticas. |

---

## ¿Me sirve?

### Luces (RGB Control)

**Sí**, si tienes:

- Una **placa Gigabyte con el controlador ITE IT5711**, que Windows ve como el dispositivo USB **`048D:5711`**. Lo llevan muchas placas Gigabyte recientes (varias X870, B850, Z890 y algunas B760/B840). Para comprobarlo:
  - en *Configuración → Personalización → Iluminación dinámica* aparece un **"GIGABYTE Device"**, o
  - en el *Administrador de dispositivos* hay un dispositivo HID con `VID_048D&PID_5711` (pestaña *Detalles → Id. de hardware*).
- **Windows 10 u 11 de 64 bits.** No hace falta instalar nada: `RGB Control` usa .NET Framework 4.8, que ya trae Windows, y `RGB Helper` es autónomo.

**No**, si:

- tu placa es de **ASUS, MSI, ASRock** u otro fabricante (usan otros chips y protocolos);
- es una Gigabyte **más antigua** con controlador **IT5702 o IT8297**: el protocolo es muy parecido, pero el programa solo busca el IT5711 y habría que adaptarlo;
- tus luces van por un **controlador propio** con su programa (por ejemplo, kits de Corsair o Lian Li con su propio mando).

Las luces deben ir a los conectores **ARGB de 3 pines y 5 V** de la placa (**D_LED**). Si los ventiladores van a un controlador de la caja que recibe la señal de la placa (lo más habitual), todos mostrarán el mismo patrón.

### Pantalla

Necesitas una pantalla USB compatible con turing-smart-screen-python (Turing Smart Screen, XuanFang y otras; consulta [su lista](https://github.com/mathoudebine/turing-smart-screen-python#readme)). Algunos sensores necesitan permisos de administrador; por eso Motherboard Panel arranca la pantalla elevada mediante su tarea programada.

---

## Instalación

El repositorio contiene **solo el código propio**. Los programas de terceros (el instalador oficial de la pantalla, LibreHardwareMonitor y PawnIO) no se incluyen: hay que descargarlos de sus proyectos (ver [Compilación](#compilación)) para montar el paquete de instalación.

Con el paquete montado:

1. Ejecuta `Instalar\Instalar Motherboard Panel.cmd`. Pide permisos de administrador.
2. Elige **Instalar**. Se copia todo a la carpeta elegida, se instala la pantalla con sus correcciones y se crea la tarea programada **"Motherboard Panel"**, que lo arranca al iniciar sesión.
3. La primera vez, Windows puede mostrar **"Windows protegió su PC"** porque los ejecutables no están firmados: *Más información → Ejecutar de todas formas*.

El mismo script permite **Desinstalar** y ver el **Estado** (`-Accion Instalar | Desinstalar | Estado`).

---

## Uso de las luces (RGB Control)

Abre **RGB Control** desde el menú del icono de Motherboard Panel (*Luces RGB → Abrir panel de luces…*) o directamente desde su carpeta.

- **Efectos normales** (color fijo, respiración, parpadeo, ciclo de colores, arcoíris de fábrica, apagado): al pulsar **Aplicar** se **graban en la placa** y siguen funcionando sin ningún programa abierto, también al reiniciar.
- **Efectos avanzados** (degradado fijo o giratorio, ciclo de colores propio, arcoíris ajustable, cometa, respiración multicolor, destellos, fuego, vela, latido, aurora, escáner, persecución, doble cometa y tormenta): los genera **RGB Helper** en segundo plano, con muy poco consumo (unos 2-3 MB de memoria y menos del 1 % de un núcleo). Mientras arranca Windows se ve el último efecto normal guardado.
- Al aplicar, la **Iluminación dinámica de Windows** se desactiva automáticamente: si no, pisaría los colores.
- Los presets propios se crean, duplican, ordenan arrastrando y se les puede poner un icono.
- Los ajustes se guardan en `%LOCALAPPDATA%\RGB Control\presets.json`.

### Calibrar los LEDs (importante para los efectos avanzados)

Para que un degradado ocupe exactamente un ventilador, el programa necesita saber cuántos LEDs hay en cada conector. Pulsa **Calibrar LEDs…**:

1. Para cada conector (D_LED1, D_LED2, D_LED3) se encienden varios LEDs en verde y el último en **rojo**.
2. Ajusta con **−** y **+** (o con las flechas del teclado) hasta que el LED rojo sea el **último de un ventilador** o de la tira. Si en un conector hay varias cosas, usa la que tenga más LEDs.
3. Marca **"No hay nada conectado"** en los conectores vacíos y pulsa **Guardar**.

Sin calibrar se usan 32 LEDs por conector, y los efectos avanzados pueden verse cortados.

---

## Compilación

Necesitas:

- **Visual Studio Build Tools** 2022 o posterior, con la carga de trabajo *Desarrollo para el escritorio con C++*.
- **SDK de .NET** 8 o posterior (para compilar los proyectos de .NET Framework 4.8).
- **Python 3** (solo para reconstruir la pantalla y para las pruebas de `tests`).

### RGB Control

```powershell
cd src\RGBControl
.\build.ps1 -Test
```

Compila `RGB Control.exe` y `RGB Helper.exe`, ejecuta las pruebas de C# y C++ y deja los dos `.exe` en `src\RGBControl`.

### Motherboard Panel

```powershell
cd src\MotherboardPanel
.\build.ps1
```

### Pantalla (turing-smart-screen-python con los parches)

La pantalla se reconstruye a partir del commit **`2b33ab4`** del proyecto original más los parches de `vendor/Parches`, en orden. Las instrucciones detalladas están en [`vendor/Parches/LEEME.txt`](vendor/Parches/LEEME.txt).

### Paquete de instalación

Dentro de `Instalar\` se monta esta estructura junto al script:

```
Instalar\
  Instalar Motherboard Panel.cmd / .ps1
  Motherboard Panel\Motherboard Panel.exe
  Luces\RGB Control.exe, RGB Helper.exe
  Pantalla\Turing system monitor 3.10.0.exe     (instalador oficial, de las releases del proyecto)
  Pantalla\Programa corregido\                  (archivos compilados con los parches)
  Pantalla\Parches\ , Pantalla\Temas\
  Drivers\PawnIO_2.2.0.exe                      (de su proyecto)
  Drivers\LibreHardwareMonitor_0.9.6\           (de sus releases)
```

---

## Seguridad y responsabilidad

- RGB Control se comunica con el controlador de la placa con el mismo protocolo que el software de Gigabyte y graba los efectos en su memoria, igual que ese software. Úsalo bajo tu responsabilidad: **se ofrece sin ninguna garantía**.
- En los conectores **D_LED** (3 pines, 5 V) conecta solo dispositivos ARGB de 5 V. Nunca conectes una tira de 5 V a un conector RGB de 12 V (4 pines).

---

## Créditos y licencias

Este proyecto se apoya en el trabajo de otros proyectos de software libre:

| Proyecto | Licencia | Uso |
|---|---|---|
| [turing-smart-screen-python](https://github.com/mathoudebine/turing-smart-screen-python) (Matthieu Houdebine y colaboradores) | GPL-3.0 | Programa de la pantalla. Aquí solo hay parches sobre el commit `2b33ab4`; el parche 1 es el [PR 1036](https://github.com/mathoudebine/turing-smart-screen-python/pull/1036). |
| [OpenRGB](https://gitlab.com/CalcProgrammer1/OpenRGB) | GPL-2.0 o posterior | Documentación del protocolo del controlador Gigabyte RGB Fusion 2 / ITE IT5711, en la que se basa RGB Control. |
| [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | MPL-2.0 | Sensores de la pantalla. Se instala sin cambios; no se incluye en el repositorio. |
| [PawnIO](https://github.com/namazso/PawnIO) | GPL-2.0 | Controlador que necesita LibreHardwareMonitor. Se instala sin cambios; no se incluye en el repositorio. |
| [libusb](https://github.com/libusb/libusb) | LGPL-2.1 | Lo incluye el programa de la pantalla. |

El código de este repositorio se publica bajo la **Licencia Pública General de GNU, versión 3 (GPL-3.0)**. Consulta [`LICENSE`](LICENSE).

Gigabyte, RGB Fusion y Turing son marcas de sus respectivos propietarios. Este proyecto no está afiliado a ellos.
