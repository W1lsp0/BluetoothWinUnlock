#pragma once

#include "Common.h"
#include "Fields.h"

class BluetoothUnlockCredential final :
    public ICredentialProviderCredential2
{
public:
    BluetoothUnlockCredential();
    ~BluetoothUnlockCredential();

    HRESULT Initialize(
        CREDENTIAL_PROVIDER_USAGE_SCENARIO scenario,
        ICredentialProviderUser *user);

    IFACEMETHODIMP QueryInterface(REFIID riid, void **ppv) override;
    IFACEMETHODIMP_(ULONG) AddRef() override;
    IFACEMETHODIMP_(ULONG) Release() override;

    IFACEMETHODIMP Advise(ICredentialProviderCredentialEvents *events) override;
    IFACEMETHODIMP UnAdvise() override;
    IFACEMETHODIMP SetSelected(BOOL *autoLogon) override;
    IFACEMETHODIMP SetDeselected() override;
    IFACEMETHODIMP GetFieldState(DWORD fieldId, CREDENTIAL_PROVIDER_FIELD_STATE *state, CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE *interactiveState) override;
    IFACEMETHODIMP GetStringValue(DWORD fieldId, PWSTR *value) override;
    IFACEMETHODIMP GetBitmapValue(DWORD fieldId, HBITMAP *bitmap) override;
    IFACEMETHODIMP GetCheckboxValue(DWORD fieldId, BOOL *checked, PWSTR *label) override;
    IFACEMETHODIMP GetSubmitButtonValue(DWORD fieldId, DWORD *adjacentTo) override;
    IFACEMETHODIMP GetComboBoxValueCount(DWORD fieldId, DWORD *items, DWORD *selectedItem) override;
    IFACEMETHODIMP GetComboBoxValueAt(DWORD fieldId, DWORD item, PWSTR *value) override;
    IFACEMETHODIMP SetStringValue(DWORD fieldId, PCWSTR value) override;
    IFACEMETHODIMP SetCheckboxValue(DWORD fieldId, BOOL checked) override;
    IFACEMETHODIMP SetComboBoxSelectedValue(DWORD fieldId, DWORD selectedItem) override;
    IFACEMETHODIMP CommandLinkClicked(DWORD fieldId) override;
    IFACEMETHODIMP GetSerialization(CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE *response, CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION *serialization, PWSTR *optionalStatusText, CREDENTIAL_PROVIDER_STATUS_ICON *optionalStatusIcon) override;
    IFACEMETHODIMP ReportResult(NTSTATUS status, NTSTATUS substatus, PWSTR *optionalStatusText, CREDENTIAL_PROVIDER_STATUS_ICON *optionalStatusIcon) override;

    IFACEMETHODIMP GetUserSid(PWSTR *sid) override;

private:
    long _refCount;
    CREDENTIAL_PROVIDER_USAGE_SCENARIO _scenario;
    ICredentialProviderCredentialEvents *_events;
    PWSTR _userSid;
    PWSTR _fieldStrings[FID_NUM_FIELDS];
};
