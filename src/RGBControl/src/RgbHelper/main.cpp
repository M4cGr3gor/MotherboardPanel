// RGB Helper: reproduce en segundo plano los efectos avanzados de RGB Control.
// Lee helper.cfg (lo escribe RGB Control), calcula los fotogramas y los envía al chip a 30 fps.
// Icono en la bandeja con: Abrir RGB Control, Iniciar con Windows y Cerrar.
// Con Motherboard Panel en marcha (mutex "MotherboardPanel"), el ayudante no muestra icono: lo gestiona él.

#include <atomic>
#include <mutex>
#include <cwchar>
#include <memory>
#include <string>
#include <thread>
#include <vector>

#include <windows.h>
#include <shellapi.h>

#include "config.h"
#include "effects.h"
#include "frame.h"
#include "hid_device.h"
#include "presence.h"

namespace
{
constexpr int IDI_APP = 101;
constexpr UINT WM_TRAY = WM_APP + 1;
constexpr UINT WM_TIP = WM_APP + 2;
constexpr UINT_PTR TrayTimerId = 1;
constexpr UINT TrayCheckMs = 2000; // cada cuánto se comprueba si Motherboard Panel está en marcha
constexpr UINT ID_OPEN = 1, ID_AUTOSTART = 2, ID_CLOSE = 3;
constexpr int Fps = 30;
const wchar_t* RunKey = L"Software\\Microsoft\\Windows\\CurrentVersion\\Run";
const wchar_t* RunValue = L"RGBControlHelper";
const wchar_t* SettingsKey = L"Software\\RGBControl";
const wchar_t* PreferenceValue = L"HelperAutostart";

HINSTANCE g_instance;
HWND g_window;
UINT g_taskbarCreated;
std::wstring g_dir; // carpeta del exe, con barra final
std::wstring g_tip = L"RGB Helper"; // lo escribe el hilo de trabajo y lo lee la ventana: protegido por g_tipLock
std::mutex g_tipLock;

bool g_trayVisible = false;   // solo lo usa el hilo de la ventana
ULONGLONG g_startTick = 0;

HANDLE g_exitEvent;   // RGBControl.Exit: la ventana pide cerrar (sin restaurar: ella aplica su preset)
HANDLE g_reloadEvent; // RGBControl.Reload: recargar helper.cfg
HANDLE g_stopEvent;   // RGBControl.Stop: cerrar restaurando las luces (menú Cerrar, fin de sesión o Motherboard Panel)
HANDLE g_wakeEvent;   // interno: reanudación tras suspensión
std::atomic<bool> g_restoreOnExit{true};

// ------------------------------------------------------------------ inicio con Windows

bool AutostartPreference()
{
    DWORD value = 1, size = sizeof(value);
    if (RegGetValueW(HKEY_CURRENT_USER, SettingsKey, PreferenceValue, RRF_RT_REG_DWORD, nullptr, &value, &size) != ERROR_SUCCESS)
        return true;
    return value != 0;
}

void SetAutostart(bool enable)
{
    DWORD value = enable ? 1 : 0;
    RegSetKeyValueW(HKEY_CURRENT_USER, SettingsKey, PreferenceValue, REG_DWORD, &value, sizeof(value));
    if (enable)
    {
        std::wstring cmd = L"\"" + g_dir + L"RGB Helper.exe\"";
        RegSetKeyValueW(HKEY_CURRENT_USER, RunKey, RunValue, REG_SZ, cmd.c_str(), static_cast<DWORD>((cmd.size() + 1) * sizeof(wchar_t)));
    }
    else
        RegDeleteKeyValueW(HKEY_CURRENT_USER, RunKey, RunValue);
}

// ------------------------------------------------------------------ bandeja

void UpdateTray(DWORD message)
{
    NOTIFYICONDATAW nid{sizeof(nid)};
    nid.hWnd = g_window;
    nid.uID = 1;
    nid.uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP;
    nid.uCallbackMessage = WM_TRAY;
    nid.hIcon = static_cast<HICON>(LoadImageW(g_instance, MAKEINTRESOURCEW(IDI_APP), IMAGE_ICON, GetSystemMetrics(SM_CXSMICON),
                                              GetSystemMetrics(SM_CYSMICON), LR_DEFAULTCOLOR));
    {
        std::lock_guard<std::mutex> lock(g_tipLock);
        wcsncpy_s(nid.szTip, g_tip.c_str(), _TRUNCATE);
    }
    Shell_NotifyIconW(message, &nid);
    if (nid.hIcon)
        DestroyIcon(nid.hIcon);
}

// Muestra u oculta el icono según Motherboard Panel (ver presence.h).
void RefreshTray()
{
    ULONGLONG now = GetTickCount64();
    TrayDecision d = DecideTray(IsHubPresent(), now, now - g_startTick);
    if (d == TrayDecision::Show && !g_trayVisible)
    {
        UpdateTray(NIM_ADD);
        g_trayVisible = true;
    }
    else if (d == TrayDecision::Hide && g_trayVisible)
    {
        UpdateTray(NIM_DELETE);
        g_trayVisible = false;
    }
}

void OpenMainWindow()
{
    std::wstring exe = g_dir + L"RGB Control.exe";
    ShellExecuteW(nullptr, L"open", exe.c_str(), nullptr, g_dir.c_str(), SW_SHOWNORMAL);
}

void ShowMenu()
{
    HMENU menu = CreatePopupMenu();
    AppendMenuW(menu, MF_STRING, ID_OPEN, L"Abrir RGB Control");
    AppendMenuW(menu, MF_STRING | (AutostartPreference() ? MF_CHECKED : MF_UNCHECKED), ID_AUTOSTART, L"Iniciar con Windows");
    AppendMenuW(menu, MF_SEPARATOR, 0, nullptr);
    AppendMenuW(menu, MF_STRING, ID_CLOSE, L"Cerrar");
    SetMenuDefaultItem(menu, ID_OPEN, FALSE);

    POINT pt;
    GetCursorPos(&pt);
    SetForegroundWindow(g_window); // necesario para que el menú se cierre al hacer clic fuera
    UINT cmd = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_NONOTIFY, pt.x, pt.y, 0, g_window, nullptr);
    PostMessageW(g_window, WM_NULL, 0, 0);
    DestroyMenu(menu);

