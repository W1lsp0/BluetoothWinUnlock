#include "Provider.h"
#include "Fields.h"
#include "Helpers.h"
#include "IpcClient.h"
#include <new>

namespace
{
    bool IsAutoSubmitScenario(CREDENTIAL_PROVIDER_USAGE_SCENARIO scenario)
    {
        return scenario == CPUS_UNLOCK_WORKSTATION || scenario == CPUS_LOGON;
    }
}

BluetoothUnlockProvider::BluetoothUnlockProvider() :
    _refCount(1),
    _scenario(CPUS_INVALID),
    _users(nullptr),
    _credential(nullptr),
    _events(nullptr),
    _adviseContext(0),
    _stopPollEvent(nullptr),
    _pollThread(nullptr),
    _lastAutoSubmitReady(false)
{
    DllAddRef();
}

BluetoothUnlockProvider::~BluetoothUnlockProvider()
{
    StopAutoSubmitPolling();
    SafeRelease(&_events);
    SafeRelease(&_users);
    SafeRelease(&_credential);
    DllRelease();
}

IFACEMETHODIMP BluetoothUnlockProvider::QueryInterface(REFIID riid, void **ppv)
{
    if (!ppv)
    {
        return E_INVALIDARG;
    }

    *ppv = nullptr;
    if (riid == IID_IUnknown || riid == IID_ICredentialProvider)
    {
        *ppv = static_cast<ICredentialProvider *>(this);
    }
    else if (riid == IID_ICredentialProviderSetUserArray)
    {
        *ppv = static_cast<ICredentialProviderSetUserArray *>(this);
    }

    if (*ppv)
    {
        AddRef();
        return S_OK;
    }

    return E_NOINTERFACE;
}

IFACEMETHODIMP_(ULONG) BluetoothUnlockProvider::AddRef()
{
    return InterlockedIncrement(&_refCount);
}

IFACEMETHODIMP_(ULONG) BluetoothUnlockProvider::Release()
{
    const long count = InterlockedDecrement(&_refCount);
    if (count == 0)
    {
        delete this;
    }
    return count;
}

IFACEMETHODIMP BluetoothUnlockProvider::SetUsageScenario(CREDENTIAL_PROVIDER_USAGE_SCENARIO scenario, DWORD)
{
    if (scenario == CPUS_UNLOCK_WORKSTATION || scenario == CPUS_LOGON)
    {
        _scenario = scenario;
        SafeRelease(&_credential);
        return S_OK;
    }

    return E_NOTIMPL;
}

IFACEMETHODIMP BluetoothUnlockProvider::SetSerialization(const CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION *)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP BluetoothUnlockProvider::Advise(ICredentialProviderEvents *events, UINT_PTR adviseContext)
{
    StopAutoSubmitPolling();
    SafeRelease(&_events);
    _adviseContext = adviseContext;
    _lastAutoSubmitReady = false;

    if (events)
    {
        HRESULT hr = events->QueryInterface(IID_PPV_ARGS(&_events));
        if (FAILED(hr))
        {
            return hr;
        }
    }

    StartAutoSubmitPolling();
    return S_OK;
}

IFACEMETHODIMP BluetoothUnlockProvider::UnAdvise()
{
    StopAutoSubmitPolling();
    SafeRelease(&_events);
    _adviseContext = 0;
    return S_OK;
}

IFACEMETHODIMP BluetoothUnlockProvider::GetFieldDescriptorCount(DWORD *count)
{
    *count = FID_NUM_FIELDS;
    return S_OK;
}

IFACEMETHODIMP BluetoothUnlockProvider::GetFieldDescriptorAt(DWORD index, CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR **descriptor)
{
    *descriptor = nullptr;
    if (index >= FID_NUM_FIELDS)
    {
        return E_INVALIDARG;
    }

    return FieldDescriptorCoAllocCopy(g_fieldDescriptors[index], descriptor);
}

