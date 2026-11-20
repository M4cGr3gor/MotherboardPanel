// Motherboard Panel: un único icono en la bandeja para la pantalla USB (Turing System Monitor) y las
// luces RGB (RGB Control). Arranca y vigila los módulos, que ocultan su propio icono mientras existe el
// mutex "MotherboardPanel". La tarea programada "Motherboard Panel" lo arranca elevado al iniciar sesión
// (la pantalla necesita permisos de administrador); las luces se arrancan sin elevar, como siempre.

#include <cstdio>
#include <ctime>
#include <string>

#include <windows.h>
#include <shellapi.h>
#include <shlobj.h>

#include "logic.h"
#include "system.h"

namespace
{
constexpr int IDI_APP = 101;
constexpr UINT WM_TRAY = WM_APP + 1;
constexpr UINT_PTR WatchTimerId = 1;
constexpr UINT WatchMs = 2000;
constexpr DWORD CloseWaitMs = 10000;
const wchar_t* AppName = L"Motherboard Panel";
const wchar_t* TaskName = L"Motherboard Panel";
const wchar_t* MutexName = L"MotherboardPanel";
const wchar_t* HelperStopEvent = L"RGBControl.Stop";

enum MenuId : UINT
{
    ID_SCREEN_CONFIG = 100,
    ID_SCREEN_RESTART,
    ID_SCREEN_TOGGLE,
    ID_RGB_OPEN,
    ID_RGB_TOGGLE,
    ID_AUTOSTART,
    ID_EXIT
};

HINSTANCE g_instance;
HWND g_window;
UINT g_taskbarCreated;
Paths g_paths;

// Pantalla
ScreenMode g_screenMode = ScreenMode::Running;
HANDLE g_screenWatch = nullptr; // proceso main.exe vigilado
DWORD g_configPid = 0;
HANDLE g_configWatch = nullptr; // configure.exe abierto desde el menú
ULONGLONG g_configClosedTick = 0;
RestartBudget g_screenBudget;
bool g_screenGaveUp = false;

// Luces
HANDLE g_rgbWatch = nullptr; // RGB Helper.exe vigilado
RestartBudget g_rgbBudget;
bool g_rgbGaveUp = false;

bool g_endingSession = false; // Windows se apaga o cierra sesión: los módulos se cierran solos

std::wstring Hex(DWORD code)
{
    wchar_t buf[16];
    swprintf_s(buf, L"0x%08X", code);
    return buf;
}

std::wstring ScreenExe() { return g_paths.screenDir + L"main.exe"; }
std::wstring ConfigExe() { return g_paths.screenDir + L"configure.exe"; }
std::wstring HelperExe() { return g_paths.rgbDir + L"RGB Helper.exe"; }
std::wstring ControlExe() { return g_paths.rgbDir + L"RGB Control.exe"; }
std::wstring HelperCfg() { return HelperConfigPath(g_paths.rgbDir); }

bool Exists(const std::wstring& path)
{
    DWORD a = GetFileAttributesW(path.c_str());
    return a != INVALID_FILE_ATTRIBUTES && !(a & FILE_ATTRIBUTE_DIRECTORY);
}

bool ScreenInstalled() { return !g_paths.screenDir.empty() && Exists(ScreenExe()); }
bool RgbInstalled() { return !g_paths.rgbDir.empty() && Exists(HelperExe()); }

// ------------------------------------------------------------------ registro

std::wstring LogPath()
{
    wchar_t* local = nullptr;
    std::wstring dir;
    if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &local)))
        dir = std::wstring(local) + L"\\Motherboard Panel";
    CoTaskMemFree(local);
    CreateDirectoryW(dir.c_str(), nullptr);
    return dir + L"\\registro.log";
}

