// Cálculo de los fotogramas de los efectos avanzados (sin E/S).
#pragma once

#include <cstdint>
#include <vector>

#include "config.h"

class EffectRenderer
{
public:
    EffectRenderer(const HelperConfig& cfg, int ledCount, uint32_t seed);

    // false solo para Degradado fijo: basta con enviar un fotograma.
    bool IsAnimated() const;

    // Rellena out[0..ledCount) con colores 0xRRGGBB, con el brillo ya aplicado.
    void Render(double seconds, uint32_t* out);

private:
    HelperConfig cfg_;
    int count_;
    double period_;
    uint32_t rng_;
    std::vector<double> sparkle_;   // Destellos: intensidad de cada LED
    std::vector<int> sparkleColor_; // Destellos: índice de color de cada LED
    std::vector<double> level_;     // Fuego y Vela: nivel de cada LED
    double flash_ = 0;              // Tormenta: intensidad del relámpago actual
    double lastTime_ = -1;

    uint32_t NextRandom();
    double Random01();
    void DrawComet(uint32_t* out, int head, uint32_t color, int towardsTail);
    uint32_t Gradient(double position) const;
    uint32_t Scale(uint32_t color, double factor) const;
};
