#include "ClassFactory.h"
#include "Provider.h"
#include <new>

ClassFactory::ClassFactory() : _refCount(1)
{
    DllAddRef();
}

ClassFactory::~ClassFactory()
{
    DllRelease();
}

IFACEMETHODIMP ClassFactory::QueryInterface(REFIID riid, void **ppv)
{
    if (!ppv)
    {
        return E_INVALIDARG;
    }

    *ppv = nullptr;
    if (riid == IID_IUnknown || riid == IID_IClassFactory)
    {
        *ppv = static_cast<IClassFactory *>(this);
        AddRef();
        return S_OK;
    }

    return E_NOINTERFACE;
}

IFACEMETHODIMP_(ULONG) ClassFactory::AddRef()
{
    return InterlockedIncrement(&_refCount);
}

IFACEMETHODIMP_(ULONG) ClassFactory::Release()
{
    const long count = InterlockedDecrement(&_refCount);
    if (count == 0)
    {
        delete this;
    }
    return count;
}

IFACEMETHODIMP ClassFactory::CreateInstance(IUnknown *outer, REFIID riid, void **ppv)
{
    *ppv = nullptr;
    if (outer)
    {
        return CLASS_E_NOAGGREGATION;
    }

    auto provider = new (std::nothrow) BluetoothUnlockProvider();
    if (!provider)
    {
        return E_OUTOFMEMORY;
    }

    HRESULT hr = provider->QueryInterface(riid, ppv);
    provider->Release();
    return hr;
}

IFACEMETHODIMP ClassFactory::LockServer(BOOL lock)
{
    if (lock)
    {
        DllAddRef();
    }
    else
    {
        DllRelease();
    }

    return S_OK;
}