void Log(const std::wstring& text)
{
    static std::wstring path = LogPath();
    // Se reinicia si crece mucho (más de 1 MB).
    WIN32_FILE_ATTRIBUTE_DATA info;
    if (GetFileAttributesExW(path.c_str(), GetFileExInfoStandard, &info) && info.nFileSizeLow > 1000000)
        DeleteFileW(path.c_str());
    SYSTEMTIME t;
    GetLocalTime(&t);
    wchar_t stamp[64];
    swprintf_s(stamp, L"%02d/%02d/%04d %02d:%02d:%02d ", t.wDay, t.wMonth, t.wYear, t.wHour, t.wMinute, t.wSecond);
    std::wstring line = stamp + text + L"\r\n";
    int n = WideCharToMultiByte(CP_UTF8, 0, line.c_str(), -1, nullptr, 0, nullptr, nullptr);
    std::string utf8(n - 1, '\0');
    WideCharToMultiByte(CP_UTF8, 0, line.c_str(), -1, utf8.data(), n, nullptr, nullptr);
    HANDLE f = CreateFileW(path.c_str(), FILE_APPEND_DATA, FILE_SHARE_READ, nullptr, OPEN_ALWAYS, 0, nullptr);
    if (f == INVALID_HANDLE_VALUE)
        return;
    DWORD written;
    WriteFile(f, utf8.data(), static_cast<DWORD>(utf8.size()), &written, nullptr);
    CloseHandle(f);
}

// ------------------------------------------------------------------ bandeja

std::wstring ScreenStatus()
{
    if (!ScreenInstalled())
        return L"no encontrada";
    if (g_screenMode == ScreenMode::Configuring)
        return L"configurando";
    if (g_screenGaveUp)
        return L"falla al arrancar";
    if (FindProcess(ScreenExe()))
        return L"en marcha";
    return g_screenMode == ScreenMode::Stopped ? L"detenida" : L"arrancando";
}

std::wstring RgbStatus()
{
    if (!RgbInstalled())
        return L"no encontradas";
    if (g_rgbGaveUp)
        return L"el ayudante falla";
    if (!FindProcess(HelperExe()))
        return L"efecto normal";
    std::wstring name = HelperEffectName(ReadTextFile(HelperCfg()));
    return name.empty() ? L"sin efecto avanzado" : name;
}

bool UpdateTray(DWORD message)
{
    NOTIFYICONDATAW nid{sizeof(nid)};
    nid.hWnd = g_window;
    nid.uID = 1;
    nid.uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP;
    nid.uCallbackMessage = WM_TRAY;
    nid.hIcon = static_cast<HICON>(LoadImageW(g_instance, MAKEINTRESOURCEW(IDI_APP), IMAGE_ICON, GetSystemMetrics(SM_CXSMICON),
                                              GetSystemMetrics(SM_CYSMICON), LR_DEFAULTCOLOR));
    std::wstring tip = std::wstring(AppName) + L"\nPantalla: " + ScreenStatus() + L"\nLuces: " + RgbStatus();
    wcsncpy_s(nid.szTip, tip.c_str(), _TRUNCATE);
    bool ok = Shell_NotifyIconW(message, &nid) != FALSE;
    if (nid.hIcon)
        DestroyIcon(nid.hIcon);
    return ok;
}

// ------------------------------------------------------------------ pantalla

void StartScreen(const wchar_t* why)
{
    if (!ScreenInstalled() || FindProcess(ScreenExe()))
        return;
    DWORD pid = StartElevated(ScreenExe(), g_paths.screenDir);
    Log(std::wstring(L"Pantalla: arrancada (") + why + L")" + (pid ? L"" : L" — ERROR al arrancar"));
    if (pid && !g_screenWatch)
        g_screenWatch = OpenWatch(pid);
}

// Cierra la pantalla limpiamente (apaga la pantalla) y espera; si no responde, la termina.
void StopScreen()
{
    DWORD pid = FindProcess(ScreenExe());
    if (!pid)
        return;
    HANDLE h = OpenProcess(SYNCHRONIZE | PROCESS_TERMINATE, FALSE, pid);
    bool asked = CloseScreen(pid);
    if (h && (!asked || WaitForSingleObject(h, CloseWaitMs) != WAIT_OBJECT_0))
    {
        TerminateProcess(h, 1);
        WaitForSingleObject(h, 3000);
        Log(L"Pantalla: no respondió al cierre, se ha terminado");
    }
    else
        Log(L"Pantalla: detenida");
    if (h)
        CloseHandle(h);
    if (g_screenWatch)
    {
        CloseHandle(g_screenWatch);
        g_screenWatch = nullptr;
    }
}

void OpenConfigurator()
{
    if (g_screenMode == ScreenMode::Configuring && g_configWatch && WaitForSingleObject(g_configWatch, 0) == WAIT_TIMEOUT)
    {
        BringToFront(g_configPid);
        return;
    }
    g_screenMode = ScreenMode::Configuring;
    StopScreen();
    g_configPid = StartElevated(ConfigExe(), g_paths.screenDir);
    g_configWatch = OpenWatch(g_configPid);
    if (!g_configWatch)
    {
        Log(L"Configurador: ERROR al abrirlo");
        g_screenMode = ScreenMode::Running; // la vigilancia volverá a arrancar la pantalla
        return;
    }
    Log(L"Configurador: abierto");
}

