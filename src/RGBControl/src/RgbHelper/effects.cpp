#include "effects.h"

#include <algorithm>
#include <cmath>

namespace
{
constexpr double Pi = 3.14159265358979323846;

uint32_t Lerp(uint32_t a, uint32_t b, double f)
{
    uint32_t r = 0;
    for (int s = 0; s < 24; s += 8)
    {
        double ca = (a >> s) & 0xFF, cb = (b >> s) & 0xFF;
        r |= static_cast<uint32_t>(ca + (cb - ca) * f) << s;
    }
    return r;
}

double Wrap(double x)
{
    return x - std::floor(x);
}

uint32_t Hue(double h)
{
    h = Wrap(h);
    auto ch = [](double v) { return static_cast<uint32_t>(std::clamp(v, 0.0, 1.0) * 255); };
    return (ch(std::abs(h * 6 - 3) - 1) << 16) | (ch(2 - std::abs(h * 6 - 2)) << 8) | ch(2 - std::abs(h * 6 - 4));
}
} // namespace

EffectRenderer::EffectRenderer(const HelperConfig& cfg, int ledCount, uint32_t seed)
    : cfg_(cfg), count_(std::max(0, ledCount)), period_(12.0 / std::clamp(cfg.speed, 1, 10)), rng_(seed ? seed : 1)
{
    if (cfg_.colors.empty())
        cfg_.colors = {0xFFFFFF, 0xFFFFFF};
    sparkle_.assign(count_, -1.0);
    sparkleColor_.assign(count_, 0);
    level_.assign(count_, -1.0);
}

bool EffectRenderer::IsAnimated() const
{
    return cfg_.effect != GradientStatic;
}

uint32_t EffectRenderer::NextRandom()
{
    // xorshift32: rápido y determinista para la misma semilla.
    rng_ ^= rng_ << 13;
    rng_ ^= rng_ >> 17;
    rng_ ^= rng_ << 5;
    return rng_;
}

double EffectRenderer::Random01()
{
    return (NextRandom() % 100000) / 100000.0;
}

// Cometa: cabeza en `head` y estela de cfg_.trail LEDs hacia `towardsTail` (+1 o -1), desvaneciéndose al fondo.
void EffectRenderer::DrawComet(uint32_t* out, int head, uint32_t color, int towardsTail)
{
    const int n = count_;
    for (int k = cfg_.trail; k >= 1; k--)
    {
        int idx = ((head + towardsTail * k) % n + n) % n;
        out[idx] = Lerp(cfg_.background, color, 1.0 - double(k) / (cfg_.trail + 1));
    }
}

uint32_t EffectRenderer::Gradient(double position) const
{
    const size_t n = cfg_.colors.size();
    double x = Wrap(position) * n;
    size_t i = static_cast<size_t>(x) % n;
    return Lerp(cfg_.colors[i], cfg_.colors[(i + 1) % n], x - std::floor(x));
}

uint32_t EffectRenderer::Scale(uint32_t color, double factor) const
{
    return Lerp(0, color, factor);
}

