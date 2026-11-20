// Acceso al controlador RGB de la placa (ITE IT5711, 048D:5711) por HID.
#pragma once

#include <cstdint>

#include <windows.h>

#include "config.h"

class HidDevice
{
public:
    HidDevice() = default;
    ~HidDevice() { Close(); }
    HidDevice(const HidDevice&) = delete;
    HidDevice& operator=(const HidDevice&) = delete;

    // Busca la colección de control (UsagePage 0xFF89, Usage 0xCC) y la abre.
    bool Open();
    void Close();
    bool IsOpen() const { return handle_ != INVALID_HANDLE_VALUE; }

    // Envía un feature report de 64 bytes (el primer byte es el ID 0xCC).
    bool Send(const Packet& packet);

private:
    HANDLE handle_ = INVALID_HANDLE_VALUE;
};