void WatchScreen()
{
    if (!ScreenInstalled())
        return;
    ULONGLONG now = GetTickCount64();

    if (g_screenMode == ScreenMode::Configuring && g_configWatch && WaitForSingleObject(g_configWatch, 0) == WAIT_OBJECT_0)
    {
        CloseHandle(g_configWatch);
        g_configWatch = nullptr;
        g_configPid = 0;
        g_screenMode = ScreenMode::WaitingAfterConfig;
        g_configClosedTick = now;
        Log(L"Configurador: cerrado");
    }

    // ¿Ha terminado la pantalla vigilada?
    if (g_screenWatch && WaitForSingleObject(g_screenWatch, 0) == WAIT_OBJECT_0)
    {
        DWORD code = 0;
        GetExitCodeProcess(g_screenWatch, &code);
        CloseHandle(g_screenWatch);
        g_screenWatch = nullptr;
        if (g_screenMode == ScreenMode::Running)
            Log(L"Pantalla: se ha cerrado sin pedirlo (código " + Hex(code) + L")");
    }

    DWORD pid = FindProcess(ScreenExe());
    if (pid)
    {
        if (!g_screenWatch)
            g_screenWatch = OpenWatch(pid); // p. ej. la ha arrancado "Save and run" del configurador
        if (g_screenMode == ScreenMode::WaitingAfterConfig)
        {
            g_screenMode = ScreenMode::Running;
            Log(L"Pantalla: arrancada por el configurador");
        }
        return;
    }

    if (g_screenGaveUp || !ShouldStartScreen(g_screenMode, false, now - g_configClosedTick))
        return;
    if (g_screenMode == ScreenMode::WaitingAfterConfig)
    {
        g_screenMode = ScreenMode::Running;
        StartScreen(L"configurador cerrado sin guardar");
        return;
    }
    if (!g_screenBudget.TryConsume(now))
    {
        g_screenGaveUp = true;
        Log(L"Pantalla: demasiados reinicios en 5 minutos, se deja de intentar");
        return;
    }
    StartScreen(L"reinicio automático");
}

// ------------------------------------------------------------------ luces

void StartHelper(const wchar_t* why)
{
    if (!RgbInstalled() || FindProcess(HelperExe()))
        return;
    DWORD pid = StartAsDesktopUser(HelperExe(), g_paths.rgbDir);
    Log(std::wstring(L"Luces: ayudante arrancado (") + why + L")" + (pid ? L"" : L" — ERROR al arrancar"));
}

void StopHelper()
{
    DWORD pid = FindProcess(HelperExe());
    if (!pid)
        return;
    HANDLE h = OpenProcess(SYNCHRONIZE, FALSE, pid);
    SignalNamedEvent(HelperStopEvent); // devuelve las luces a su efecto normal
    if (h)
    {
        WaitForSingleObject(h, 5000);
        CloseHandle(h);
    }
    Log(L"Luces: ayudante detenido");
}

void WatchRgb()
{
    if (!RgbInstalled())
        return;
    if (g_rgbWatch && WaitForSingleObject(g_rgbWatch, 0) == WAIT_OBJECT_0)
    {
        DWORD code = 0;
        GetExitCodeProcess(g_rgbWatch, &code);
        CloseHandle(g_rgbWatch);
        g_rgbWatch = nullptr;
        // Solo tras un fallo: si se cerró normalmente (efecto normal elegido, Detener...) es lo esperado.
        if (IsCrashExitCode(code) && !g_rgbGaveUp)
        {
            Log(L"Luces: el ayudante ha fallado (código " + Hex(code) + L")");
            if (g_rgbBudget.TryConsume(GetTickCount64()))
                StartHelper(L"reinicio tras fallo");
            else
            {
                g_rgbGaveUp = true;
                Log(L"Luces: demasiados reinicios en 5 minutos, se deja de intentar");
            }
        }
    }
    if (!g_rgbWatch)
    {
        DWORD pid = FindProcess(HelperExe());
        if (pid)
            g_rgbWatch = OpenWatch(pid);
    }
}

// ------------------------------------------------------------------ menú

void RunCommand(UINT cmd);

