#include "Credential.h"
#include "Guid.h"
#include "Helpers.h"
#include "IpcClient.h"
#include <new>
#include <string>

namespace
{
    void NormalizeLocalDomain(std::wstring &domain)
    {
        if (!domain.empty() && domain != L".")
        {
            return;
        }

        wchar_t computerName[MAX_COMPUTERNAME_LENGTH + 1] = {};
        DWORD cchComputerName = ARRAYSIZE(computerName);
        if (GetComputerNameW(computerName, &cchComputerName) && cchComputerName > 0)
        {
            domain.assign(computerName, cchComputerName);
        }
    }
}

BluetoothUnlockCredential::BluetoothUnlockCredential() :
    _refCount(1),
    _scenario(CPUS_INVALID),
    _events(nullptr),
    _userSid(nullptr)
{
    ZeroMemory(_fieldStrings, sizeof(_fieldStrings));
    DllAddRef();
}

BluetoothUnlockCredential::~BluetoothUnlockCredential()
{
    SafeRelease(&_events);
    CoTaskMemFree(_userSid);
    for (auto &value : _fieldStrings)
    {
        CoTaskMemFree(value);
    }
    DllRelease();
}

HRESULT BluetoothUnlockCredential::Initialize(
    CREDENTIAL_PROVIDER_USAGE_SCENARIO scenario,
    ICredentialProviderUser *user)
{
    _scenario = scenario;

    HRESULT hr = SHStrDupW(L"Bluetooth Unlock", &_fieldStrings[FID_LARGE_TEXT]);
    if (SUCCEEDED(hr))
    {
        hr = SHStrDupW(L"Submit after your trusted device is verified.", &_fieldStrings[FID_STATUS_TEXT]);
    }
    if (SUCCEEDED(hr))
    {
        hr = SHStrDupW(L"Unlock", &_fieldStrings[FID_SUBMIT_BUTTON]);
    }
    if (SUCCEEDED(hr) && user)
    {
        hr = user->GetSid(&_userSid);
        if (hr == S_FALSE)
        {
            hr = S_OK;
        }
    }

    return hr;
}

IFACEMETHODIMP BluetoothUnlockCredential::QueryInterface(REFIID riid, void **ppv)
{
    if (!ppv)
    {
        return E_INVALIDARG;
    }

    *ppv = nullptr;
    if (riid == IID_IUnknown || riid == IID_ICredentialProviderCredential)
    {
        *ppv = static_cast<ICredentialProviderCredential *>(this);
    }
    else if (riid == IID_ICredentialProviderCredential2)
    {
        *ppv = static_cast<ICredentialProviderCredential2 *>(this);
    }

    if (*ppv)
    {
        AddRef();
        return S_OK;
    }

    return E_NOINTERFACE;
}

IFACEMETHODIMP_(ULONG) BluetoothUnlockCredential::AddRef()
{
    return InterlockedIncrement(&_refCount);
}

IFACEMETHODIMP_(ULONG) BluetoothUnlockCredential::Release()
{
    const long count = InterlockedDecrement(&_refCount);
    if (count == 0)
    {
        delete this;
    }
    return count;
}

IFACEMETHODIMP BluetoothUnlockCredential::Advise(ICredentialProviderCredentialEvents *events)
{
    SafeRelease(&_events);
    return events ? events->QueryInterface(IID_PPV_ARGS(&_events)) : S_OK;
}

IFACEMETHODIMP BluetoothUnlockCredential::UnAdvise()
{
    SafeRelease(&_events);
    return S_OK;
}

IFACEMETHODIMP BluetoothUnlockCredential::SetSelected(BOOL *autoLogon)
{
    if (!autoLogon)
    {
        return E_INVALIDARG;
    }

    *autoLogon = (_scenario == CPUS_UNLOCK_WORKSTATION && QueryAutoSubmitAllowed()) ? TRUE : FALSE;
    return S_OK;
}

IFACEMETHODIMP BluetoothUnlockCredential::SetDeselected()
{
    return S_OK;
}

IFACEMETHODIMP BluetoothUnlockCredential::GetFieldState(
    DWORD fieldId,
    CREDENTIAL_PROVIDER_FIELD_STATE *state,
    CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE *interactiveState)
{
    if (fieldId >= FID_NUM_FIELDS)
    {
        return E_INVALIDARG;
    }

    *state = g_fieldStatePairs[fieldId].cpfs;
    *interactiveState = g_fieldStatePairs[fieldId].cpfis;
    return S_OK;
}

IFACEMETHODIMP BluetoothUnlockCredential::GetStringValue(DWORD fieldId, PWSTR *value)
{
    *value = nullptr;
    if (fieldId >= FID_NUM_FIELDS)
    {
        return E_INVALIDARG;
    }

    return SHStrDupW(_fieldStrings[fieldId] ? _fieldStrings[fieldId] : L"", value);
}