void EffectRenderer::Render(double t, uint32_t* out)
{
    if (count_ == 0)
        return;

    const int n = count_;
    const double cycles = t / period_;
    const bool firstFrame = lastTime_ < 0;
    const double dt = firstFrame ? 0 : std::max(0.0, t - lastTime_);
    lastTime_ = t;
    const double dir = cfg_.clockwise ? -1.0 : 1.0; // horario: el patrón avanza hacia índices mayores
    const size_t nColors = cfg_.colors.size();

    switch (cfg_.effect)
    {
    case GradientStatic:
        for (int i = 0; i < n; i++)
            out[i] = Gradient(double(i) / n);
        break;

    case GradientRotate:
        for (int i = 0; i < n; i++)
            out[i] = Gradient(double(i) / n + dir * cycles);
        break;

    case RainbowAdjustable:
        for (int i = 0; i < n; i++)
            out[i] = Hue(double(i) / n + dir * cycles);
        break;

    case CustomCycle:
    {
        double x = cycles;
        size_t k = static_cast<size_t>(std::floor(x)) % nColors;
        uint32_t c = Lerp(cfg_.colors[k], cfg_.colors[(k + 1) % nColors], x - std::floor(x));
        std::fill(out, out + n, c);
        break;
    }

    case BreathingMulti:
    {
        size_t k = static_cast<size_t>(std::floor(cycles)) % nColors;
        double level = (1 - std::cos(2 * Pi * cycles)) / 2;
        std::fill(out, out + n, Lerp(0, cfg_.colors[k], level));
        break;
    }

    case Comet:
    {
        int step = static_cast<int>(std::floor(cycles * n)) % n;
        int head = cfg_.clockwise ? step : (n - step) % n;
        std::fill(out, out + n, cfg_.background);
        DrawComet(out, head, cfg_.colors[0], cfg_.clockwise ? -1 : 1);
        out[head] = cfg_.colors[0];
        break;
    }

    case Twinkle:
    {
        // Cada destello vive `life` segundos (sube y baja). Se mantienen de media density*n/20 encendidos.
        const double life = period_ / 2;
        const double target = cfg_.density * n / 20.0;
        if (firstFrame)
        {
            // Primer fotograma: arranca ya con destellos a medio camino, no desde cero.
            for (int i = 0; i < n; i++)
                if (NextRandom() % 1000 < static_cast<uint32_t>(1000 * target / n))
                {
                    sparkle_[i] = (NextRandom() % 1000) / 1000.0;
                    sparkleColor_[i] = static_cast<int>(NextRandom() % nColors);
                }
        }

        int dark = 0;
        for (int i = 0; i < n; i++)
        {
            if (sparkle_[i] >= 0)
            {
                sparkle_[i] += dt / life;
                if (sparkle_[i] >= 1)
                    sparkle_[i] = -1;
            }
            if (sparkle_[i] < 0)
                dark++;
        }
        double spawnChance = dark > 0 ? (target / life) * dt / dark : 0;
        for (int i = 0; i < n; i++)
        {
            if (sparkle_[i] < 0 && (NextRandom() % 100000) < spawnChance * 100000)
            {
                sparkle_[i] = 0;
                sparkleColor_[i] = static_cast<int>(NextRandom() % nColors);
            }
            double level = sparkle_[i] < 0 ? 0 : 1 - std::abs(2 * sparkle_[i] - 1);
            out[i] = Lerp(cfg_.background, cfg_.colors[sparkleColor_[i]], level);
        }
        break;
    }

    case Fire:
    case Candle:
    {
        // Cada LED persigue un nivel aleatorio; el fuego varía mucho y rápido, la vela poco y suave.
        const bool fire = cfg_.effect == Fire;
        const double lo = fire ? 0.0 : 0.75;
        const double rate = std::min(1.0, dt * cfg_.speed * (fire ? 2.5 : 0.8));
        for (int i = 0; i < n; i++)
        {
            double target = lo + (1 - lo) * Random01();
            level_[i] = level_[i] < 0 ? target : level_[i] + (target - level_[i]) * rate;
            if (fire)
            {
                // De rojo oscuro a naranja y a amarillo: nunca azul y siempre más rojo que verde.
                double h = level_[i];
                out[i] = h < 0.5 ? Lerp(0xA00000, 0xFF5000, h / 0.5) : Lerp(0xFF5000, 0xFFB000, (h - 0.5) / 0.5);
            }
            else
                out[i] = Lerp(0, cfg_.colors[0], level_[i]);
        }
        break;
    }

    case Heartbeat:
    {
        // Dos golpes por ciclo ("pum-pum") y reposo hasta el siguiente.
        double p = Wrap(cycles);
        auto bump = [](double x, double center, double width) {
            double d = std::abs(x - center);
            return d < width ? 0.5 * (1 + std::cos(Pi * d / width)) : 0.0;
        };
        double level = std::max(bump(p, 0.075, 0.075), 0.6 * bump(p, 0.3, 0.075));
        std::fill(out, out + n, Lerp(0, cfg_.colors[0], level));
        break;
    }

    case Aurora:
    {
        // Los colores se desplazan despacio y una ola de brillo recorre el anillo.
        double drift = 0.15 * std::sin(2 * Pi * cycles);
        for (int i = 0; i < n; i++)
        {
            double pos = double(i) / n;
            double wave = 0.55 + 0.45 * std::sin(2 * Pi * (pos + cycles));
            out[i] = Lerp(0, Gradient(pos * 0.5 + cycles / 3 + drift), wave);
        }
        break;
    }

    case Scanner:
    {
        // Va de un extremo al otro y vuelve; un ciclo completo dura T.
        double u = Wrap(cycles);
        double x = (1 - std::abs(2 * u - 1)) * (n - 1);
        int head = static_cast<int>(std::lround(x));
        int towardsTail = u < 0.5 ? -1 : 1;
        std::fill(out, out + n, cfg_.background);
        for (int k = cfg_.trail; k >= 1; k--)
        {
            int idx = head + towardsTail * k;
            if (idx >= 0 && idx < n)
                out[idx] = Lerp(cfg_.background, cfg_.colors[0], 1.0 - double(k) / (cfg_.trail + 1));
        }
        out[head] = cfg_.colors[0];
        break;
    }

    case Chase:
    {
        // LEDs alternos con los colores elegidos que avanzan un LED cada T/n.
        int step = static_cast<int>(std::floor(cycles * n));
        for (int i = 0; i < n; i++)
        {
            int k = cfg_.clockwise ? i - step : i + step;
            if (((k % 2) + 2) % 2 == 0)
                out[i] = cfg_.colors[(((k / 2) % static_cast<int>(nColors)) + nColors) % nColors];
            else
                out[i] = cfg_.background;
        }
        break;
    }

    case DoubleComet:
    {
        int step = static_cast<int>(std::floor(cycles * n)) % n;
        int head1 = cfg_.clockwise ? step : (n - step) % n;
        int head2 = (head1 + n / 2) % n;
        int towardsTail = cfg_.clockwise ? -1 : 1;
        std::fill(out, out + n, cfg_.background);
        DrawComet(out, head1, cfg_.colors[0], towardsTail);
        DrawComet(out, head2, cfg_.colors[1], towardsTail);
        out[head1] = cfg_.colors[0];
        out[head2] = cfg_.colors[1];
        break;
    }

    case Storm:
    {
        // Fondo tenue y relámpagos al azar que iluminan todo y se apagan en unas décimas.
        double rate = cfg_.density * 0.07; // relámpagos por segundo
        flash_ *= std::exp(-dt / 0.12);
        if (flash_ < 0.002)
            flash_ = 0;
        if (!firstFrame && Random01() < rate * dt)
            flash_ = 1.0;
        std::fill(out, out + n, Lerp(cfg_.background, cfg_.colors[0], flash_));
        break;
    }

    default:
        std::fill(out, out + n, 0u);
        break;
    }

    const double bright = cfg_.brightness / 100.0;
    if (bright < 1.0)
        for (int i = 0; i < n; i++)
            out[i] = Scale(out[i], bright);
}
