#include "presence.h"

#include <sddl.h>

namespace
{
constexpr ULONGLONG BootWindowMs = 3 * 60 * 1000; // "recién iniciado Windows"
constexpr ULONGLONG MaxWaitMs = 60 * 1000;        // espera máxima a Motherboard Panel al iniciar Windows
} // namespace

TrayDecision DecideTray(bool hubPresent, ULONGLONG uptimeMs, ULONGLONG sinceStartMs)
{
    if (hubPresent)
        return TrayDecision::Hide;
    if (uptimeMs < BootWindowMs && sinceStartMs < MaxWaitMs)
        return TrayDecision::Wait;
    return TrayDecision::Show;
}

bool IsHubPresent(const wchar_t* name)
{
    HANDLE m = OpenMutexW(SYNCHRONIZE, FALSE, name);
    if (!m)
        return false;
    CloseHandle(m);
    return true;
}

SECURITY_ATTRIBUTES* SharedObjectSecurity()
{
    // Acceso total para usuarios interactivos, SYSTEM y administradores; etiqueta de integridad media
    // sin "escritura hacia arriba", para que un proceso sin elevar pueda usar objetos creados elevado.
    static SECURITY_ATTRIBUTES sa{};
    static bool ready = false;
    if (!ready)
    {
        PSECURITY_DESCRIPTOR sd = nullptr;
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(L"D:(A;;GA;;;IU)(A;;GA;;;SY)(A;;GA;;;BA)S:(ML;;NW;;;ME)",
                                                                  SDDL_REVISION_1, &sd, nullptr))
            return nullptr;
        sa.nLength = sizeof(sa);
        sa.lpSecurityDescriptor = sd; // se conserva toda la vida del proceso
        sa.bInheritHandle = FALSE;
        ready = true;
    }
    return &sa;
}
