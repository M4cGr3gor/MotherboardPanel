#include "system.h"

#include <cwchar>
#include <vector>

#include <comdef.h>
#include <sddl.h>
#include <taskschd.h>
#include <tlhelp32.h>
#include <userenv.h>

namespace
{
std::wstring RegString(HKEY root, const wchar_t* key, const wchar_t* value)
{
    wchar_t buf[2048];
    DWORD size = sizeof(buf);
    if (RegGetValueW(root, key, value, RRF_RT_REG_SZ, nullptr, buf, &size) != ERROR_SUCCESS)
        return L"";
    return buf;
}

std::wstring WithSlash(std::wstring dir)
{
    if (!dir.empty() && dir.back() != L'\\')
        dir += L'\\';
    return dir;
}

bool FileExists(const std::wstring& path)
{
    DWORD a = GetFileAttributesW(path.c_str());
    return a != INVALID_FILE_ATTRIBUTES && !(a & FILE_ATTRIBUTE_DIRECTORY);
}

// InstallLocation de la entrada de desinstalación cuyo DisplayName empieza por `prefix`.
std::wstring FindInstallLocation(const wchar_t* prefix)
{
    const HKEY roots[] = {HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE};
    const REGSAM views[] = {KEY_WOW64_64KEY, KEY_WOW64_32KEY};
    const wchar_t* base = L"Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall";
    for (HKEY root : roots)
        for (REGSAM view : views)
        {
            HKEY key;
            if (RegOpenKeyExW(root, base, 0, KEY_READ | view, &key) != ERROR_SUCCESS)
                continue;
            wchar_t name[256];
            for (DWORD i = 0;; i++)
            {
                DWORD len = 256;
                if (RegEnumKeyExW(key, i, name, &len, nullptr, nullptr, nullptr, nullptr) != ERROR_SUCCESS)
                    break;
                std::wstring sub = std::wstring(base) + L"\\" + name;
                std::wstring display = RegString(root, sub.c_str(), L"DisplayName");
                if (_wcsnicmp(display.c_str(), prefix, wcslen(prefix)) == 0)
                {
                    std::wstring loc = RegString(root, sub.c_str(), L"InstallLocation");
                    if (!loc.empty())
                    {
                        RegCloseKey(key);
                        return WithSlash(loc);
                    }
                }
            }
            RegCloseKey(key);
        }
    return L"";
}

} // namespace

std::wstring ReadTextFile(const std::wstring& path)
{
    HANDLE f = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr);
    if (f == INVALID_HANDLE_VALUE)
        return L"";
    std::string bytes;
    char buf[4096];
    DWORD read;
    while (ReadFile(f, buf, sizeof(buf), &read, nullptr) && read > 0)
        bytes.append(buf, read);
    CloseHandle(f);
    if (bytes.size() >= 3 && bytes.compare(0, 3, "\xEF\xBB\xBF") == 0)
        bytes.erase(0, 3);
    int n = MultiByteToWideChar(CP_UTF8, 0, bytes.data(), static_cast<int>(bytes.size()), nullptr, 0);
    std::wstring text(n, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, bytes.data(), static_cast<int>(bytes.size()), text.data(), n);
    return text;
}

bool ReadHelperAutostart(const wchar_t* settingsKey)
{
    DWORD value = 1, size = sizeof(value);
    if (RegGetValueW(HKEY_CURRENT_USER, settingsKey, L"HelperAutostart", RRF_RT_REG_DWORD, nullptr, &value, &size) != ERROR_SUCCESS)
        return true;
    return value != 0;
}

std::wstring HelperConfigPath(const std::wstring& rgbDir)
{
    wchar_t local[MAX_PATH];
    DWORD len = GetEnvironmentVariableW(L"LOCALAPPDATA", local, MAX_PATH);
    std::wstring legacy = rgbDir + L"helper.cfg";
    if (len == 0 || len >= MAX_PATH)
        return legacy;
    std::wstring data = std::wstring(local, len) + L"\\RGB Control\\helper.cfg";
    return !FileExists(data) && FileExists(legacy) ? legacy : data;
}

