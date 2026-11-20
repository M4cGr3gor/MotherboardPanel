#include "logic.h"

#include <algorithm>
#include <cwctype>

bool IsCrashExitCode(DWORD code)
{
    return code != STILL_ACTIVE && code >= 0xC0000000;
}

RestartBudget::RestartBudget(int max, ULONGLONG windowMs) : max_(max), windowMs_(windowMs) {}

bool RestartBudget::TryConsume(ULONGLONG nowMs)
{
    uses_.erase(std::remove_if(uses_.begin(), uses_.end(), [&](ULONGLONG t) { return nowMs - t >= windowMs_; }),
                uses_.end());
    if (static_cast<int>(uses_.size()) >= max_)
        return false;
    uses_.push_back(nowMs);
    return true;
}

namespace
{
std::wstring Trim(const std::wstring& s)
{
    size_t a = 0, b = s.size();
    while (a < b && std::iswspace(s[a]))
        ++a;
    while (b > a && std::iswspace(s[b - 1]))
        --b;
    return s.substr(a, b - a);
}

bool EqualsNoCase(const std::wstring& a, const wchar_t* b)
{
    return _wcsicmp(a.c_str(), b) == 0;
}
} // namespace

std::wstring ExeFromCommandLine(const std::wstring& cmd)
{
    std::wstring s = Trim(cmd);
    if (s.empty())
        return s;
    if (s[0] == L'"')
    {
        size_t end = s.find(L'"', 1);
        return end == std::wstring::npos ? s.substr(1) : s.substr(1, end - 1);
    }
    size_t exe = s.find(L".exe");
    if (exe != std::wstring::npos)
        return s.substr(0, exe + 4);
    size_t space = s.find(L' ');
    return space == std::wstring::npos ? s : s.substr(0, space);
}

Paths ReadIniOverrides(const std::wstring& iniText, Paths detected)
{
    std::wstring section;
    size_t pos = 0;
    while (pos <= iniText.size())
    {
        size_t eol = iniText.find(L'\n', pos);
        std::wstring line = Trim(iniText.substr(pos, eol == std::wstring::npos ? std::wstring::npos : eol - pos));
        pos = eol == std::wstring::npos ? iniText.size() + 1 : eol + 1;
        if (line.empty() || line[0] == L';' || line[0] == L'#')
            continue;
        if (line.front() == L'[' && line.back() == L']')
        {
            section = Trim(line.substr(1, line.size() - 2));
            continue;
        }
        size_t eq = line.find(L'=');
        if (eq == std::wstring::npos || !EqualsNoCase(section, L"Rutas"))
            continue;
        std::wstring key = Trim(line.substr(0, eq));
        std::wstring value = Trim(line.substr(eq + 1));
        if (value.empty())
            continue;
        if (EqualsNoCase(key, L"Pantalla"))
            detected.screenDir = value;
        else if (EqualsNoCase(key, L"Luces"))
            detected.rgbDir = value;
    }
    return detected;
}

std::wstring HelperEffectName(const std::wstring& helperCfgText)
{
    size_t pos = 0;
    while (pos < helperCfgText.size())
    {
        size_t eol = helperCfgText.find(L'\n', pos);
        std::wstring line = Trim(helperCfgText.substr(pos, eol == std::wstring::npos ? std::wstring::npos : eol - pos));
        pos = eol == std::wstring::npos ? helperCfgText.size() : eol + 1;
        if (line.compare(0, 5, L"name=") == 0)
            return Trim(line.substr(5));
    }
    return L"";
}

bool ShouldStartScreen(ScreenMode mode, bool running, ULONGLONG msSinceConfigClosed)
{
    if (running)
        return false;
    switch (mode)
    {
    case ScreenMode::Running:
        return true;
    case ScreenMode::WaitingAfterConfig:
        return msSinceConfigClosed >= AfterConfigGraceMs;
    default:
        return false;
    }
}
