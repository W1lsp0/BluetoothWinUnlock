#include "Provider.h"
#include "Fields.h"
#include "Helpers.h"
#include <new>

BluetoothUnlockProvider::BluetoothUnlockProvider() :
    _refCount(1),
    _scenario(CPUS_INVALID),
    _users(nullptr),
    _credential(nullptr)
{
    DllAddRef();
}

BluetoothUnlockProvider::~BluetoothUnlockProvider()
{
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

IFACEMETHODIMP BluetoothUnlockProvider::Advise(ICredentialProviderEvents *, UINT_PTR)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP BluetoothUnlockProvider::UnAdvise()
{
    return E_NOTIMPL;
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
