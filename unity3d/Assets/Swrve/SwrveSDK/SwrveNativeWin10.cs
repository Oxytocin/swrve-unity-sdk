﻿#if UNITY_WSA_10_0

using System.Collections.Generic;
using System;
using SwrveUnity.IAP;
using SwrveUnity.Messaging;

public partial class SwrveSDK
{
    private void setNativeInfo (Dictionary<string, string> deviceInfo) {}
    private string getNativeLanguage () { return null; }
    private void setNativeAppVersion () {}

    private void showNativeConversation (string conversation)
    {
#if SWRVE_CONVERSATION_SDK
        UnityEngine.WSA.Application.InvokeOnUIThread(() =>
            {
                Swrve.Conversation.SwrveUnityBridge.ShowConversation(1, conversation);
            },
        true);
#endif
    }
    private void initNative () {}
    private void startNativeLocation () {}
    private void startNativeLocationAfterPermission () {}
    private void setNativeConversationVersion ()
    {
#if SWRVE_CONVERSATION_SDK
        SetConversationVersion(3);
#endif
    }
    private bool NativeIsBackPressed () { return false; }

    /// <summary>
    /// Buffer the event of a purchase using real currency, where a single item
    /// (that isn't an in-app currency) was purchased.
    /// The receipt provided will be validated against the Windows Store.
    /// </summary>
    /// <remarks>
    /// See the REST API documentation for the "iap" event.
    /// Note that this method is currently only supported for the Windows Store,
    /// and a valid receipt needs to be provided for verification.
    /// </remarks>
    /// <param name="receipt">
    /// The receipt sent back from the Windows Store upon successful purchase - this receipt will be verified by Swrve
    /// </param>
    public void IapWindows(IapReceipt receipt)
    {
        IapRewards no_rewards = new IapRewards();
        IapWindows(receipt, no_rewards);
    }

    /// <summary>
    /// Buffer the event of a purchase using real currency, where a single item
    /// (that isn't an in-app currency) was purchased.
    /// The receipt provided will be validated against the Windows Store.
    /// </summary>
    /// <remarks>
    /// See the REST API documentation for the "iap" event.
    /// Note that this method is currently only supported for the Windows Store,
    /// and a valid receipt needs to be provided for verification.
    /// </remarks>
    /// <param name="receipt">
    /// The receipt sent back from the Windows Store upon successful purchase - this receipt will be verified by Swrve
    /// </param>
    /// <param name="rewards">
    /// SwrveIAPRewards object containing any in-app currency and/or additional items
    /// included in this purchase that need to be recorded.
    /// This parameter is optional.
    /// </param>
    public void IapWindows(IapReceipt receipt, IapRewards rewards)
    {
        if (config.AppStore != SwrveAppStore.Windows)
        {
            throw new Exception ("This function can only be called to validate IAP events from Windows Store");
        }
        else
        {
            string encodedReceipt = (receipt != null) ? receipt.GetBase64EncodedReceipt() : null;
            if (receipt != null && string.IsNullOrEmpty(encodedReceipt))
            {
                SwrveLog.LogError("IAP event not sent: receipt cannot be empty for Windows Store verification");
                return;
            }
            // Windows Store IAP is always of quantity 1
            Dictionary<string, object> json = new Dictionary<string, object>();
            json.Add("app_store", config.AppStore);
            if (!string.IsNullOrEmpty(GetAppVersion()))
            {
                json.Add("app_version", GetAppVersion());
            }
            json.Add("receipt", encodedReceipt);
            AppendEventToBuffer("iap", json);

            if (config.AutoDownloadCampaignsAndResources)
            {
                // Send events automatically and check for changes
                CheckForCampaignsAndResourcesUpdates(false);
            }
        }
    }
}

#endif
