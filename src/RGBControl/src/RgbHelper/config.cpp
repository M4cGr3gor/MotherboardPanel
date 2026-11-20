#include "config.h"

#include <algorithm>
#include <charconv>

#include <windows.h>

namespace
{
std::string_view Trim(std::string_view s)
{
    while (!s.empty() && (s.back() == '\r' || s.back() == ' ' || s.back() == '\t'))
        s.remove_suffix(1);
    while (!s.empty() && (s.front() == ' ' || s.front() == '\t'))
        s.remove_prefix(1);
    return s;
}

bool ParseInt(std::string_view s, int& out)
{
    auto r = std::from_chars(s.data(), s.data() + s.size(), out);
    return r.ec == std::errc() && r.ptr == s.data() + s.size();
}

bool ParseHex(std::string_view s, uint32_t& out)
{
    auto r = std::from_chars(s.data(), s.data() + s.size(), out, 16);
    return r.ec == std::errc() && r.ptr == s.data() + s.size();
}

// Divide "a,b,c" en trozos.
std::vector<std::string_view> Split(std::string_view s)
{
    std::vector<std::string_view> parts;
    size_t start = 0;
    while (start <= s.size())
    {
        size_t comma = s.find(',', start);
        if (comma == std::string_view::npos)
            comma = s.size();
        parts.push_back(Trim(s.substr(start, comma - start)));
        start = comma + 1;
    }
    return parts;
}

int ClampInt(std::string_view s, int def, int lo, int hi)
{
    int v;
    if (!ParseInt(s, v))
        return def;
    return std::clamp(v, lo, hi);
}

bool ParsePacket(std::string_view hex, Packet& out)
{
    if (hex.size() != 128)
        return false;
    for (size_t i = 0; i < 64; i++)
    {
        uint32_t b;
        if (!ParseHex(hex.substr(i * 2, 2), b))
            return false;
        out[i] = static_cast<uint8_t>(b);
    }
    return true;
}

std::wstring Utf8ToWide(std::string_view s)
{
    if (s.empty())
        return {};
    int n = MultiByteToWideChar(CP_UTF8, 0, s.data(), static_cast<int>(s.size()), nullptr, 0);
    std::wstring w(n, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, s.data(), static_cast<int>(s.size()), w.data(), n);
    return w;
}
} // namespace

bool ParseHelperConfig(std::string_view text, HelperConfig& out)
{
    out = HelperConfig{};
    bool version = false;

    size_t pos = 0;
    while (pos < text.size())
    {
        size_t eol = text.find('\n', pos);
        if (eol == std::string_view::npos)
            eol = text.size();
        std::string_view line = Trim(text.substr(pos, eol - pos));
        pos = eol + 1;

        size_t eq = line.find('=');
        if (eq == std::string_view::npos)
            continue;
        std::string_view key = Trim(line.substr(0, eq));
        std::string_view value = Trim(line.substr(eq + 1));

        if (key == "version")
            version = value == "1";
        else if (key == "name")
            out.name = Utf8ToWide(value);
        else if (key == "effect")
            out.effect = ClampInt(value, 0, 0, 1000);
        else if (key == "colors")
        {
            for (std::string_view c : Split(value))
            {
                if (out.colors.size() == 8)
                    break;
                uint32_t v;
                out.colors.push_back(c.size() == 6 && ParseHex(c, v) ? v : 0xFFFFFF);
            }
        }
        else if (key == "background")
        {
            uint32_t v;
            out.background = value.size() == 6 && ParseHex(value, v) ? v : 0;
        }
        else if (key == "brightness")
            out.brightness = ClampInt(value, 100, 0, 100);
        else if (key == "speed")
            out.speed = ClampInt(value, 5, 1, 10);
        else if (key == "clockwise")
            out.clockwise = value != "0";
        else if (key == "trail")
            out.trail = ClampInt(value, 5, 1, 10);
        else if (key == "density")
            out.density = ClampInt(value, 5, 1, 10);
        else if (key == "leds")
        {
            auto parts = Split(value);
            for (size_t i = 0; i < 3 && i < parts.size(); i++)
                out.leds[i] = ClampInt(parts[i], 0, 0, 256);
        }
        else if (key == "byteorders")
        {
            auto parts = Split(value);
            for (size_t i = 0; i < 3 && i < parts.size(); i++)
            {
                uint32_t v;
                out.byteOrders[i] = ParseHex(parts[i], v) ? v : 0x00010002;
            }
        }
        else if (key == "enter" || key == "restore")
        {
            Packet p;
            if (ParsePacket(value, p))
                (key == "enter" ? out.enter : out.restore).push_back(p);
        }
    }

    for (auto& o : out.byteOrders)
        if (o == 0)
            o = 0x00010002;
    uint32_t fill = out.colors.empty() ? 0xFFFFFF : out.colors.front();
    while (out.colors.size() < 2)
        out.colors.push_back(fill);

    return version && out.effect >= GradientStatic && out.effect <= LastEffect;
}

std::wstring DataFolder()
{
    wchar_t local[MAX_PATH];
    DWORD len = GetEnvironmentVariableW(L"LOCALAPPDATA", local, MAX_PATH);
    if (len == 0 || len >= MAX_PATH)
        return L"";
    return std::wstring(local, len) + L"\\RGB Control\\";
}

std::wstring HelperConfigPath(const std::wstring& dataDir, const std::wstring& exeDir)
{
    std::wstring data = dataDir + L"helper.cfg";
    std::wstring legacy = exeDir + L"helper.cfg";
    if (dataDir.empty() || (GetFileAttributesW(data.c_str()) == INVALID_FILE_ATTRIBUTES &&
                            GetFileAttributesW(legacy.c_str()) != INVALID_FILE_ATTRIBUTES))
        return legacy;
    return data;
}

bool LoadHelperConfig(const std::wstring& path, HelperConfig& out)
{
    // Se abre compartiendo también el borrado, para que RGB Control pueda sustituir el archivo
    // (renombrar encima) aunque el ayudante lo esté leyendo en ese momento.
    HANDLE h = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr,
                           OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE)
    {
        out = HelperConfig{};
        return false;
    }
    std::string text;
    char buffer[4096];
    DWORD read = 0;
    while (ReadFile(h, buffer, sizeof(buffer), &read, nullptr) && read > 0)
        text.append(buffer, read);
    CloseHandle(h);
    if (text.size() >= 3 && text.compare(0, 3, "\xEF\xBB\xBF") == 0)
        text.erase(0, 3);
    return ParseHelperConfig(text, out);
}
