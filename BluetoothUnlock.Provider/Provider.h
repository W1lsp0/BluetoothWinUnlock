#pragma once

#include "Common.h"
#include "Credential.h"

class BluetoothUnlockProvider final :
    public ICredentialProvider,
    public ICredentialProviderSetUserArray
{
public:
    BluetoothUnlockProvider();
    ~BluetoothUnlockProvider();

    IFACEMETHODIMP QueryInterface(REFIID riid, void **ppv) override;
    IFACEMETHODIMP_(ULONG) AddRef() override;
    IFACEMETHODIMP_(ULONG) Release() override;

    IFACEMETHODIMP SetUsageScenario(CREDENTIAL_PROVIDER_USAGE_SCENARIO scenario, DWORD flags) override;
    IFACEMETHODIMP SetSerialization(const CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION *serialization) override;
    IFACEMETHODIMP Advise(ICredentialProviderEvents *events, UINT_PTR adviseContext) override;
    IFACEMETHODIMP UnAdvise() override;
    IFACEMETHODIMP GetFieldDescriptorCount(DWORD *count) override;
    IFACEMETHODIMP GetFieldDescriptorAt(DWORD index, CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR **descriptor) override;
    IFACEMETHODIMP GetCredentialCount(DWORD *count, DWORD *defaultCredential, BOOL *autoLogonWithDefault) override;
    IFACEMETHODIMP GetCredentialAt(DWORD index, ICredentialProviderCredential **credential) override;

    IFACEMETHODIMP SetUserArray(ICredentialProviderUserArray *users) override;

private:
    HRESULT EnsureCredential();

    long _refCount;
    CREDENTIAL_PROVIDER_USAGE_SCENARIO _scenario;
    ICredentialProviderUserArray *_users;
    BluetoothUnlockCredential *_credential;
};