    if (cmd == ID_OPEN)
        OpenMainWindow();
    else if (cmd == ID_AUTOSTART)
        SetAutostart(!AutostartPreference());
    else if (cmd == ID_CLOSE)
        SetEvent(g_stopEvent);
}

LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    if (msg == g_taskbarCreated && g_taskbarCreated != 0)
    {
        if (g_trayVisible)
            UpdateTray(NIM_ADD); // el Explorador se ha reiniciado
        return 0;
    }
    switch (msg)
    {
    case WM_TIMER:
        if (wParam == TrayTimerId)
            RefreshTray();
        return 0;
    case WM_TRAY:
        if (lParam == WM_RBUTTONUP)
            ShowMenu();
        else if (lParam == WM_LBUTTONDBLCLK)
            OpenMainWindow();
        return 0;
    case WM_TIP:
        if (g_trayVisible)
            UpdateTray(NIM_MODIFY);
        return 0;
    case WM_POWERBROADCAST:
        if (wParam == PBT_APMRESUMEAUTOMATIC)
            SetEvent(g_wakeEvent);
        return TRUE;
    case WM_QUERYENDSESSION:
        return TRUE;
    case WM_ENDSESSION:
        if (wParam)
        {
            g_restoreOnExit = false; // se apaga Windows: no tocar las luces
            SetEvent(g_stopEvent);
        }
        return 0;
    case WM_DESTROY:
        KillTimer(hwnd, TrayTimerId);
        if (g_trayVisible)
            UpdateTray(NIM_DELETE);
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

// ------------------------------------------------------------------ reproducción

enum class Wake
{
    Timer,
    Reload,
    Stop,
    Resume
};

// Espera al siguiente fotograma (o indefinidamente si timer es null) o a una señal.
Wake WaitFor(HANDLE timer)
{
    HANDLE handles[5] = {g_exitEvent, g_stopEvent, g_reloadEvent, g_wakeEvent, timer};
    DWORD count = timer ? 5 : 4;
    DWORD r = WaitForMultipleObjects(count, handles, FALSE, INFINITE);
    switch (r)
    {
    case WAIT_OBJECT_0:
        // Cierre pedido por RGB Control: aplicará su propio preset, así que no se restaura nada.
        g_restoreOnExit = false;
        return Wake::Stop;
    case WAIT_OBJECT_0 + 1:
        return Wake::Stop;
    case WAIT_OBJECT_0 + 2:
        return Wake::Reload;
    case WAIT_OBJECT_0 + 3:
        return Wake::Resume;
    default:
        return Wake::Timer;
    }
}

// Espera ms milisegundos salvo que llegue una señal.
Wake Pause(DWORD ms)
{
    HANDLE handles[4] = {g_exitEvent, g_stopEvent, g_reloadEvent, g_wakeEvent};
    DWORD r = WaitForMultipleObjects(4, handles, FALSE, ms);
    if (r == WAIT_OBJECT_0)
        g_restoreOnExit = false; // ver WaitFor
    if (r == WAIT_OBJECT_0 || r == WAIT_OBJECT_0 + 1)
        return Wake::Stop;
    if (r == WAIT_OBJECT_0 + 2)
        return Wake::Reload;
    return r == WAIT_OBJECT_0 + 3 ? Wake::Resume : Wake::Timer;
}

bool SendAll(HidDevice& dev, const std::vector<Packet>& packets)
{
    for (const Packet& p : packets)
    {
        if (!dev.Send(p))
            return false;
        Sleep(30); // el chip necesita tiempo entre órdenes de configuración
    }
    return true;
}

void SetTip(const std::wstring& tip)
{
    {
        std::lock_guard<std::mutex> lock(g_tipLock);
        g_tip = tip;
    }
    PostMessageW(g_window, WM_TIP, 0, 0);
}

void Worker()
{
    HidDevice dev;
    HelperConfig cfg;
    bool valid = false;
    bool reload = true;

    // Temporizador de alta resolución: 30 fps sin subir la resolución del reloj de todo el sistema.
    HANDLE timer = CreateWaitableTimerExW(nullptr, nullptr, CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, TIMER_ALL_ACCESS);
    if (!timer)
        timer = CreateWaitableTimerW(nullptr, FALSE, nullptr);

    for (;;)
    {
        if (reload)
        {
            reload = false;
            valid = LoadHelperConfig(HelperConfigPath(DataFolder(), g_dir), cfg);
            SetTip(valid ? L"RGB Helper – " + cfg.name : L"RGB Helper – sin efecto activo");
            dev.Close();
        }
        if (!valid)
        {
            Wake w = WaitFor(nullptr);
            if (w == Wake::Stop)
                break;
            reload = w == Wake::Reload;
            continue;
        }

        // Abrir el dispositivo y entrar en modo LED a LED; si falla, reintentar cada 3 s.
        if (!dev.IsOpen())
        {
            if (!dev.Open() || !SendAll(dev, cfg.enter))
            {
                dev.Close();
                Wake w = Pause(3000);
                if (w == Wake::Stop)
                    break;
                reload = w == Wake::Reload;
                continue;
            }
        }

        std::vector<std::unique_ptr<EffectRenderer>> renderers;
        for (int h = 0; h < 3; h++)
            renderers.push_back(std::make_unique<EffectRenderer>(cfg, cfg.leds[h], static_cast<uint32_t>(h + 1)));
        const bool animated = renderers[0]->IsAnimated();

        LARGE_INTEGER freq, start, now;
        QueryPerformanceFrequency(&freq);
        QueryPerformanceCounter(&start);
        LARGE_INTEGER due{};
        due.QuadPart = -1; // primer disparo inmediato
        SetWaitableTimer(timer, &due, 1000 / Fps, nullptr, nullptr, FALSE);

        std::vector<uint32_t> frame(256);
        bool stop = false, failed = false;
        for (;;)
        {
            Wake w = WaitFor(animated ? timer : nullptr);
            if (w == Wake::Stop)
            {
                stop = true;
                break;
            }
            if (w == Wake::Reload)
            {
                reload = true;
                break;
            }
            if (w == Wake::Resume)
            {
                failed = true; // tras suspender, reabrir y volver a entrar en modo directo
                break;
            }

            QueryPerformanceCounter(&now);
            double t = double(now.QuadPart - start.QuadPart) / freq.QuadPart;
            for (int h = 0; h < 3 && !failed; h++)
            {
                if (cfg.leds[h] == 0)
                    continue;
                renderers[h]->Render(t, frame.data());
                for (const Packet& p : BuildFramePackets(h, frame.data(), cfg.leds[h], cfg.byteOrders[h]))
                    if (!dev.Send(p))
                    {
                        failed = true;
                        break;
                    }
            }
            if (failed)
                break;
            if (!animated)
            {
                // Degradado fijo: un solo fotograma; desde aquí solo se esperan señales.
                Wake idle = WaitFor(nullptr);
                if (idle == Wake::Stop)
                    stop = true;
                else if (idle == Wake::Reload)
                    reload = true;
                else
                    failed = true;
                break;
            }
        }
        CancelWaitableTimer(timer);
        if (stop)
            break;
        if (failed)
            dev.Close();
    }

    // Al cerrar, devolver las luces al último efecto normal (sin grabar la flash).
    if (g_restoreOnExit && valid && !cfg.restore.empty())
    {
        if (dev.IsOpen() || dev.Open())
            SendAll(dev, cfg.restore);
    }
    dev.Close();
    if (timer)
        CloseHandle(timer);
    PostMessageW(g_window, WM_CLOSE, 0, 0);
}
} // namespace

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int)
{
    g_instance = instance;

    // Una sola copia a la vez.
    SECURITY_ATTRIBUTES* shared = SharedObjectSecurity();
    HANDLE mutex = CreateMutexW(shared, TRUE, L"RGBControl.Helper");
    if (!mutex || GetLastError() == ERROR_ALREADY_EXISTS)
        return 0;
    g_startTick = GetTickCount64();

    wchar_t path[MAX_PATH];
    DWORD len = GetModuleFileNameW(nullptr, path, MAX_PATH);
    g_dir.assign(path, len);
    g_dir.erase(g_dir.find_last_of(L'\\') + 1);

    g_exitEvent = CreateEventW(shared, FALSE, FALSE, L"RGBControl.Exit");
    g_reloadEvent = CreateEventW(shared, FALSE, FALSE, L"RGBControl.Reload");
    g_stopEvent = CreateEventW(shared, TRUE, FALSE, L"RGBControl.Stop");
    ResetEvent(g_stopEvent); // por si quedó activado de una petición anterior a este arranque
    g_wakeEvent = CreateEventW(nullptr, FALSE, FALSE, nullptr);

    WNDCLASSW wc{};
    wc.lpfnWndProc = WndProc;
    wc.hInstance = instance;
    wc.lpszClassName = L"RGBControlHelperWindow";
    RegisterClassW(&wc);
    // Ventana oculta (no de solo mensajes) para recibir mensajes de energía, sesión y TaskbarCreated.
    g_window = CreateWindowExW(0, wc.lpszClassName, L"RGB Helper", 0, 0, 0, 0, 0, nullptr, nullptr, instance, nullptr);
    g_taskbarCreated = RegisterWindowMessageW(L"TaskbarCreated");
    RefreshTray();
    SetTimer(g_window, TrayTimerId, TrayCheckMs, nullptr);

    std::thread worker(Worker);

    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0) > 0)
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    SetEvent(g_stopEvent);
    worker.join();
    CloseHandle(mutex);
    return 0;
}