IFACEMETHODIMP BluetoothUnlockCredential::GetBitmapValue(DWORD fieldId, HBITMAP *bitmap)
{
    *bitmap = nullptr;
    if (fieldId != FID_TILE_IMAGE)
    {
        return E_INVALIDARG;
    }

    DWORD pixels[64 * 64] = {};
    for (auto &pixel : pixels)
    {
        pixel = 0xFF1F6FEB;
    }

    *bitmap = CreateBitmap(64, 64, 1, 32, pixels);
    return *bitmap ? S_OK : HRESULT_FROM_WIN32(GetLastError());
}

IFACEMETHODIMP BluetoothUnlockCredential::GetCheckboxValue(DWORD, BOOL *, PWSTR *)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP BluetoothUnlockCredential::GetSubmitButtonValue(DWORD fieldId, DWORD *adjacentTo)
{
    if (fieldId != FID_SUBMIT_BUTTON)
    {
        return E_INVALIDARG;
    }

    *adjacentTo = FID_STATUS_TEXT;
    return S_OK;
}

IFACEMETHODIMP BluetoothUnlockCredential::GetComboBoxValueCount(DWORD, DWORD *, DWORD *)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP BluetoothUnlockCredential::GetComboBoxValueAt(DWORD, DWORD, PWSTR *)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP BluetoothUnlockCredential::SetStringValue(DWORD, PCWSTR)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP BluetoothUnlockCredential::SetCheckboxValue(DWORD, BOOL)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP BluetoothUnlockCredential::SetComboBoxSelectedValue(DWORD, DWORD)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP BluetoothUnlockCredential::CommandLinkClicked(DWORD)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP BluetoothUnlockCredential::GetSerialization(
    CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE *response,
    CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION *serialization,
    PWSTR *optionalStatusText,
    CREDENTIAL_PROVIDER_STATUS_ICON *optionalStatusIcon)
{
    *response = CPGSR_NO_CREDENTIAL_NOT_FINISHED;
    *optionalStatusText = nullptr;
    *optionalStatusIcon = CPSI_NONE;
    ZeroMemory(serialization, sizeof(*serialization));

    auto credential = QueryServiceCredential();
    if (!credential.success)
    {
        SHStrDupW(credential.status.empty() ? L"Bluetooth device is not verified." : credential.status.c_str(), optionalStatusText);
        *optionalStatusIcon = CPSI_WARNING;
        return S_OK;
    }
    NormalizeLocalDomain(credential.domain);

    PWSTR protectedPassword = nullptr;
    HRESULT hr = ProtectIfNecessaryAndCopyPassword(credential.password.c_str(), _scenario, &protectedPassword);
    if (FAILED(hr))
    {
        return hr;
    }

    KERB_INTERACTIVE_UNLOCK_LOGON unlockLogon = {};
    hr = KerbInteractiveUnlockLogonInit(
        const_cast<PWSTR>(credential.domain.c_str()),
        const_cast<PWSTR>(credential.username.c_str()),
        protectedPassword,
        _scenario,
        &unlockLogon);

    if (SUCCEEDED(hr))
    {
        hr = KerbInteractiveUnlockLogonPack(
            unlockLogon,
            &serialization->rgbSerialization,
            &serialization->cbSerialization);
    }

    if (SUCCEEDED(hr))
    {
        ULONG authPackage = 0;
        hr = RetrieveNegotiateAuthPackage(&authPackage);
        if (SUCCEEDED(hr))
        {
            serialization->ulAuthenticationPackage = authPackage;
            serialization->clsidCredentialProvider = CLSID_BluetoothUnlockProvider;
            *response = CPGSR_RETURN_CREDENTIAL_FINISHED;
        }
    }

    if (FAILED(hr) && serialization->rgbSerialization)
    {
        CoTaskMemFree(serialization->rgbSerialization);
        serialization->rgbSerialization = nullptr;
        serialization->cbSerialization = 0;
    }

    if (protectedPassword)
    {
        SecureZeroMemory(protectedPassword, wcslen(protectedPassword) * sizeof(wchar_t));
        CoTaskMemFree(protectedPassword);
    }

    SecureZeroMemory(credential.password.data(), credential.password.size() * sizeof(wchar_t));
    return hr;
}

IFACEMETHODIMP BluetoothUnlockCredential::ReportResult(
    NTSTATUS status,
    NTSTATUS,
    PWSTR *optionalStatusText,
    CREDENTIAL_PROVIDER_STATUS_ICON *optionalStatusIcon)
{
    *optionalStatusText = nullptr;
    *optionalStatusIcon = CPSI_NONE;

    if (FAILED(HRESULT_FROM_NT(status)))
    {
        SHStrDupW(L"Bluetooth unlock failed. Check the saved Windows credential.", optionalStatusText);
        *optionalStatusIcon = CPSI_ERROR;
    }

    return S_OK;
}

IFACEMETHODIMP BluetoothUnlockCredential::GetUserSid(PWSTR *sid)
{
    *sid = nullptr;
    return _userSid ? SHStrDupW(_userSid, sid) : S_FALSE;
}
