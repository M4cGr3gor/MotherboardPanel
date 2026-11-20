// Pruebas del lector de helper.cfg y de los efectos. Devuelve el número de fallos.
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <string>

#include <windows.h>

#include "../config.h"
#include "../effects.h"
#include "../frame.h"
#include "../presence.h"

static int g_failures = 0;
static int g_checks = 0;

#define CHECK(cond)                                                              \
    do                                                                           \
    {                                                                            \
        ++g_checks;                                                              \
        if (!(cond))                                                             \
        {                                                                        \
            ++g_failures;                                                        \
            std::printf("FALLO %s:%d: %s\n", __FILE__, __LINE__, #cond);         \
        }                                                                        \
    } while (0)

static bool Near(uint32_t a, uint32_t b)
{
    for (int s = 0; s < 24; s += 8)
        if (std::abs(int((a >> s) & 0xFF) - int((b >> s) & 0xFF)) > 1)
            return false;
    return true;
}

static std::string Enter48()
{
    std::string hex = "CC48";
    hex.append(124, '0');
    return hex;
}

static HelperConfig Make(int effect, std::vector<uint32_t> colors, int speed = 6)
{
    HelperConfig c;
    c.effect = effect;
    c.colors = std::move(colors);
    c.speed = speed;
    c.leds = {12, 12, 0};
    return c;
}

static void TestConfig()
{
    std::string text = "version=1\nname=Degradado giratorio\neffect=8\ncolors=8000FF,0040FF,00E0FF\n"
                       "background=000000\nbrightness=100\nspeed=5\nclockwise=1\ntrail=5\ndensity=5\n"
                       "leds=12,12,0\nbyteorders=00010002,00010002,00010002\nenter=" + Enter48() + "\n";
    HelperConfig c;
    CHECK(ParseHelperConfig(text, c));
    CHECK(c.effect == 8);
    CHECK(c.colors.size() == 3 && c.colors[0] == 0x8000FF);
    CHECK(c.leds[0] == 12 && c.leds[1] == 12 && c.leds[2] == 0);
    CHECK(c.byteOrders[2] == 0x00010002);
    CHECK(c.enter.size() == 1 && c.enter[0][0] == 0xCC && c.enter[0][1] == 0x48);
    CHECK(c.name == L"Degradado giratorio");

    HelperConfig empty;
    CHECK(!ParseHelperConfig("", empty));

    HelperConfig chip;
    CHECK(!ParseHelperConfig("version=1\neffect=3\n", chip));
    HelperConfig storm;
    CHECK(ParseHelperConfig("version=1\neffect=21\n", storm)); // Tormenta, el último efecto
    HelperConfig unknown;
    CHECK(!ParseHelperConfig("version=1\neffect=22\n", unknown));

    HelperConfig bad;
    CHECK(ParseHelperConfig("version=1\neffect=7\ncolors=ZZZZZZ\nenter=ABC\nleds=999,-3,5\n", bad));
    CHECK(bad.colors.size() == 2 && bad.colors[0] == 0xFFFFFF && bad.colors[1] == 0xFFFFFF);
    CHECK(bad.enter.empty());
    CHECK(bad.leds[0] == 256 && bad.leds[1] == 0 && bad.leds[2] == 5);

    HelperConfig crlf;
    CHECK(ParseHelperConfig("version=1\r\neffect=9\r\ncolors=FF0000,0000FF\r\n", crlf));
    CHECK(crlf.colors.size() == 2 && crlf.colors[1] == 0x0000FF);
}

static void TestEffects()
{
    uint32_t a[12], b[12];

    auto fixed = Make(GradientStatic, {0xFF0000, 0x0000FF});
    EffectRenderer gs(fixed, 12, 1);
    gs.Render(0, a);
    CHECK(a[0] == 0xFF0000);
    CHECK(a[6] == 0x0000FF);
    CHECK(!gs.IsAnimated());

    auto rot = Make(GradientRotate, {0xFF0000, 0x00FF00, 0x0000FF});
    EffectRenderer r0(rot, 12, 1);
    r0.Render(0, a);
    r0.Render(0.5, b); // T = 2 s: un cuarto de vuelta = 3 LEDs
    CHECK(Near(b[3], a[0]));
    rot.clockwise = false;
    EffectRenderer r1(rot, 12, 1);
    r1.Render(0.5, b);
    CHECK(Near(b[9], a[0]));

    auto cyc = Make(CustomCycle, {0xFF0000, 0x0000FF});
    EffectRenderer cc(cyc, 12, 1);
    cc.Render(1.0, a);
    bool allEqual = true;
    for (int i = 0; i < 12; i++)
        allEqual = allEqual && a[i] == a[0];
    CHECK(allEqual);
    CHECK(Near(a[0], 0x7F007F));

    auto dim = Make(GradientStatic, {0xFFFFFF, 0xFFFFFF});
    dim.brightness = 50;
    EffectRenderer dm(dim, 12, 1);
    dm.Render(0, a);
    CHECK(Near(a[0], 0x7F7F7F) && Near(a[11], 0x7F7F7F));

    auto comet = Make(Comet, {0xFF8000, 0xFF8000});
    comet.background = 0x000020;
    EffectRenderer cm(comet, 12, 1);
    cm.Render(1.0, a);
    CHECK(a[6] == 0xFF8000);
    CHECK(a[0] == 0x000020);

    auto tw = Make(Twinkle, {0xFF0000, 0x00FF00});
    EffectRenderer t1(tw, 12, 7), t2(tw, 12, 7);
    for (int f = 0; f <= 99; f++)
    {
        t1.Render(f / 30.0, a);
        t2.Render(f / 30.0, b);
    }
    bool same = true;
    for (int i = 0; i < 12; i++)
        same = same && a[i] == b[i];
    CHECK(same);

    tw.density = 10;
    tw.background = 0;
    EffectRenderer t3(tw, 40, 3);
    uint32_t big[40];
    double lit = 0;
    for (int f = 0; f < 100; f++)
    {
        t3.Render(f / 30.0, big);
        for (int i = 0; i < 40; i++)
            lit += big[i] != 0 ? 1 : 0;
    }
    lit /= 100;
    CHECK(lit >= 10 && lit <= 30);

    EffectRenderer none(fixed, 0, 1);
    none.Render(0, a); // no debe fallar
    CHECK(true);
}

static uint32_t R(uint32_t c) { return (c >> 16) & 0xFF; }
static uint32_t G(uint32_t c) { return (c >> 8) & 0xFF; }
static uint32_t B(uint32_t c) { return c & 0xFF; }

static void TestDecorativeEffects()
{
    uint32_t a[8], b[8];

    // Fuego: colores cálidos (rojo >= verde, sin azul) y cambia con el tiempo.
    auto fire = Make(Fire, {0, 0}, 6);
    EffectRenderer fr(fire, 8, 3);
    bool warm = true, changes = false;
    fr.Render(0, a);
    for (int f = 1; f <= 30; f++)
    {
        fr.Render(f / 30.0, b);
        for (int i = 0; i < 8; i++)
        {
            warm = warm && R(b[i]) >= G(b[i]) && B(b[i]) == 0 && R(b[i]) > 0;
            changes = changes || b[i] != a[i];
        }
    }
    CHECK(warm);
    CHECK(changes);

    // Vela: el color elegido, entre el 70 % y el 100 % de intensidad.
    auto candle = Make(Candle, {0xFF8020, 0xFF8020}, 5);
    EffectRenderer cd(candle, 8, 3);
    bool inRange = true;
    for (int f = 0; f < 60; f++)
    {
        cd.Render(f / 30.0, a);
        for (int i = 0; i < 8; i++)
            inRange = inRange && R(a[i]) >= 0xFF * 7 / 10 && R(a[i]) <= 0xFF && B(a[i]) <= 0x20;
    }
    CHECK(inRange);

    // Latido: con velocidad 6 (T = 2 s) el primer golpe culmina en t = 0,15 s y en t = 1,2 s está en reposo.
    auto beat = Make(Heartbeat, {0xFF0000, 0xFF0000}, 6);
    EffectRenderer hb(beat, 8, 1);
    hb.Render(0.15, a);
    CHECK(Near(a[0], 0xFF0000));
    hb.Render(1.2, a);
    CHECK(a[0] == 0 && a[7] == 0);

    // Aurora: se mueve y nunca se sale de los colores elegidos (aquí, sin azul).
    auto aurora = Make(Aurora, {0xFF0000, 0x00FF00}, 6);
    EffectRenderer au(aurora, 8, 1);
    au.Render(0, a);
    au.Render(0.5, b);
    bool moved = false, noBlue = true;
    for (int i = 0; i < 8; i++)
    {
        moved = moved || a[i] != b[i];
        noBlue = noBlue && B(a[i]) == 0 && B(b[i]) == 0;
    }
    CHECK(moved);
    CHECK(noBlue);

    // Escáner: va y vuelve. Con T = 2 s: t = 0 al principio, t = 1 al final, t = 2 otra vez al principio.
    auto scan = Make(Scanner, {0x00FF00, 0x00FF00}, 6);
    scan.background = 0;
    scan.trail = 1;
    EffectRenderer sc(scan, 8, 1);
    sc.Render(0, a);
    CHECK(a[0] == 0x00FF00 && a[7] == 0);
    sc.Render(1.0, a);
    CHECK(a[7] == 0x00FF00 && a[0] == 0);
    sc.Render(2.0, a);
    CHECK(a[0] == 0x00FF00);

    // Persecución: LEDs alternos encendidos que avanzan un LED por paso (T/n).
    auto chase = Make(Chase, {0xFF0000, 0x0000FF}, 6);
    chase.background = 0;
    EffectRenderer ch(chase, 8, 1);
    ch.Render(0, a);
    CHECK(a[0] == 0xFF0000 && a[1] == 0 && a[2] == 0x0000FF);
    ch.Render(2.0 / 8, a); // un paso en sentido horario
    CHECK(a[0] == 0 && a[1] == 0xFF0000);

    // Doble cometa: dos cabezas enfrentadas con los dos primeros colores.
    auto twin = Make(DoubleComet, {0xFF0000, 0x0000FF}, 6);
    twin.background = 0;
    EffectRenderer dc(twin, 8, 1);
    dc.Render(0, a);
    CHECK(a[0] == 0xFF0000 && a[4] == 0x0000FF);

    // Tormenta: fondo tenue y, con densidad 10, algún relámpago que ilumina todo con el color elegido.
    auto storm = Make(Storm, {0xFFFFFF, 0xFFFFFF}, 5);
    storm.background = 0x000010;
    storm.density = 10;
    EffectRenderer st(storm, 8, 5);
    bool flash = false, calm = false;
    for (int f = 0; f < 300; f++)
    {
        st.Render(f / 30.0, a);
        bool all = true;
        for (int i = 0; i < 8; i++)
            all = all && a[i] == 0xFFFFFF;
        flash = flash || all;
        calm = calm || a[0] == 0x000010;
    }
    CHECK(flash);
    CHECK(calm);
}

static void TestFrame()
{
    uint32_t colors[20];
    for (int i = 0; i < 20; i++)
        colors[i] = 0x112233;
    colors[0] = 0xFF0000;
    colors[19] = 0x0000FF;

    auto packets = BuildFramePackets(0, colors, 20, 0x00010002); // orden del equipo: G, R, B
    CHECK(packets.size() == 2);
    if (packets.size() == 2)
    {
        const Packet& a = packets[0];
        const Packet& b = packets[1];
        CHECK(a[0] == 0xCC && a[1] == 0x58);
        CHECK(a[2] == 0 && a[3] == 0 && a[4] == 57);
        CHECK(a[5] == 0x00 && a[6] == 0xFF && a[7] == 0x00); // rojo: R en la posición 1
        CHECK(b[2] == 57 && b[3] == 0 && b[4] == 3);
        CHECK(b[5] == 0x00 && b[6] == 0x00 && b[7] == 0xFF); // azul: B en la posición 2
    }

    auto third = BuildFramePackets(2, colors, 1, 0x00000102); // orden R, G, B: R en 0, G en 1, B en 2
    CHECK(third.size() == 1 && third[0][1] == 0x62 && third[0][5] == 0xFF && third[0][6] == 0 && third[0][7] == 0);

    CHECK(BuildFramePackets(1, colors, 0, 0x00010002).empty());
}

static void TestLoadFile()
{
    wchar_t dir[MAX_PATH];
    GetTempPathW(MAX_PATH, dir);
    std::wstring path = std::wstring(dir) + L"rgb-helper-test.cfg";
    {
        FILE* f = nullptr;
        _wfopen_s(&f, path.c_str(), L"wb");
        if (f)
        {
            std::fputs("\xEF\xBB\xBFversion=1\neffect=9\ncolors=FF0000,0000FF\n", f);
            std::fclose(f);
        }
    }

    // Mientras el ayudante lee, la ventana debe poder sustituir el archivo (renombrar encima).
    HANDLE other = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr,
                               OPEN_EXISTING, 0, nullptr);
    HelperConfig c;
    CHECK(LoadHelperConfig(path, c));
    CHECK(c.effect == 9 && c.colors.size() == 2);
    CloseHandle(other);

    HelperConfig missing;
    CHECK(!LoadHelperConfig(path + L".no-existe", missing));
    DeleteFileW(path.c_str());
}

