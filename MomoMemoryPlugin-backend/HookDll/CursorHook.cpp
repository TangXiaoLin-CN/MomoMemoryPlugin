// CursorHook.cpp - GetCursorPos Hook DLL
// Uses shared memory for IPC, auto-installs hook on DllMain

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <stdio.h>

// ==================== Debug Log ====================

void DebugLog(const char* format, ...) {
    char path[MAX_PATH];
    GetTempPathA(MAX_PATH, path);
    strcat_s(path, "CursorHook_debug.log");

    FILE* f = NULL;
    fopen_s(&f, path, "a");
    if (f) {
        va_list args;
        va_start(args, format);
        vfprintf(f, format, args);
        va_end(args);
        fprintf(f, "\n");
        fclose(f);
    }
}

// ==================== Shared Memory Structure ====================

#pragma pack(push, 1)
struct SharedData {
    volatile LONG enabled;      // Enable flag
    volatile LONG fakeX;        // Fake X coordinate
    volatile LONG fakeY;        // Fake Y coordinate
    volatile LONG initialized;  // Initialization flag
};
#pragma pack(pop)

// ==================== Global Variables ====================

typedef BOOL(WINAPI* GetCursorPos_t)(LPPOINT lpPoint);

GetCursorPos_t g_OriginalGetCursorPos = NULL;
BYTE g_OriginalBytes[32] = { 0 };
LPVOID g_GetCursorPosAddr = NULL;
BOOL g_Hooked = FALSE;

// Shared memory
HANDLE g_hMapFile = NULL;
SharedData* g_pSharedData = NULL;
const wchar_t* SHARED_MEM_NAME = L"Local\\MomoCursorHookSharedMem";

// ==================== Shared Memory Functions ====================

BOOL OpenSharedMemory() {
    DebugLog("OpenSharedMemory: Starting...");

    g_hMapFile = OpenFileMappingW(FILE_MAP_ALL_ACCESS, FALSE, SHARED_MEM_NAME);
    DebugLog("OpenSharedMemory: OpenFileMapping result = %p, LastError = %d", g_hMapFile, GetLastError());

    if (!g_hMapFile) {
        g_hMapFile = CreateFileMappingW(
            INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE, 0, sizeof(SharedData), SHARED_MEM_NAME);
        DebugLog("OpenSharedMemory: CreateFileMapping result = %p, LastError = %d", g_hMapFile, GetLastError());
    }

    if (!g_hMapFile) {
        DebugLog("OpenSharedMemory: Failed to get file mapping handle");
        return FALSE;
    }

    g_pSharedData = (SharedData*)MapViewOfFile(g_hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(SharedData));
    DebugLog("OpenSharedMemory: MapViewOfFile result = %p, LastError = %d", g_pSharedData, GetLastError());

    if (!g_pSharedData) {
        DebugLog("OpenSharedMemory: Failed to map view");
        return FALSE;
    }

    DebugLog("OpenSharedMemory: Success! enabled=%d, fakeX=%d, fakeY=%d, initialized=%d",
        g_pSharedData->enabled, g_pSharedData->fakeX, g_pSharedData->fakeY, g_pSharedData->initialized);

    return TRUE;
}

void CloseSharedMemory() {
    if (g_pSharedData) {
        UnmapViewOfFile(g_pSharedData);
        g_pSharedData = NULL;
    }
    if (g_hMapFile) {
        CloseHandle(g_hMapFile);
        g_hMapFile = NULL;
    }
}

// ==================== Hook Function ====================

BOOL WINAPI HookedGetCursorPos(LPPOINT lpPoint) {
    if (g_pSharedData && g_pSharedData->enabled && lpPoint) {
        lpPoint->x = g_pSharedData->fakeX;
        lpPoint->y = g_pSharedData->fakeY;
        return TRUE;
    }

    if (g_OriginalGetCursorPos) {
        return g_OriginalGetCursorPos(lpPoint);
    }

    return FALSE;
}

// ==================== Install/Uninstall Hook ====================