void ShowMenu()
{
    bool screenOk = ScreenInstalled(), rgbOk = RgbInstalled();
    bool screenRunning = screenOk && FindProcess(ScreenExe()) != 0;
    bool helperRunning = rgbOk && FindProcess(HelperExe()) != 0;
    std::optional<bool> autostart = GetTaskEnabled(TaskName);

    HMENU screen = CreatePopupMenu();
    AppendMenuW(screen, MF_STRING | (screenOk ? 0 : MF_GRAYED), ID_SCREEN_CONFIG, L"Configurar...");
    AppendMenuW(screen, MF_STRING | (screenOk ? 0 : MF_GRAYED), ID_SCREEN_RESTART, L"Reiniciar");
    AppendMenuW(screen, MF_STRING | (screenOk ? 0 : MF_GRAYED), ID_SCREEN_TOGGLE,
                screenRunning || g_screenMode == ScreenMode::Configuring ? L"Detener" : L"Iniciar");

    HMENU rgb = CreatePopupMenu();
    AppendMenuW(rgb, MF_STRING | (rgbOk && Exists(ControlExe()) ? 0 : MF_GRAYED), ID_RGB_OPEN, L"Abrir panel de luces...");
    bool canStartHelper = rgbOk && Exists(HelperCfg());
    AppendMenuW(rgb, MF_STRING | (helperRunning || canStartHelper ? 0 : MF_GRAYED), ID_RGB_TOGGLE,
                helperRunning ? L"Detener efecto avanzado" : L"Iniciar efecto avanzado");

    HMENU menu = CreatePopupMenu();
    AppendMenuW(menu, MF_STRING | MF_DISABLED, 0, AppName);
    AppendMenuW(menu, MF_SEPARATOR, 0, nullptr);
    AppendMenuW(menu, MF_POPUP, reinterpret_cast<UINT_PTR>(screen), L"Pantalla USB");
    AppendMenuW(menu, MF_POPUP, reinterpret_cast<UINT_PTR>(rgb), L"Luces RGB");
    AppendMenuW(menu, MF_SEPARATOR, 0, nullptr);
    AppendMenuW(menu, MF_STRING | (autostart.has_value() ? 0 : MF_GRAYED) | (autostart.value_or(false) ? MF_CHECKED : 0),
                ID_AUTOSTART, L"Iniciar con Windows");
    AppendMenuW(menu, MF_SEPARATOR, 0, nullptr);
    AppendMenuW(menu, MF_STRING, ID_EXIT, L"Salir");

    POINT pt;
    GetCursorPos(&pt);
    SetForegroundWindow(g_window); // necesario para que el menú se cierre al hacer clic fuera
    UINT cmd = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_NONOTIFY, pt.x, pt.y, 0, g_window, nullptr);
    PostMessageW(g_window, WM_NULL, 0, 0);
    DestroyMenu(menu); // destruye también los submenús
    if (cmd)
        RunCommand(cmd);
}

// Ejecuta una opción del menú (también llega por WM_COMMAND, lo que permite automatizar las pruebas).
void RunCommand(UINT cmd)
{
    bool screenRunning = ScreenInstalled() && FindProcess(ScreenExe()) != 0;
    bool helperRunning = RgbInstalled() && FindProcess(HelperExe()) != 0;
    std::optional<bool> autostart = GetTaskEnabled(TaskName);
    switch (cmd)
    {
    case ID_SCREEN_CONFIG:
        OpenConfigurator();
        break;
    case ID_SCREEN_RESTART:
        StopScreen();
        g_screenMode = ScreenMode::Running;
        g_screenGaveUp = false;
        StartScreen(L"Reiniciar");
        break;
    case ID_SCREEN_TOGGLE:
        if (screenRunning || g_screenMode == ScreenMode::Configuring)
        {
            g_screenMode = ScreenMode::Stopped;
            StopScreen();
        }
        else
        {
            g_screenMode = ScreenMode::Running;
            g_screenGaveUp = false;
            StartScreen(L"Iniciar");
        }
        break;
    case ID_RGB_OPEN:
        StartAsDesktopUser(ControlExe(), g_paths.rgbDir); // RGB Control solo admite una ventana: la trae al frente
        break;
    case ID_RGB_TOGGLE:
        if (helperRunning)
            StopHelper();
        else
        {
            g_rgbGaveUp = false;
            StartHelper(L"Iniciar");
        }
        break;
    case ID_AUTOSTART:
        if (autostart.has_value())
            SetTaskEnabled(TaskName, !*autostart);
        break;
    case ID_EXIT:
        DestroyWindow(g_window);
        break;
    }
    UpdateTray(NIM_MODIFY);
}

LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    if (msg == g_taskbarCreated && g_taskbarCreated != 0)
    {
        UpdateTray(NIM_ADD); // el Explorador se ha reiniciado
        return 0;
    }
    switch (msg)
    {
    case WM_TIMER:
        if (wParam == WatchTimerId)
        {
            WatchScreen();
            WatchRgb();
            // Si el icono no existe (la barra de tareas aún no estaba lista al arrancar, o el Explorador se
            // ha reiniciado), se vuelve a crear.
            if (!UpdateTray(NIM_MODIFY))
                UpdateTray(NIM_ADD);
        }
        return 0;
    case WM_TRAY:
        if (lParam == WM_RBUTTONUP || lParam == WM_LBUTTONDBLCLK)
            ShowMenu();
        return 0;
    case WM_COMMAND:
        RunCommand(LOWORD(wParam));
        return 0;
    case WM_QUERYENDSESSION:
        return TRUE;
    case WM_ENDSESSION:
        if (wParam)
        {
            g_endingSession = true;
            KillTimer(hwnd, WatchTimerId); // Windows se apaga: los módulos se cierran solos, no reiniciarlos
        }
        return 0;
    case WM_DESTROY:
        KillTimer(hwnd, WatchTimerId);
        g_screenMode = ScreenMode::Stopped;
        if (!g_endingSession)
        {
            StopScreen();
            StopHelper();
        }
        UpdateTray(NIM_DELETE);
        Log(L"Motherboard Panel: cerrado");
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(hwnd, msg, wParam, lParam);
}
} // namespace

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int)
{
    g_instance = instance;

    // Sin permisos de administrador la pantalla no puede leer sus sensores: si existe la tarea
    // programada, se relanza a través de ella (elevado, sin aviso) y esta copia termina.
    if (!IsElevated() && RunTask(TaskName))
        return 0;

    // Una sola copia; el mutex indica además a los módulos que oculten su icono.
    HANDLE mutex = CreatePresenceMutex(MutexName);
    if (!mutex || GetLastError() == ERROR_ALREADY_EXISTS)
        return 0;

    wchar_t path[MAX_PATH];
    DWORD len = GetModuleFileNameW(nullptr, path, MAX_PATH);
    std::wstring dir(path, len);
    dir.erase(dir.find_last_of(L'\\') + 1);
    g_paths = DetectPaths(dir + L"Motherboard Panel.ini");
    Log(L"Motherboard Panel: iniciado" + std::wstring(IsElevated() ? L"" : L" SIN permisos de administrador") +
        L" — pantalla: " + (g_paths.screenDir.empty() ? L"(no encontrada)" : g_paths.screenDir) +
        L" — luces: " + (g_paths.rgbDir.empty() ? L"(no encontradas)" : g_paths.rgbDir));

    WNDCLASSW wc{};
    wc.lpfnWndProc = WndProc;
    wc.hInstance = instance;
    wc.lpszClassName = L"MotherboardPanelWindow";
    RegisterClassW(&wc);
    // Ventana oculta (no de solo mensajes) para recibir mensajes de sesión y TaskbarCreated.
    g_window = CreateWindowExW(0, wc.lpszClassName, AppName, 0, 0, 0, 0, 0, nullptr, nullptr, instance, nullptr);
    g_taskbarCreated = RegisterWindowMessageW(L"TaskbarCreated");
    // El Explorador (sin elevar) envía TaskbarCreated a todos; a una ventana elevada solo le llega si se permite.
    ChangeWindowMessageFilterEx(g_window, g_taskbarCreated, MSGFLT_ALLOW, nullptr);

    StartScreen(L"inicio");
    // Con un efecto avanzado activo, el ayudante también arranca aquí: la entrada Run de Windows procesa
    // los programas de inicio de uno en uno y puede tardar más de un minuto (y queda solo como respaldo).
    if (RgbInstalled() && Exists(HelperCfg()) && ReadHelperAutostart())
        StartHelper(L"inicio");
    WatchRgb();
    UpdateTray(NIM_ADD);
    SetTimer(g_window, WatchTimerId, WatchMs, nullptr);

    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0) > 0)
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }
    CloseHandle(mutex);
    return 0;
}
