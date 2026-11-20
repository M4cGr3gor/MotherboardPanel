// Interacción de Motherboard Panel con el sistema: rutas, procesos, señales, ventanas y tarea programada.
#pragma once

#include <optional>
#include <string>

#include <windows.h>

#include "logic.h"

// Módulos instalados junto a Motherboard Panel: <installDir>\Pantalla\main.exe y <installDir>\Luces\RGB Helper.exe.
// Cada ruta queda vacía si su ejecutable no está.
Paths PathsFromInstallDir(const std::wstring& installDir);

// Carpetas de la pantalla y de las luces: primero las subcarpetas junto al .ini (instalación de Motherboard
// Panel), después lo instalado por separado (registro) y, si existe, lo fijado en el .ini.
Paths DetectPaths(const std::wstring& iniPath);

// PID del proceso cuyo ejecutable es exactamente `exePath` (sin distinguir mayúsculas), o 0.
DWORD FindProcess(const std::wstring& exePath);

// Handle para esperar a un proceso y leer su código de salida (nullptr si no se puede abrir).
HANDLE OpenWatch(DWORD pid);

// Arranca un programa con los mismos permisos que Motherboard Panel (elevado). Devuelve el PID o 0.
DWORD StartElevated(const std::wstring& exe, const std::wstring& dir);

// Arranca un programa sin elevar, como el usuario de la sesión (token del Explorador).
// Si no es posible, lo arranca como StartElevated. Devuelve el PID o 0.
DWORD StartAsDesktopUser(const std::wstring& exe, const std::wstring& dir);

// Pide a la pantalla (main.exe con PID `pid`) que se cierre limpiamente: WM_CLOSE a su ventana oculta
// de clase "turingEventWndClass". False si no se encuentra esa ventana.
bool CloseScreen(DWORD pid);

// Activa el evento con nombre `name`. False si no existe.
bool SignalNamedEvent(const wchar_t* name);

// Trae al frente la ventana visible principal del proceso. False si no tiene ninguna.
bool BringToFront(DWORD pid);

// Estado de la tarea programada (vacío si no existe) y cambio de estado.
std::optional<bool> GetTaskEnabled(const wchar_t* taskName);
bool SetTaskEnabled(const wchar_t* taskName, bool enable);

// Ejecuta ahora la tarea programada. False si no existe o no se pudo.
bool RunTask(const wchar_t* taskName);

// Crea el mutex de presencia de Motherboard Panel, comprobable también desde procesos sin elevar.
HANDLE CreatePresenceMutex(const wchar_t* name);

// True si este proceso tiene permisos de administrador (elevado).
bool IsElevated();

// Contenido de un archivo de texto UTF-8 (con o sin BOM), o "" si no se puede leer.
std::wstring ReadTextFile(const std::wstring& path);

// Preferencia "Iniciar RGB Helper con Windows" de RGB Control (HKCU\<settingsKey>\HelperAutostart).
// Si nunca se ha guardado, está activada.
bool ReadHelperAutostart(const wchar_t* settingsKey = L"Software\\RGBControl");

// helper.cfg de las luces: en %LOCALAPPDATA%\RGB Control\ (donde lo escribe RGB Control) o, si allí no está
// pero sí en la carpeta del programa (versiones anteriores), ese.
std::wstring HelperConfigPath(const std::wstring& rgbDir);