IFACEMETHODIMP BluetoothUnlockProvider::GetCredentialCount(
    DWORD *count,
    DWORD *defaultCredential,
    BOOL *autoLogonWithDefault)
{
    HRESULT hr = EnsureCredential();
    *count = SUCCEEDED(hr) ? 1 : 0;
    *defaultCredential = CREDENTIAL_PROVIDER_NO_DEFAULT;
    *autoLogonWithDefault = FALSE;
    if (SUCCEEDED(hr) && IsAutoSubmitScenario(_scenario) && QueryAutoSubmitAllowed())
    {
        *defaultCredential = 0;
        *autoLogonWithDefault = TRUE;
    }
    return S_OK;
}

IFACEMETHODIMP BluetoothUnlockProvider::GetCredentialAt(DWORD index, ICredentialProviderCredential **credential)
{
    *credential = nullptr;
    if (index != 0)
    {
        return E_INVALIDARG;
    }

    HRESULT hr = EnsureCredential();
    if (FAILED(hr))
    {
        return hr;
    }

    return _credential->QueryInterface(IID_PPV_ARGS(credential));
}

IFACEMETHODIMP BluetoothUnlockProvider::SetUserArray(ICredentialProviderUserArray *users)
{
    SafeRelease(&_users);
    _users = users;
    if (_users)
    {
        _users->AddRef();
    }
    SafeRelease(&_credential);
    return S_OK;
}

HRESULT BluetoothUnlockProvider::EnsureCredential()
{
    if (_credential)
    {
        return S_OK;
    }

    ICredentialProviderUser *user = nullptr;
    if (_users)
    {
        DWORD userCount = 0;
        _users->GetCount(&userCount);
        if (userCount > 0)
        {
            _users->GetAt(0, &user);
        }
    }

    auto credential = new (std::nothrow) BluetoothUnlockCredential();
    if (!credential)
    {
        SafeRelease(&user);
        return E_OUTOFMEMORY;
    }

    HRESULT hr = credential->Initialize(_scenario, user);
    SafeRelease(&user);
    if (SUCCEEDED(hr))
    {
        _credential = credential;
    }
    else
    {
        credential->Release();
    }

    return hr;
}

void BluetoothUnlockProvider::StartAutoSubmitPolling()
{
    if (_pollThread || !IsAutoSubmitScenario(_scenario) || !_events)
    {
        return;
    }

    _stopPollEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (!_stopPollEvent)
    {
        return;
    }

    AddRef();
    _pollThread = CreateThread(nullptr, 0, AutoSubmitPollThreadProc, this, 0, nullptr);
    if (!_pollThread)
    {
        CloseHandle(_stopPollEvent);
        _stopPollEvent = nullptr;
        Release();
    }
}

void BluetoothUnlockProvider::StopAutoSubmitPolling()
{
    if (_stopPollEvent)
    {
        SetEvent(_stopPollEvent);
    }

    if (_pollThread)
    {
        WaitForSingleObject(_pollThread, 3000);
        CloseHandle(_pollThread);
        _pollThread = nullptr;
    }

    if (_stopPollEvent)
    {
        CloseHandle(_stopPollEvent);
        _stopPollEvent = nullptr;
    }

    _lastAutoSubmitReady = false;
}

DWORD BluetoothUnlockProvider::AutoSubmitPollLoop()
{
    ULONGLONG lastReadyNotifyTick = 0;
    while (WaitForSingleObject(_stopPollEvent, 1000) == WAIT_TIMEOUT)
    {
        const bool ready = QueryAutoSubmitAllowed();
        const ULONGLONG now = GetTickCount64();
        if (ready && _events && (!_lastAutoSubmitReady || now - lastReadyNotifyTick >= 3000))
        {
            _events->CredentialsChanged(_adviseContext);
            lastReadyNotifyTick = now;
        }
        _lastAutoSubmitReady = ready;
    }

    Release();
    return 0;
}

DWORD WINAPI BluetoothUnlockProvider::AutoSubmitPollThreadProc(LPVOID parameter)
{
    return static_cast<BluetoothUnlockProvider *>(parameter)->AutoSubmitPollLoop();
}
