#include "hid_device.h"

#include <algorithm>
#include <cwchar>
#include <string>
#include <vector>

#include <hidsdi.h>
#include <setupapi.h>

namespace
{
bool PathMatches(const wchar_t* path)
{
    std::wstring p(path);
    std::transform(p.begin(), p.end(), p.begin(), ::towlower);
    return p.find(L"vid_048d&pid_5711") != std::wstring::npos;
}

bool IsControlCollection(HANDLE h)
{
    PHIDP_PREPARSED_DATA data = nullptr;
    if (!HidD_GetPreparsedData(h, &data))
        return false;
    HIDP_CAPS caps{};
    bool ok = HidP_GetCaps(data, &caps) == HIDP_STATUS_SUCCESS && caps.UsagePage == 0xFF89 && caps.Usage == 0xCC &&
              caps.FeatureReportByteLength == 64;
    HidD_FreePreparsedData(data);
    return ok;
}
} // namespace

bool HidDevice::Open()
{
    Close();
    GUID hidGuid;
    HidD_GetHidGuid(&hidGuid);
    HDEVINFO set = SetupDiGetClassDevsW(&hidGuid, nullptr, nullptr, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
    if (set == INVALID_HANDLE_VALUE)
        return false;

    SP_DEVICE_INTERFACE_DATA iface{sizeof(iface)};
    for (DWORD i = 0; SetupDiEnumDeviceInterfaces(set, nullptr, &hidGuid, i, &iface); i++)
    {
        DWORD required = 0;
        SetupDiGetDeviceInterfaceDetailW(set, &iface, nullptr, 0, &required, nullptr);
        std::vector<BYTE> buffer(required);
        auto detail = reinterpret_cast<SP_DEVICE_INTERFACE_DETAIL_DATA_W*>(buffer.data());
        detail->cbSize = sizeof(SP_DEVICE_INTERFACE_DETAIL_DATA_W);
        if (!SetupDiGetDeviceInterfaceDetailW(set, &iface, detail, required, nullptr, nullptr) || !PathMatches(detail->DevicePath))
            continue;

        HANDLE h = CreateFileW(detail->DevicePath, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr,
                               OPEN_EXISTING, 0, nullptr);
        if (h == INVALID_HANDLE_VALUE)
            continue;
        if (IsControlCollection(h))
        {
            handle_ = h;
            break;
        }
        CloseHandle(h);
    }
    SetupDiDestroyDeviceInfoList(set);
    return IsOpen();
}

void HidDevice::Close()
{
    if (handle_ != INVALID_HANDLE_VALUE)
    {
        CloseHandle(handle_);
        handle_ = INVALID_HANDLE_VALUE;
    }
}

bool HidDevice::Send(const Packet& packet)
{
    if (!IsOpen())
        return false;
    Packet copy = packet; // HidD_SetFeature pide un búfer modificable
    return HidD_SetFeature(handle_, copy.data(), static_cast<ULONG>(copy.size())) != FALSE;
}
