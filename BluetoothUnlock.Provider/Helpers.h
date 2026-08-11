#pragma once

#include "Common.h"

HRESULT FieldDescriptorCoAllocCopy(
    _In_ const CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR &source,
    _Outptr_result_nullonfailure_ CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR **target);

HRESULT FieldDescriptorCopy(
    _In_ const CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR &source,
    _Out_ CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR *target);

HRESULT KerbInteractiveUnlockLogonInit(
    _In_ PWSTR domain,
    _In_ PWSTR username,
    _In_ PWSTR password,
    _In_ CREDENTIAL_PROVIDER_USAGE_SCENARIO scenario,
    _Out_ KERB_INTERACTIVE_UNLOCK_LOGON *unlockLogon);

HRESULT KerbInteractiveUnlockLogonPack(
    _In_ const KERB_INTERACTIVE_UNLOCK_LOGON &unlockLogon,
    _Outptr_result_bytebuffer_(*size) BYTE **buffer,
    _Out_ DWORD *size);

HRESULT RetrieveNegotiateAuthPackage(_Out_ ULONG *authPackage);

HRESULT ProtectIfNecessaryAndCopyPassword(
    _In_ PCWSTR password,
    _In_ CREDENTIAL_PROVIDER_USAGE_SCENARIO scenario,
    _Outptr_result_nullonfailure_ PWSTR *protectedPassword);
