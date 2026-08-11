#pragma once

#ifndef WIN32_NO_STATUS
#include <ntstatus.h>
#define WIN32_NO_STATUS
#endif

#include <windows.h>
#include <credentialprovider.h>
#include <ntsecapi.h>
#include <strsafe.h>
#include <shlwapi.h>
#include <wincred.h>

#define SECURITY_WIN32
#include <security.h>

void DllAddRef();
void DllRelease();
HINSTANCE DllInstance();

template <typename T>
void SafeRelease(T **value)
{
    if (value && *value)
    {
        (*value)->Release();
        *value = nullptr;
    }
}