// helper.cfg se busca en la carpeta de datos del usuario y, si no está, junto al programa.
static void TestConfigPath()
{
    wchar_t local[MAX_PATH];
    GetEnvironmentVariableW(L"LOCALAPPDATA", local, MAX_PATH);
    CHECK(DataFolder() == std::wstring(local) + L"\\RGB Control\\");

    wchar_t tmp[MAX_PATH];
    GetTempPathW(MAX_PATH, tmp);
    std::wstring data = std::wstring(tmp) + L"rgb-datos-test\\";
    std::wstring exe = std::wstring(tmp) + L"rgb-programa-test\\";
    CreateDirectoryW(data.c_str(), nullptr);
    CreateDirectoryW(exe.c_str(), nullptr);
    auto touch = [](const std::wstring& p) {
        CloseHandle(CreateFileW(p.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, 0, nullptr));
    };

    CHECK(HelperConfigPath(data, exe) == data + L"helper.cfg"); // ninguno: donde lo escribirá RGB Control
    touch(exe + L"helper.cfg");
    CHECK(HelperConfigPath(data, exe) == exe + L"helper.cfg"); // solo el de una versión anterior
    touch(data + L"helper.cfg");
    CHECK(HelperConfigPath(data, exe) == data + L"helper.cfg"); // los dos: manda la carpeta de datos
    CHECK(HelperConfigPath(L"", exe) == exe + L"helper.cfg");

    DeleteFileW((data + L"helper.cfg").c_str());
    DeleteFileW((exe + L"helper.cfg").c_str());
    RemoveDirectoryW(data.c_str());
    RemoveDirectoryW(exe.c_str());
}