BOOL InstallHook() {
    DebugLog("InstallHook: Starting...");

    if (g_Hooked) {
        DebugLog("InstallHook: Already hooked");
        return TRUE;
    }

    HMODULE hUser32 = GetModuleHandleW(L"user32.dll");
    DebugLog("InstallHook: GetModuleHandle(user32) = %p", hUser32);

    if (!hUser32) {
        hUser32 = LoadLibraryW(L"user32.dll");
        DebugLog("InstallHook: LoadLibrary(user32) = %p", hUser32);
    }
    if (!hUser32) {
        DebugLog("InstallHook: Failed to get user32.dll");
        return FALSE;
    }

    g_GetCursorPosAddr = (LPVOID)GetProcAddress(hUser32, "GetCursorPos");
    DebugLog("InstallHook: GetCursorPos address = %p", g_GetCursorPosAddr);

    if (!g_GetCursorPosAddr) {
        DebugLog("InstallHook: Failed to get GetCursorPos address");
        return FALSE;
    }

    // Read original bytes
    DWORD oldProtect;
    VirtualProtect(g_GetCursorPosAddr, 32, PAGE_EXECUTE_READWRITE, &oldProtect);
    memcpy(g_OriginalBytes, g_GetCursorPosAddr, 32);

    DebugLog("InstallHook: Original bytes: %02X %02X %02X %02X %02X %02X %02X %02X %02X %02X %02X %02X %02X %02X %02X %02X",
        g_OriginalBytes[0], g_OriginalBytes[1], g_OriginalBytes[2], g_OriginalBytes[3],
        g_OriginalBytes[4], g_OriginalBytes[5], g_OriginalBytes[6], g_OriginalBytes[7],
        g_OriginalBytes[8], g_OriginalBytes[9], g_OriginalBytes[10], g_OriginalBytes[11],
        g_OriginalBytes[12], g_OriginalBytes[13], g_OriginalBytes[14], g_OriginalBytes[15]);

    // Check if function starts with: BA xx xx xx xx 48 FF 25 (mov edx, imm32; jmp [rip+disp32])
    // This is the Windows 10/11 stub pattern for user32 -> win32u forwarding
    if (g_OriginalBytes[0] == 0xBA &&           // mov edx, imm32
        g_OriginalBytes[5] == 0x48 &&           // REX.W
        g_OriginalBytes[6] == 0xFF &&           // JMP
        g_OriginalBytes[7] == 0x25) {           // [rip+disp32]

        DebugLog("InstallHook: Detected Windows stub pattern (mov edx + jmp [rip+disp])");

        // Calculate the actual target of the indirect jump
        // jmp [rip+disp32] at offset 5
        // After this instruction, RIP = g_GetCursorPosAddr + 5 + 7 = g_GetCursorPosAddr + 12
        // Target address = [RIP + disp32]
        INT32 disp32 = *(INT32*)(g_OriginalBytes + 8);
        UINT64 ripAfterJmp = (UINT64)g_GetCursorPosAddr + 12;
        UINT64 targetPtrAddr = ripAfterJmp + disp32;
        UINT64 actualTarget = *(UINT64*)targetPtrAddr;

        DebugLog("InstallHook: disp32 = %08X, ripAfterJmp = %p, targetPtrAddr = %p, actualTarget = %p",
            disp32, (void*)ripAfterJmp, (void*)targetPtrAddr, (void*)actualTarget);

        // The actualTarget is where the real function is
        // We'll create a trampoline that:
        // 1. mov edx, <original value>
        // 2. jmp <actualTarget> (using absolute 64-bit jump)

        BYTE* trampoline = (BYTE*)VirtualAlloc(NULL, 64, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
        if (!trampoline) {
            DebugLog("InstallHook: Failed to allocate trampoline");
            VirtualProtect(g_GetCursorPosAddr, 32, oldProtect, &oldProtect);
            return FALSE;
        }
        DebugLog("InstallHook: Trampoline address = %p", trampoline);

        // Copy: BA xx xx xx xx (mov edx, imm32) - 5 bytes
        memcpy(trampoline, g_OriginalBytes, 5);

        // Add absolute jump to actual target
        // FF 25 00 00 00 00 [8-byte address]
        trampoline[5] = 0xFF;
        trampoline[6] = 0x25;
        trampoline[7] = 0x00;
        trampoline[8] = 0x00;
        trampoline[9] = 0x00;
        trampoline[10] = 0x00;
        *(UINT64*)(trampoline + 11) = actualTarget;

        g_OriginalGetCursorPos = (GetCursorPos_t)trampoline;
        DebugLog("InstallHook: Trampoline created with absolute jump to %p", (void*)actualTarget);

    } else {
        // Generic case: just call the original function directly
        // This won't work for all cases but is a fallback
        DebugLog("InstallHook: Unknown pattern, using direct call fallback");

        // For unknown patterns, we can't safely create a trampoline
        // Instead, we'll use a different approach: save the original function pointer
        // and only return fake values when enabled (never call original if pattern unknown)
        // This is safer but means we can't get real cursor pos when disabled

        // Actually, let's try a simple trampoline for other patterns
        BYTE* trampoline = (BYTE*)VirtualAlloc(NULL, 64, MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
        if (!trampoline) {
            DebugLog("InstallHook: Failed to allocate trampoline");
            VirtualProtect(g_GetCursorPosAddr, 32, oldProtect, &oldProtect);
            return FALSE;
        }

        // Copy first 14 bytes and add jump back
        // WARNING: This may crash if there are RIP-relative instructions!
        memcpy(trampoline, g_OriginalBytes, 14);
        trampoline[14] = 0xFF;
        trampoline[15] = 0x25;
        trampoline[16] = 0x00;
        trampoline[17] = 0x00;
        trampoline[18] = 0x00;
        trampoline[19] = 0x00;
        *(UINT64*)(trampoline + 20) = (UINT64)((BYTE*)g_GetCursorPosAddr + 14);

        g_OriginalGetCursorPos = (GetCursorPos_t)trampoline;
        DebugLog("InstallHook: Generic trampoline created (may be unstable)");
    }

    // Write jump to our hook function at the original location
    // FF 25 00 00 00 00 [8-byte address]
    BYTE jumpCode[14] = {
        0xFF, 0x25, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    };
    *(UINT64*)(jumpCode + 6) = (UINT64)HookedGetCursorPos;

    memcpy(g_GetCursorPosAddr, jumpCode, 14);
    VirtualProtect(g_GetCursorPosAddr, 32, oldProtect, &oldProtect);

    DebugLog("InstallHook: Hook installed, HookedGetCursorPos = %p", HookedGetCursorPos);

    g_Hooked = TRUE;

    if (g_pSharedData) {
        InterlockedExchange(&g_pSharedData->initialized, 1);
        DebugLog("InstallHook: Marked initialized = 1");
    }

    DebugLog("InstallHook: Success!");
    return TRUE;
}

BOOL UninstallHook() {
    DebugLog("UninstallHook: Starting...");

    if (!g_Hooked || !g_GetCursorPosAddr) {
        DebugLog("UninstallHook: Not hooked or no address");
        return FALSE;
    }

    DWORD oldProtect;
    VirtualProtect(g_GetCursorPosAddr, 32, PAGE_EXECUTE_READWRITE, &oldProtect);
    memcpy(g_GetCursorPosAddr, g_OriginalBytes, 14);
    VirtualProtect(g_GetCursorPosAddr, 32, oldProtect, &oldProtect);

    g_Hooked = FALSE;
    DebugLog("UninstallHook: Success!");
    return TRUE;
}

// ==================== DLL Entry Point ====================

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID lpReserved) {
    switch (reason) {
        case DLL_PROCESS_ATTACH:
            DebugLog("=== DllMain: DLL_PROCESS_ATTACH ===");
            DisableThreadLibraryCalls(hModule);
            if (OpenSharedMemory()) {
                DebugLog("DllMain: Shared memory opened, installing hook...");
                InstallHook();
            } else {
                DebugLog("DllMain: Failed to open shared memory!");
            }
            DebugLog("DllMain: DLL_PROCESS_ATTACH complete");
            return TRUE;

        case DLL_PROCESS_DETACH:
            DebugLog("=== DllMain: DLL_PROCESS_DETACH ===");
            UninstallHook();
            CloseSharedMemory();
            break;
    }
    return TRUE;
}
