#pragma once

#include "Common.h"

class ClassFactory final : public IClassFactory
{
public:
    ClassFactory();
    ~ClassFactory();

    IFACEMETHODIMP QueryInterface(REFIID riid, void **ppv) override;
    IFACEMETHODIMP_(ULONG) AddRef() override;
    IFACEMETHODIMP_(ULONG) Release() override;
    IFACEMETHODIMP CreateInstance(IUnknown *outer, REFIID riid, void **ppv) override;
    IFACEMETHODIMP LockServer(BOOL lock) override;

private:
    long _refCount;
};
