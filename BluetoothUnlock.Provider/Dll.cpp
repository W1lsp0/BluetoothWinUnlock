#include "ClassFactory.h"
#include "Guid.h"
#include <new>
#include <strsafe.h>

namespace
{
    HINSTANCE g_instance = nullptr;
    long g_refCount = 0;

    constexpr wchar_t kProviderName[] = L"Bluetooth Unlock";
    constexpr wchar_t kCredentialProviderRegPath[] =
        L"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Authentication\\Credential Providers\\{8AA16CC0-1E39-473D-B8C7-8C3F7A4D6D62}";

    HRESULT GuidToString(REFGUID guid, wchar_t *buffer, DWORD cchBuffer)
    {
        return StringFromGUID2(guid, buffer, static_cast<int>(cchBuffer)) > 0 ? S_OK : E_FAIL;
    }

    HRESULT SetRegistryString(HKEY root, PCWSTR path, PCWSTR name, PCWSTR value)
    {
        HKEY key = nullptr;
        LONG status = RegCreateKeyExW(root, path, 0, nullptr, 0, KEY_WRITE, nullptr, &key, nullptr);
        if (status != ERROR_SUCCESS)
        {
            return HRESULT_FROM_WIN32(status);
        }

        status = RegSetValueExW(
            key,
            name,
            0,
            REG_SZ,
            reinterpret_cast<const BYTE *>(value),
            static_cast<DWORD>((wcslen(value) + 1) * sizeof(wchar_t)));
        RegCloseKey(key);
        return HRESULT_FROM_WIN32(status);
    }

    HRESULT DeleteRegistryTree(HKEY root, PCWSTR path)
    {
        LONG status = RegDeleteTreeW(root, path);
        return status == ERROR_FILE_NOT_FOUND ? S_OK : HRESULT_FROM_WIN32(status);
    }
}

void DllAddRef()
{
    InterlockedIncrement(&g_refCount);
}

void DllRelease()
{
    InterlockedDecrement(&g_refCount);
}

HINSTANCE DllInstance()
{
    return g_instance;
}

BOOL APIENTRY DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_instance = instance;
        DisableThreadLibraryCalls(instance);
    }

    return TRUE;
}

STDAPI DllCanUnloadNow()
{
    return g_refCount == 0 ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(REFCLSID clsid, REFIID riid, void **ppv)
{
    *ppv = nullptr;
    if (clsid != CLSID_BluetoothUnlockProvider)
    {
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    auto factory = new (std::nothrow) ClassFactory();
    if (!factory)
    {
        return E_OUTOFMEMORY;
    }

    HRESULT hr = factory->QueryInterface(riid, ppv);
    factory->Release();
    return hr;
}

STDAPI DllRegisterServer()
{
    wchar_t modulePath[MAX_PATH] = {};
    if (!GetModuleFileNameW(g_instance, modulePath, ARRAYSIZE(modulePath)))
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    wchar_t clsidText[64] = {};
    HRESULT hr = GuidToString(CLSID_BluetoothUnlockProvider, clsidText, ARRAYSIZE(clsidText));
    if (FAILED(hr))
    {
        return hr;
    }

    wchar_t clsidPath[256] = {};
    hr = StringCchPrintfW(clsidPath, ARRAYSIZE(clsidPath), L"CLSID\\%s", clsidText);
    if (FAILED(hr))
    {
        return hr;
    }

    hr = SetRegistryString(HKEY_CLASSES_ROOT, clsidPath, nullptr, kProviderName);
    if (FAILED(hr))
    {
        return hr;
    }

    wchar_t inprocPath[320] = {};
    hr = StringCchPrintfW(inprocPath, ARRAYSIZE(inprocPath), L"%s\\InprocServer32", clsidPath);
    if (FAILED(hr))
    {
        return hr;
    }

    hr = SetRegistryString(HKEY_CLASSES_ROOT, inprocPath, nullptr, modulePath);
    if (FAILED(hr))
    {
        return hr;
    }

    hr = SetRegistryString(HKEY_CLASSES_ROOT, inprocPath, L"ThreadingModel", L"Apartment");
    if (FAILED(hr))
    {
        return hr;
    }

    return SetRegistryString(HKEY_LOCAL_MACHINE, kCredentialProviderRegPath, nullptr, kProviderName);
}

STDAPI DllUnregisterServer()
{
    wchar_t clsidText[64] = {};
    HRESULT hr = GuidToString(CLSID_BluetoothUnlockProvider, clsidText, ARRAYSIZE(clsidText));
    if (FAILED(hr))
    {
        return hr;
    }

    wchar_t clsidPath[256] = {};
    hr = StringCchPrintfW(clsidPath, ARRAYSIZE(clsidPath), L"CLSID\\%s", clsidText);
    if (FAILED(hr))
    {
        return hr;
    }

    HRESULT hrProvider = DeleteRegistryTree(HKEY_LOCAL_MACHINE, kCredentialProviderRegPath);
    HRESULT hrCom = DeleteRegistryTree(HKEY_CLASSES_ROOT, clsidPath);
    return FAILED(hrProvider) ? hrProvider : hrCom;
}
