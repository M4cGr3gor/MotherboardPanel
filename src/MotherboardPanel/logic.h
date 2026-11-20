// Lógica pura de Motherboard Panel (sin efectos sobre el sistema): se prueba en tests\tests.cpp.
#pragma once

#include <string>
#include <vector>

#include <windows.h>

// True si el código de salida de un proceso corresponde a una excepción (fallo), no a un cierre normal.
bool IsCrashExitCode(DWORD code);

// Límite de reinicios automáticos: como máximo `max` dentro de una ventana de `windowMs` milisegundos.
class RestartBudget
{
public:
    explicit RestartBudget(int max = 3, ULONGLONG windowMs = 300000);
    // Si queda margen, anota un reinicio en `nowMs` y devuelve true.
    bool TryConsume(ULONGLONG nowMs);

private:
    int max_;
    ULONGLONG windowMs_;
    std::vector<ULONGLONG> uses_;
};

// Ruta del ejecutable de una línea de órdenes ("C:\a b\x.exe" -arg  ->  C:\a b\x.exe).
std::wstring ExeFromCommandLine(const std::wstring& cmd);

// Carpetas de los módulos.
struct Paths
{
    std::wstring screenDir; // Turing System Monitor (main.exe, configure.exe)
    std::wstring rgbDir;    // RGB control (RGB Helper.exe, RGB Control.exe)
};

// Aplica las rutas fijadas en el .ini (sección [Rutas], claves Pantalla y Luces) sobre las detectadas.
Paths ReadIniOverrides(const std::wstring& iniText, Paths detected);

// Estado deseado de la pantalla.
enum class ScreenMode
{
    Running,            // debe estar funcionando
    Stopped,            // el usuario la ha detenido
    Configuring,        // el configurador está abierto
    WaitingAfterConfig  // se acaba de cerrar el configurador: "Save and run" puede estar arrancándola
};

constexpr ULONGLONG AfterConfigGraceMs = 8000;

// Nombre del efecto avanzado (línea "name=") del contenido de helper.cfg, o "" si no lo tiene.
std::wstring HelperEffectName(const std::wstring& helperCfgText);

// True si Motherboard Panel debe arrancar la pantalla ahora.
bool ShouldStartScreen(ScreenMode mode, bool running, ULONGLONG msSinceConfigClosed);
