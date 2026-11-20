// Pruebas de Motherboard Panel. Devuelve el número de fallos.
#include <cstdio>
#include <string>

#include <windows.h>

#include "../logic.h"
#include "../system.h"

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

static void TestCrashExitCodes()
{
    CHECK(!IsCrashExitCode(0));          // cierre normal (p. ej. RGB Control elige un efecto normal)
    CHECK(!IsCrashExitCode(1));          // error de Python: no es una excepción de proceso
    CHECK(IsCrashExitCode(0xC0000005));  // violación de acceso
    CHECK(IsCrashExitCode(0xE0434352));  // excepción .NET no controlada
    CHECK(!IsCrashExitCode(STILL_ACTIVE));
}

static void TestRestartBudget()
{
    const ULONGLONG minute = 60000;
    RestartBudget b; // 3 en 5 minutos
    CHECK(b.TryConsume(0));
    CHECK(b.TryConsume(1 * minute));
    CHECK(b.TryConsume(2 * minute));
    CHECK(!b.TryConsume(3 * minute)); // el 4.º dentro de la ventana
    CHECK(b.TryConsume(5 * minute + 1)); // el primero ya ha salido de la ventana

    RestartBudget small(1, 1000);
    CHECK(small.TryConsume(100));
    CHECK(!small.TryConsume(500));
    CHECK(small.TryConsume(1200));
}

static void TestExeFromCommandLine()
{
    CHECK(ExeFromCommandLine(L"\"C:\\Program Files\\RGB control\\RGB Helper.exe\"") ==
          L"C:\\Program Files\\RGB control\\RGB Helper.exe");
    CHECK(ExeFromCommandLine(L"\"C:\\a b\\x.exe\" -arg 2") == L"C:\\a b\\x.exe");
    CHECK(ExeFromCommandLine(L"C:\\Tools\\x.exe -arg") == L"C:\\Tools\\x.exe");
    CHECK(ExeFromCommandLine(L"  C:\\Tools\\x.exe") == L"C:\\Tools\\x.exe");
    CHECK(ExeFromCommandLine(L"") == L"");
}

static void TestIniOverrides()
{
    Paths detected{L"C:\\Turing", L"C:\\RGB"};
    Paths same = ReadIniOverrides(L"", detected);
    CHECK(same.screenDir == L"C:\\Turing" && same.rgbDir == L"C:\\RGB");

    Paths one = ReadIniOverrides(L"[Rutas]\r\nPantalla = D:\\Otra pantalla\\\r\n", detected);
    CHECK(one.screenDir == L"D:\\Otra pantalla\\");
    CHECK(one.rgbDir == L"C:\\RGB");

    Paths both = ReadIniOverrides(L"; comentario\n[Rutas]\nPantalla=D:\\P\nLuces=D:\\L\n[Otra]\nLuces=X\n", detected);
    CHECK(both.screenDir == L"D:\\P" && both.rgbDir == L"D:\\L");

    Paths empty = ReadIniOverrides(L"[Rutas]\nPantalla=\n", detected); // valor vacío: se ignora
    CHECK(empty.screenDir == L"C:\\Turing");
}

static void TestShouldStartScreen()
{
    CHECK(ShouldStartScreen(ScreenMode::Running, false, 0));
    CHECK(!ShouldStartScreen(ScreenMode::Running, true, 0));
    CHECK(!ShouldStartScreen(ScreenMode::Stopped, false, 0));
    CHECK(!ShouldStartScreen(ScreenMode::Configuring, false, 0));
    // Tras cerrar el configurador: esperar a que "Save and run" la arranque.
    CHECK(!ShouldStartScreen(ScreenMode::WaitingAfterConfig, false, 7999));
    CHECK(ShouldStartScreen(ScreenMode::WaitingAfterConfig, false, 8000));
    CHECK(!ShouldStartScreen(ScreenMode::WaitingAfterConfig, true, 9000));
}

