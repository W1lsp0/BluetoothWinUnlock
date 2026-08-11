#pragma once

#include <string>

struct ServiceCredential
{
    bool success = false;
    std::wstring domain;
    std::wstring username;
    std::wstring password;
    std::wstring status;
};

ServiceCredential QueryServiceCredential();
