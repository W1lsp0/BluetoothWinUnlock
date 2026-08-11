#include "IpcClient.h"
#include <windows.h>
#include <wincrypt.h>
#include <strsafe.h>
#include <cstring>
#include <sstream>
#include <string>
#include <vector>

namespace
{
    constexpr wchar_t kPipePath[] = L"\\\\.\\pipe\\BluetoothUnlock";

    std::wstring Utf8ToWide(const std::vector<BYTE> &bytes)
    {
        if (bytes.empty())
        {
            return L"";
        }

        const int required = MultiByteToWideChar(
            CP_UTF8,
            MB_ERR_INVALID_CHARS,
            reinterpret_cast<LPCCH>(bytes.data()),
            static_cast<int>(bytes.size()),
            nullptr,
            0);
        if (required <= 0)
        {
            return L"";
        }

        std::wstring value(required, L'\0');
        MultiByteToWideChar(
            CP_UTF8,
            MB_ERR_INVALID_CHARS,
            reinterpret_cast<LPCCH>(bytes.data()),
            static_cast<int>(bytes.size()),
            value.data(),
            required);
        return value;
    }

    std::wstring Base64Utf8ToWide(const std::string &encoded)
    {
        DWORD cbBinary = 0;
        if (!CryptStringToBinaryA(
            encoded.c_str(),
            0,
            CRYPT_STRING_BASE64,
            nullptr,
            &cbBinary,
            nullptr,
            nullptr))
        {
            return L"";
        }

        std::vector<BYTE> binary(cbBinary);
        if (!CryptStringToBinaryA(
            encoded.c_str(),
            0,
            CRYPT_STRING_BASE64,
            binary.data(),
            &cbBinary,
            nullptr,
            nullptr))
        {
            return L"";
        }

        binary.resize(cbBinary);
        return Utf8ToWide(binary);
    }

    std::wstring ErrorText(DWORD error)
    {
        wchar_t buffer[256] = {};
        StringCchPrintfW(buffer, ARRAYSIZE(buffer), L"pipe error 0x%08X", error);
        return buffer;
    }

    bool SendPipeCommand(const char *request, DWORD waitMs, std::string &response, std::wstring &status)
    {
        if (!WaitNamedPipeW(kPipePath, waitMs))
        {
            status = ErrorText(GetLastError());
            return false;
        }

        HANDLE pipe = CreateFileW(
            kPipePath,
            GENERIC_READ | GENERIC_WRITE,
            0,
            nullptr,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL,
            nullptr);

        if (pipe == INVALID_HANDLE_VALUE)
        {
            status = ErrorText(GetLastError());
            return false;
        }

        DWORD written = 0;
        if (!WriteFile(pipe, request, static_cast<DWORD>(strlen(request)), &written, nullptr))
        {
            status = ErrorText(GetLastError());
            CloseHandle(pipe);
            return false;
        }

        char buffer[512] = {};
        DWORD read = 0;
        while (ReadFile(pipe, buffer, sizeof(buffer), &read, nullptr) && read > 0)
        {
            response.append(buffer, buffer + read);
            if (response.find("\nEND\n") != std::string::npos || response.size() > 8192)
            {
                break;
            }
        }
        CloseHandle(pipe);
        return !response.empty();
    }
}

ServiceCredential QueryServiceCredential()
{
    ServiceCredential result;
    std::string response;
    if (!SendPipeCommand("GETCRED\n", 3000, response, result.status))
    {
        return result;
    }

    std::istringstream lines(response);
    std::string line;
    std::getline(lines, line);
    if (line != "OK")
    {
        if (line == "ERR not-verified" || line == "ERR not-ready")
        {
            result.status = L"Waiting for trusted Bluetooth device.";
        }
        else
        {
            result.status = Utf8ToWide(std::vector<BYTE>(line.begin(), line.end()));
        }
        return result;
    }

    result.success = true;
    while (std::getline(lines, line))
    {
        if (line == "END")
        {
            break;
        }

        const auto separator = line.find(':');
        if (separator == std::string::npos)
        {
            continue;
        }

        const auto key = line.substr(0, separator);
        const auto value = line.substr(separator + 1);
        if (key == "domain")
        {
            result.domain = Base64Utf8ToWide(value);
        }
        else if (key == "username")
        {
            result.username = Base64Utf8ToWide(value);
        }
        else if (key == "password")
        {
            result.password = Base64Utf8ToWide(value);
        }
    }

    if (result.username.empty())
    {
        result.success = false;
        result.status = L"service returned no username";
    }

    if (result.domain.empty())
    {
        result.domain = L".";
    }

    return result;
}

bool QueryAutoSubmitAllowed()
{
    std::string response;
    std::wstring status;
    if (!SendPipeCommand("CANUNLOCK\n", 250, response, status))
    {
        return false;
    }

    std::istringstream lines(response);
    std::string line;
    std::getline(lines, line);
    return line == "OK";
}