static void TestSystem()
{
    wchar_t self[MAX_PATH];
    GetModuleFileNameW(nullptr, self, MAX_PATH);
    CHECK(FindProcess(self) == GetCurrentProcessId());
    CHECK(FindProcess(L"C:\\no\\existe.exe") == 0);

    // Vigilar un proceso: el handle permite leer su código de salida al terminar.
    HANDLE watch = OpenWatch(GetCurrentProcessId());
    CHECK(watch != nullptr);
    DWORD code = 0;
    CHECK(GetExitCodeProcess(watch, &code) && code == STILL_ACTIVE);
    CloseHandle(watch);
    CHECK(OpenWatch(0) == nullptr);

    // Instalación en una sola carpeta: los módulos están en Pantalla\ y Luces\ junto al .exe del panel.
    wchar_t tmp[MAX_PATH];
    GetTempPathW(MAX_PATH, tmp);
    std::wstring root = std::wstring(tmp) + L"MP.Test.Instalacion\\";
    CreateDirectoryW(root.c_str(), nullptr);
    CreateDirectoryW((root + L"Pantalla").c_str(), nullptr);
    CreateDirectoryW((root + L"Luces").c_str(), nullptr);
    CHECK(PathsFromInstallDir(root).screenDir.empty()); // aún sin ejecutables
    CloseHandle(CreateFileW((root + L"Pantalla\\main.exe").c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, 0, nullptr));
    CloseHandle(CreateFileW((root + L"Luces\\RGB Helper.exe").c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, 0, nullptr));
    Paths inst = PathsFromInstallDir(root);
    CHECK(inst.screenDir == root + L"Pantalla\\");
    CHECK(inst.rgbDir == root + L"Luces\\");
    DeleteFileW((root + L"Pantalla\\main.exe").c_str());
    DeleteFileW((root + L"Luces\\RGB Helper.exe").c_str());
    RemoveDirectoryW((root + L"Pantalla").c_str());
    RemoveDirectoryW((root + L"Luces").c_str());
    RemoveDirectoryW(root.c_str());

    // Señales con nombre.
    CHECK(!SignalNamedEvent(L"MP.Test.NoExiste"));
    HANDLE ev = CreateEventW(nullptr, FALSE, FALSE, L"MP.Test.Evento");
    CHECK(SignalNamedEvent(L"MP.Test.Evento"));
    CHECK(WaitForSingleObject(ev, 0) == WAIT_OBJECT_0);
    CloseHandle(ev);

    // Sin una ventana de la pantalla en ese proceso, no hay nada que cerrar.
    CHECK(!CloseScreen(GetCurrentProcessId()));

    // Tarea programada inexistente: sin valor, y no se puede ejecutar.
    CHECK(!GetTaskEnabled(L"MP.Test.NoExiste").has_value());
    CHECK(!RunTask(L"MP.Test.NoExiste"));

    // El propio proceso de pruebas tiene el mismo nivel que su token.
    HANDLE token = nullptr;
    OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token);
    TOKEN_ELEVATION e{};
    DWORD len = 0;
    GetTokenInformation(token, TokenElevation, &e, sizeof(e), &len);
    CloseHandle(token);
    CHECK(IsElevated() == (e.TokenIsElevated != 0));

    // Valor "name=" de helper.cfg (nombre del efecto avanzado activo).
    CHECK(HelperEffectName(L"version=1\r\nname=Destellos\r\neffect=13\r\n") == L"Destellos");
    CHECK(HelperEffectName(L"version=1\r\neffect=13\r\n") == L"");

    // helper.cfg: en la carpeta de datos del usuario salvo que solo exista junto al programa.
    wchar_t local[MAX_PATH];
    GetEnvironmentVariableW(L"LOCALAPPDATA", local, MAX_PATH);
    CHECK(HelperConfigPath(L"C:\\no\\existe\\") == std::wstring(local) + L"\\RGB Control\\helper.cfg");
    std::wstring legacyDir = std::wstring(tmp) + L"MP.Test.Luces\\";
    CreateDirectoryW(legacyDir.c_str(), nullptr);
    CloseHandle(CreateFileW((legacyDir + L"helper.cfg").c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, 0, nullptr));
    std::wstring dataCfg = std::wstring(local) + L"\\RGB Control\\helper.cfg";
    bool dataExists = GetFileAttributesW(dataCfg.c_str()) != INVALID_FILE_ATTRIBUTES;
    CHECK(HelperConfigPath(legacyDir) == (dataExists ? dataCfg : legacyDir + L"helper.cfg"));
    DeleteFileW((legacyDir + L"helper.cfg").c_str());
    RemoveDirectoryW(legacyDir.c_str());

    // Preferencia de inicio del ayudante: activada si no se ha guardado; desactivada solo con 0.
    const wchar_t* prefKey = L"Software\\MP.Test.RGBControl";
    RegDeleteTreeW(HKEY_CURRENT_USER, prefKey);
    CHECK(ReadHelperAutostart(prefKey));
    DWORD off = 0, on = 1;
    RegSetKeyValueW(HKEY_CURRENT_USER, prefKey, L"HelperAutostart", REG_DWORD, &off, sizeof(off));
    CHECK(!ReadHelperAutostart(prefKey));
    RegSetKeyValueW(HKEY_CURRENT_USER, prefKey, L"HelperAutostart", REG_DWORD, &on, sizeof(on));
    CHECK(ReadHelperAutostart(prefKey));
    RegDeleteTreeW(HKEY_CURRENT_USER, prefKey);

    // Mutex de presencia: un proceso sin elevar debe poder comprobar que existe.
    HANDLE m = CreatePresenceMutex(L"MP.Test.Presencia");
    CHECK(m != nullptr);
    HANDLE again = OpenMutexW(SYNCHRONIZE, FALSE, L"MP.Test.Presencia");
    CHECK(again != nullptr);
    if (again)
        CloseHandle(again);
    CloseHandle(m);
}

int wmain(int argc, wchar_t** argv)
{
    // Prueba manual: tests.exe --rutas  muestra las carpetas detectadas en este equipo.
    if (argc > 1 && _wcsicmp(argv[1], L"--rutas") == 0)
    {
        Paths p = DetectPaths(L"no-existe.ini");
        std::wprintf(L"Pantalla: %ls\nLuces:    %ls\n", p.screenDir.c_str(), p.rgbDir.c_str());
        return 0;
    }
    TestSystem();
    TestCrashExitCodes();
    TestRestartBudget();
    TestExeFromCommandLine();
    TestIniOverrides();
    TestShouldStartScreen();
    if (g_failures == 0)
        std::printf("Todas las pruebas superadas (%d comprobaciones)\n", g_checks);
    else
        std::printf("%d de %d comprobaciones fallidas\n", g_failures, g_checks);
    return g_failures;
}
