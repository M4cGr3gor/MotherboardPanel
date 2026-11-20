// Lectura de helper.cfg, el archivo que escribe RGB Control (C#) con el preset avanzado ya resuelto.
#pragma once

#include <array>
#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

using Packet = std::array<uint8_t, 64>;

struct HelperConfig
{
    std::wstring name;
    int effect = 0;                       // PresetEffect de C#: 7..13
    std::vector<uint32_t> colors;         // 0xRRGGBB, entre 2 y 8
    uint32_t background = 0;
    int brightness = 100;                 // 0..100
    int speed = 5;                        // 1..10
    bool clockwise = true;
    int trail = 5;                        // 1..10
    int density = 5;                      // 1..10
    std::array<int, 3> leds{};            // LEDs de D_LED1, D_LED2, D_LED3 (0..256)
    std::array<uint32_t, 3> byteOrders{}; // orden de bytes de color de cada conector
    std::vector<Packet> enter;            // paquetes para entrar en modo LED a LED
    std::vector<Packet> restore;          // paquetes que reaplican el último preset normal
};

// Efectos (mismos números que PresetEffect en C#).
enum Effect : int
{
    GradientStatic = 7,
    GradientRotate = 8,
    CustomCycle = 9,
    RainbowAdjustable = 10,
    Comet = 11,
    BreathingMulti = 12,
    Twinkle = 13,
    Fire = 14,
    Candle = 15,
    Heartbeat = 16,
    Aurora = 17,
    Scanner = 18,
    Chase = 19,
    DoubleComet = 20,
    Storm = 21,
    LastEffect = Storm,
};

// Devuelve false si falta version=1 o el efecto no está entre 7 y 13. Normaliza rangos.
bool ParseHelperConfig(std::string_view text, HelperConfig& out);
bool LoadHelperConfig(const std::wstring& path, HelperConfig& out);

// Carpeta de datos del usuario donde RGB Control guarda helper.cfg: %LOCALAPPDATA%\RGB Control\ (con barra final).
std::wstring DataFolder();
// helper.cfg de la carpeta de datos; si allí no está pero sí junto al programa (versiones anteriores), ese.
std::wstring HelperConfigPath(const std::wstring& dataDir, const std::wstring& exeDir);
