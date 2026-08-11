#include "Helpers.h"
#include <intsafe.h>

HRESULT FieldDescriptorCoAllocCopy(
    _In_ const CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR &source,
    _Outptr_result_nullonfailure_ CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR **target)
{
    *target = nullptr;
    auto copy = static_cast<CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR *>(
        CoTaskMemAlloc(sizeof(CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR)));
    if (!copy)
    {
        return E_OUTOFMEMORY;
    }

    HRESULT hr = FieldDescriptorCopy(source, copy);
    if (SUCCEEDED(hr))
    {
        *target = copy;
    }
    else
    {
        CoTaskMemFree(copy);
    }

    return hr;
}

HRESULT FieldDescriptorCopy(
    _In_ const CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR &source,
    _Out_ CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR *target)
{
    target->dwFieldID = source.dwFieldID;
    target->cpft = source.cpft;
    target->guidFieldType = source.guidFieldType;
    target->pszLabel = nullptr;
    return source.pszLabel ? SHStrDupW(source.pszLabel, &target->pszLabel) : S_OK;
}

static HRESULT UnicodeStringInitWithString(PWSTR value, UNICODE_STRING *unicode)
{
    if (!value)
    {
        return E_INVALIDARG;
    }

    USHORT chars = 0;
    HRESULT hr = SizeTToUShort(wcslen(value), &chars);
    if (FAILED(hr))
    {
        return hr;
    }

    USHORT wcharSize = 0;
    hr = SizeTToUShort(sizeof(wchar_t), &wcharSize);
    if (FAILED(hr))
    {
        return hr;
    }

    hr = UShortMult(chars, wcharSize, &unicode->Length);
    if (FAILED(hr))
    {
        return hr;
    }

    unicode->MaximumLength = unicode->Length;
    unicode->Buffer = value;
    return S_OK;
}

HRESULT KerbInteractiveUnlockLogonInit(
    _In_ PWSTR domain,
    _In_ PWSTR username,
    _In_ PWSTR password,
    _In_ CREDENTIAL_PROVIDER_USAGE_SCENARIO scenario,
    _Out_ KERB_INTERACTIVE_UNLOCK_LOGON *unlockLogon)
{
    ZeroMemory(unlockLogon, sizeof(*unlockLogon));
    auto logon = &unlockLogon->Logon;

    HRESULT hr = UnicodeStringInitWithString(domain, &logon->LogonDomainName);
    if (SUCCEEDED(hr))
    {
        hr = UnicodeStringInitWithString(username, &logon->UserName);
    }
    if (SUCCEEDED(hr))
    {
        hr = UnicodeStringInitWithString(password, &logon->Password);
    }
    if (FAILED(hr))
    {
        return hr;
    }

    if (scenario == CPUS_UNLOCK_WORKSTATION)
    {
        logon->MessageType = KerbWorkstationUnlockLogon;
    }
    else if (scenario == CPUS_LOGON)
    {
        logon->MessageType = KerbInteractiveLogon;
    }
    else
    {
        return E_INVALIDARG;
    }

    return S_OK;
}

static void PackedUnicodeStringCopy(const UNICODE_STRING &source, PWSTR buffer, UNICODE_STRING *target)
{
    target->Length = source.Length;
    target->MaximumLength = source.Length;
    target->Buffer = buffer;
    CopyMemory(target->Buffer, source.Buffer, source.Length);
}

