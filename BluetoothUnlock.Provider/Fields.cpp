#include "Fields.h"

const CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR g_fieldDescriptors[FID_NUM_FIELDS] =
{
    { FID_TILE_IMAGE, CPFT_TILE_IMAGE, const_cast<PWSTR>(L"Bluetooth Unlock"), GUID_NULL },
    { FID_LARGE_TEXT, CPFT_LARGE_TEXT, const_cast<PWSTR>(L"Bluetooth Unlock"), GUID_NULL },
    { FID_STATUS_TEXT, CPFT_SMALL_TEXT, const_cast<PWSTR>(L"Waiting for verified Bluetooth device"), GUID_NULL },
    { FID_SUBMIT_BUTTON, CPFT_SUBMIT_BUTTON, const_cast<PWSTR>(L"Unlock"), GUID_NULL }
};

const FIELD_STATE_PAIR g_fieldStatePairs[FID_NUM_FIELDS] =
{
    { CPFS_DISPLAY_IN_BOTH, CPFIS_NONE },
    { CPFS_DISPLAY_IN_BOTH, CPFIS_NONE },
    { CPFS_DISPLAY_IN_SELECTED_TILE, CPFIS_NONE },
    { CPFS_DISPLAY_IN_SELECTED_TILE, CPFIS_NONE }
};