namespace
{
struct FindWindowData
{
    DWORD pid;
    const wchar_t* className; // nullptr: cualquier ventana visible con título
    HWND found;
};

BOOL CALLBACK FindWindowOfProcess(HWND hwnd, LPARAM lParam)
{
    auto* data = reinterpret_cast<FindWindowData*>(lParam);
    DWORD pid = 0;
    GetWindowThreadProcessId(hwnd, &pid);
    if (pid != data->pid)
        return TRUE;
    if (data->className)
    {
        wchar_t cls[256];
        if (GetClassNameW(hwnd, cls, 256) && _wcsicmp(cls, data->className) == 0)
        {
            data->found = hwnd;
            return FALSE;
        }
    }
    else if (IsWindowVisible(hwnd) && GetWindowTextLengthW(hwnd) > 0 && !GetWindow(hwnd, GW_OWNER))
    {
        data->found = hwnd;
        return FALSE;
    }
    return TRUE;
}

HWND WindowOfProcess(DWORD pid, const wchar_t* className)
{
    FindWindowData data{pid, className, nullptr};
    EnumWindows(FindWindowOfProcess, reinterpret_cast<LPARAM>(&data));
    return data.found;
}

// Acceso a una tarea de la carpeta raíz del Programador de tareas.
struct TaskRef
{
    ITaskService* service = nullptr;
    ITaskFolder* folder = nullptr;
    IRegisteredTask* task = nullptr;
    bool comInit = false;
    explicit TaskRef(const wchar_t* name)
    {
        comInit = SUCCEEDED(CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED));
        if (FAILED(CoCreateInstance(CLSID_TaskScheduler, nullptr, CLSCTX_INPROC_SERVER, IID_ITaskService,
                                    reinterpret_cast<void**>(&service))))
            return;
        if (FAILED(service->Connect(_variant_t(), _variant_t(), _variant_t(), _variant_t())))
            return;
        if (FAILED(service->GetFolder(_bstr_t(L"\\"), &folder)))
            return;
        folder->GetTask(_bstr_t(name), &task);
    }
    ~TaskRef()
    {
        if (task)
            task->Release();
        if (folder)
            folder->Release();
        if (service)
            service->Release();
        if (comInit)
            CoUninitialize();
    }
};
} // namespace

Paths PathsFromInstallDir(const std::wstring& installDir)
{
    std::wstring root = WithSlash(installDir);
    Paths p;
    if (FileExists(root + L"Pantalla\\main.exe"))
        p.screenDir = root + L"Pantalla\\";
    if (FileExists(root + L"Luces\\RGB Helper.exe"))
        p.rgbDir = root + L"Luces\\";
    return p;
}

Paths DetectPaths(const std::wstring& iniPath)
{
    Paths p = PathsFromInstallDir(iniPath.substr(0, iniPath.find_last_of(L'\\') + 1));
    if (p.screenDir.empty())
        p.screenDir = FindInstallLocation(L"Turing System Monitor");
    if (p.screenDir.empty())
    {
        wchar_t local[MAX_PATH];
        if (GetEnvironmentVariableW(L"LOCALAPPDATA", local, MAX_PATH))
        {
            std::wstring guess = std::wstring(local) + L"\\Programs\\Turing System Monitor\\";
            if (FileExists(guess + L"main.exe"))
                p.screenDir = guess;
        }
    }

    if (p.rgbDir.empty())
    {
        std::wstring helper = ExeFromCommandLine(
            RegString(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\CurrentVersion\\Run", L"RGBControlHelper"));
        if (!helper.empty() && FileExists(helper))
            p.rgbDir = WithSlash(helper.substr(0, helper.find_last_of(L'\\')));
        else if (FileExists(L"C:\\Program Files\\RGB control\\RGB Helper.exe"))
            p.rgbDir = L"C:\\Program Files\\RGB control\\";
    }

    p = ReadIniOverrides(ReadTextFile(iniPath), p);
    p.screenDir = WithSlash(p.screenDir);
    p.rgbDir = WithSlash(p.rgbDir);
    return p;
}

DWORD FindProcess(const std::wstring& exePath)
{
    HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snap == INVALID_HANDLE_VALUE)
        return 0;
    const wchar_t* base = wcsrchr(exePath.c_str(), L'\\');
    base = base ? base + 1 : exePath.c_str();
    PROCESSENTRY32W pe{sizeof(pe)};
    DWORD result = 0;
    for (BOOL ok = Process32FirstW(snap, &pe); ok && !result; ok = Process32NextW(snap, &pe))
    {
        if (_wcsicmp(pe.szExeFile, base) != 0)
            continue;
        HANDLE h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pe.th32ProcessID);
        if (!h)
            continue;
        wchar_t path[MAX_PATH];
        DWORD len = MAX_PATH;
        if (QueryFullProcessImageNameW(h, 0, path, &len) && _wcsicmp(path, exePath.c_str()) == 0)
            result = pe.th32ProcessID;
        CloseHandle(h);
    }
    CloseHandle(snap);
    return result;
}