HRESULT KerbInteractiveUnlockLogonPack(
    _In_ const KERB_INTERACTIVE_UNLOCK_LOGON &unlockLogon,
    _Outptr_result_bytebuffer_(*size) BYTE **buffer,
    _Out_ DWORD *size)
{
    const auto logonIn = &unlockLogon.Logon;
    DWORD totalSize = sizeof(unlockLogon) +
        logonIn->LogonDomainName.Length +
        logonIn->UserName.Length +
        logonIn->Password.Length;

    auto out = static_cast<KERB_INTERACTIVE_UNLOCK_LOGON *>(CoTaskMemAlloc(totalSize));
    if (!out)
    {
        return E_OUTOFMEMORY;
    }

    ZeroMemory(out, sizeof(*out));
    BYTE *next = reinterpret_cast<BYTE *>(out) + sizeof(*out);
    auto logonOut = &out->Logon;
    logonOut->MessageType = logonIn->MessageType;

    PackedUnicodeStringCopy(logonIn->LogonDomainName, reinterpret_cast<PWSTR>(next), &logonOut->LogonDomainName);
    logonOut->LogonDomainName.Buffer = reinterpret_cast<PWSTR>(next - reinterpret_cast<BYTE *>(out));
    next += logonOut->LogonDomainName.Length;

    PackedUnicodeStringCopy(logonIn->UserName, reinterpret_cast<PWSTR>(next), &logonOut->UserName);
    logonOut->UserName.Buffer = reinterpret_cast<PWSTR>(next - reinterpret_cast<BYTE *>(out));
    next += logonOut->UserName.Length;

    PackedUnicodeStringCopy(logonIn->Password, reinterpret_cast<PWSTR>(next), &logonOut->Password);
    logonOut->Password.Buffer = reinterpret_cast<PWSTR>(next - reinterpret_cast<BYTE *>(out));

    *buffer = reinterpret_cast<BYTE *>(out);
    *size = totalSize;
    return S_OK;
}

static HRESULT LsaInitString(PSTRING destination, PCSTR source)
{
    USHORT length = 0;
    HRESULT hr = SizeTToUShort(strlen(source), &length);
    if (FAILED(hr))
    {
        return hr;
    }

    destination->Buffer = const_cast<PCHAR>(source);
    destination->Length = length;
    destination->MaximumLength = length + 1;
    return S_OK;
}

HRESULT RetrieveNegotiateAuthPackage(_Out_ ULONG *authPackage)
{
    HANDLE lsa = nullptr;
    NTSTATUS status = LsaConnectUntrusted(&lsa);
    HRESULT hr = HRESULT_FROM_NT(status);
    if (FAILED(hr))
    {
        return hr;
    }

    LSA_STRING packageName = {};
    hr = LsaInitString(&packageName, NEGOSSP_NAME_A);
    if (SUCCEEDED(hr))
    {
        status = LsaLookupAuthenticationPackage(lsa, &packageName, authPackage);
        hr = HRESULT_FROM_NT(status);
    }

    LsaDeregisterLogonProcess(lsa);
    return hr;
}

static HRESULT ProtectAndCopyString(PCWSTR value, PWSTR *protectedValue)
{
    *protectedValue = nullptr;
    PWSTR mutableCopy = nullptr;
    HRESULT hr = SHStrDupW(value, &mutableCopy);
    if (FAILED(hr))
    {
        return hr;
    }

    DWORD protectedChars = 0;
    if (!CredProtectW(FALSE, mutableCopy, static_cast<DWORD>(wcslen(mutableCopy) + 1), nullptr, &protectedChars, nullptr) &&
        GetLastError() == ERROR_INSUFFICIENT_BUFFER)
    {
        auto buffer = static_cast<PWSTR>(CoTaskMemAlloc(protectedChars * sizeof(wchar_t)));
        if (!buffer)
        {
            hr = E_OUTOFMEMORY;
        }
        else if (CredProtectW(FALSE, mutableCopy, static_cast<DWORD>(wcslen(mutableCopy) + 1), buffer, &protectedChars, nullptr))
        {
            *protectedValue = buffer;
            hr = S_OK;
        }
        else
        {
            hr = HRESULT_FROM_WIN32(GetLastError());
            CoTaskMemFree(buffer);
        }
    }
    else
    {
        hr = HRESULT_FROM_WIN32(GetLastError());
    }

    SecureZeroMemory(mutableCopy, wcslen(mutableCopy) * sizeof(wchar_t));
    CoTaskMemFree(mutableCopy);
    return hr;
}

HRESULT ProtectIfNecessaryAndCopyPassword(
    _In_ PCWSTR password,
    _In_ CREDENTIAL_PROVIDER_USAGE_SCENARIO scenario,
    _Outptr_result_nullonfailure_ PWSTR *protectedPassword)
{
    *protectedPassword = nullptr;

    if (!password || !*password)
    {
        return SHStrDupW(L"", protectedPassword);
    }

    if (scenario != CPUS_CREDUI)
    {
        return ProtectAndCopyString(password, protectedPassword);
    }

    return SHStrDupW(password, protectedPassword);
}
