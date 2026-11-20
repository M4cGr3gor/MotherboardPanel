// Paquetes del modo LED a LED (sin E/S, para poder probarlos).
#pragma once

#include <cstdint>
#include <vector>

#include "config.h"

// Paquetes para los colores 0xRRGGBB de un conector: cabeceras 0x58 (D_LED1), 0x59 (D_LED2), 0x62 (D_LED3),
// hasta 19 LEDs por paquete; [2..3] desplazamiento en bytes, [4] bytes de color, colores desde el byte 5
// en el orden que indica byteOrder (R en (bo>>16)&0xFF, G en (bo>>8)&0xFF, B en bo&0xFF).
std::vector<Packet> BuildFramePackets(int headerIndex, const uint32_t* colors, int count, uint32_t byteOrder);