// Icono de la bandeja según Motherboard Panel (mutex con nombre "MotherboardPanel").
static void TestPresence()
{
    const ULONGLONG minute = 60000;
    // Con Motherboard Panel en marcha, el icono lo pone él.
    CHECK(DecideTray(true, 10 * minute, 0) == TrayDecision::Hide);
    CHECK(DecideTray(true, 30000, 5000) == TrayDecision::Hide);
    // Arranque normal, lejos del inicio de Windows: icono como siempre.
    CHECK(DecideTray(false, 10 * minute, 0) == TrayDecision::Show);
    // Inicio de Windows (menos de 3 min encendido): esperar hasta 60 s a Motherboard Panel...
    CHECK(DecideTray(false, 30000, 5000) == TrayDecision::Wait);
    CHECK(DecideTray(false, 2 * minute, 59999) == TrayDecision::Wait);
    // ...y después mostrarlo.
    CHECK(DecideTray(false, 30000, 61000) == TrayDecision::Show);

    // Detección por el mutex con nombre.
    const wchar_t* name = L"MP.Test.Presence";
    CHECK(!IsHubPresent(name));
    HANDLE m = CreateMutexW(nullptr, FALSE, name);
    CHECK(m != nullptr);
    CHECK(IsHubPresent(name));
    CloseHandle(m);
    CHECK(!IsHubPresent(name));

    // Los objetos compartidos se crean con la seguridad para elevado + sin elevar.
    SECURITY_ATTRIBUTES* sa = SharedObjectSecurity();
    CHECK(sa != nullptr);
    HANDLE e = CreateEventW(sa, FALSE, FALSE, L"MP.Test.SharedEvent");
    CHECK(e != nullptr);
    if (e)
        CloseHandle(e);
}

int main()
{
    TestPresence();
    TestConfig();
    TestEffects();
    TestFrame();
    TestLoadFile();
    TestConfigPath();
    TestDecorativeEffects();
    if (g_failures == 0)
        std::printf("Todas las pruebas C++ superadas (%d comprobaciones)\n", g_checks);
    else
        std::printf("%d de %d comprobaciones fallidas\n", g_failures, g_checks);
    return g_failures;
}