HANDLE OpenWatch(DWORD pid)
{
    if (pid == 0)
        return nullptr;
    return OpenProcess(SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
}

DWORD StartElevated(const std::wstring& exe, const std::wstring& dir)
{
    std::wstring cmd = L"\"" + exe + L"\"";
    STARTUPINFOW si{sizeof(si)};
    PROCESS_INFORMATION pi{};
    if (!CreateProcessW(exe.c_str(), cmd.data(), nullptr, nullptr, FALSE, 0, nullptr, dir.c_str(), &si, &pi))
        return 0;
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return pi.dwProcessId;
}

DWORD StartAsDesktopUser(const std::wstring& exe, const std::wstring& dir)
{
    DWORD result = 0;
    DWORD shellPid = 0;
    HWND shell = GetShellWindow();
    if (shell)
        GetWindowThreadProcessId(shell, &shellPid);
    HANDLE shellProcess = shellPid ? OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, shellPid) : nullptr;
    HANDLE shellToken = nullptr, token = nullptr;
    if (shellProcess && OpenProcessToken(shellProcess, TOKEN_DUPLICATE, &shellToken) &&
        DuplicateTokenEx(shellToken, TOKEN_QUERY | TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_ADJUST_DEFAULT |
                                         TOKEN_ADJUST_SESSIONID,
                         nullptr, SecurityImpersonation, TokenPrimary, &token))
    {
        void* env = nullptr;
        CreateEnvironmentBlock(&env, token, FALSE);
        std::wstring cmd = L"\"" + exe + L"\"";
        STARTUPINFOW si{sizeof(si)};
        PROCESS_INFORMATION pi{};
        if (CreateProcessWithTokenW(token, 0, exe.c_str(), cmd.data(), CREATE_UNICODE_ENVIRONMENT, env, dir.c_str(),
                                    &si, &pi))
        {
            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
            result = pi.dwProcessId;
        }
        if (env)
            DestroyEnvironmentBlock(env);
    }
    if (token)
        CloseHandle(token);
    if (shellToken)
        CloseHandle(shellToken);
    if (shellProcess)
        CloseHandle(shellProcess);
    return result ? result : StartElevated(exe, dir);
}

bool CloseScreen(DWORD pid)
{
    HWND hwnd = WindowOfProcess(pid, L"turingEventWndClass");
    return hwnd && PostMessageW(hwnd, WM_CLOSE, 0, 0);
}

bool SignalNamedEvent(const wchar_t* name)
{
    HANDLE ev = OpenEventW(EVENT_MODIFY_STATE, FALSE, name);
    if (!ev)
        return false;
    BOOL ok = SetEvent(ev);
    CloseHandle(ev);
    return ok != FALSE;
}

bool BringToFront(DWORD pid)
{
    HWND hwnd = WindowOfProcess(pid, nullptr);
    if (!hwnd)
        return false;
    if (IsIconic(hwnd))
        ShowWindow(hwnd, SW_RESTORE);
    return SetForegroundWindow(hwnd) != FALSE;
}

std::optional<bool> GetTaskEnabled(const wchar_t* taskName)
{
    TaskRef ref(taskName);
    if (!ref.task)
        return std::nullopt;
    VARIANT_BOOL enabled = VARIANT_FALSE;
    if (FAILED(ref.task->get_Enabled(&enabled)))
        return std::nullopt;
    return enabled != VARIANT_FALSE;
}

bool SetTaskEnabled(const wchar_t* taskName, bool enable)
{
    TaskRef ref(taskName);
    return ref.task && SUCCEEDED(ref.task->put_Enabled(enable ? VARIANT_TRUE : VARIANT_FALSE));
}

bool RunTask(const wchar_t* taskName)
{
    TaskRef ref(taskName);
    if (!ref.task)
        return false;
    IRunningTask* running = nullptr;
    HRESULT hr = ref.task->Run(_variant_t(), &running);
    if (running)
        running->Release();
    return SUCCEEDED(hr);
}

bool IsElevated()
{
    HANDLE token = nullptr;
    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token))
        return false;
    TOKEN_ELEVATION e{};
    DWORD len = 0;
    BOOL ok = GetTokenInformation(token, TokenElevation, &e, sizeof(e), &len);
    CloseHandle(token);
    return ok && e.TokenIsElevated != 0;
}

HANDLE CreatePresenceMutex(const wchar_t* name)
{
    // Igual que los objetos del ayudante de luces: comprobable desde procesos elevados y sin elevar.
    PSECURITY_DESCRIPTOR sd = nullptr;
    SECURITY_ATTRIBUTES sa{sizeof(sa), nullptr, FALSE};
    if (ConvertStringSecurityDescriptorToSecurityDescriptorW(L"D:(A;;GA;;;IU)(A;;GA;;;SY)(A;;GA;;;BA)S:(ML;;NW;;;ME)",
                                                             SDDL_REVISION_1, &sd, nullptr))
        sa.lpSecurityDescriptor = sd;
    HANDLE m = CreateMutexW(&sa, FALSE, name);
    if (sd)
        LocalFree(sd);
    return m;
}
