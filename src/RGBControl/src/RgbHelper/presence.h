// Convivencia con Motherboard Panel: un programa de bandeja que muestra un único icono para la pantalla
// y las luces. Mientras está en marcha mantiene el mutex con nombre "MotherboardPanel" y el ayudante
// oculta su propio icono. Sin Motherboard Panel, el ayudante funciona y muestra su icono como siempre.
#pragma once

#include <windows.h>

enum class TrayDecision
{
    Show, // mostrar el icono propio
    Hide, // Motherboard Panel está en marcha: sin icono
    Wait  // inicio de Windows: dar tiempo a que arranque Motherboard Panel antes de mostrarlo
};

// hubPresent: existe el mutex de Motherboard Panel. uptimeMs: tiempo desde que se encendió Windows.
// sinceStartMs: tiempo desde que arrancó el ayudante.
TrayDecision DecideTray(bool hubPresent, ULONGLONG uptimeMs, ULONGLONG sinceStartMs);

// True si existe el mutex con nombre `name` (por defecto el de Motherboard Panel).
bool IsHubPresent(const wchar_t* name = L"MotherboardPanel");

// Seguridad para los objetos con nombre del ayudante (mutex y eventos): usables tanto por procesos
// elevados como sin elevar de la sesión, por si alguna vez coinciden distintos niveles de permisos.
SECURITY_ATTRIBUTES* SharedObjectSecurity();
