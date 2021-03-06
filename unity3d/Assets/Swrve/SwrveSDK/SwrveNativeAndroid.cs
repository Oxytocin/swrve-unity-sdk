﻿#if UNITY_ANDROID
using System.Collections.Generic;
using System;
using UnityEngine;
using Swrve.Helpers;
using SwrveMiniJSON;

public partial class SwrveSDK
{
    private const string SwrveAndroidPushPluginPackageName = "com.swrve.unity.gcm.SwrveGcmDeviceRegistration";
    private const string SwrveAndroidUnityCommonName = "com.swrve.sdk.SwrveUnityCommon";
    private const string SwrveAndroidPlotName = "com.swrve.sdk.SwrvePlot";

    private const string IsInitialisedName = "isInitialised";
    private const string GetConversationVersionName = "getConversationVersion";
    private const string ShowConversationName = "showConversation";
    private const string SwrvePlotOnCreateName = "onCreate";

    private const string UnityPlayerName = "com.unity3d.player.UnityPlayer";
    private const string UnityCurrentActivityName = "currentActivity";

    private string gcmDeviceToken;
    private static AndroidJavaObject androidPlugin;
    private static bool androidPluginInitialized = false;
    private static bool androidPluginInitializedSuccessfully = false;
    private string googlePlayAdvertisingId;
    private static bool startedPlot;

    private const int GooglePlayPushPluginVersion = 4;

    private void setNativeInfo(Dictionary<string, string> deviceInfo)
    {
        if (!string.IsNullOrEmpty(gcmDeviceToken)) {
            deviceInfo["swrve.gcm_token"] = gcmDeviceToken;
        }

        string timezone = AndroidGetTimezone();
        if (!string.IsNullOrEmpty(timezone)) {
            deviceInfo ["swrve.timezone_name"] = timezone;
        }

        string deviceRegion = AndroidGetRegion();
        if (!string.IsNullOrEmpty(deviceRegion)) {
            deviceInfo ["swrve.device_region"] = deviceRegion;
        }

        if (config.LogAndroidId) {
            try {
                deviceInfo ["swrve.android_id"] = AndroidGetAndroidId();
            } catch (Exception e) {
                SwrveLog.LogWarning("Couldn't get device IDFA, make sure you have the plugin inside your project and you are running on a device: " + e.ToString());
            }
        }

        if (config.LogGoogleAdvertisingId) {
            if (!string.IsNullOrEmpty(googlePlayAdvertisingId)) {
                deviceInfo ["swrve.GAID"] = googlePlayAdvertisingId;
            }
        }
    }

    private string getNativeLanguage()
    {
        string language = null;
        try {
            using (AndroidJavaClass localeJavaClass = new AndroidJavaClass("java.util.Locale")) {
                AndroidJavaObject defaultLocale = localeJavaClass.CallStatic<AndroidJavaObject>("getDefault");
                language = defaultLocale.Call<string>("getLanguage");
                string country = defaultLocale.Call<string>("getCountry");
                if (!string.IsNullOrEmpty (country)) {
                    language += "-" + country;
                }
                string variant = defaultLocale.Call<string>("getVariant");
                if (!string.IsNullOrEmpty (variant)) {
                    language += "-" + variant;
                }
            }
        } catch (Exception exp) {
            SwrveLog.LogWarning("Couldn't get the device language, make sure you are running on an Android device: " + exp.ToString());
        }
        return language;
    }

    private void GooglePlayRegisterForPushNotification(MonoBehaviour container, string senderId)
    {
        try {
            bool registered = false;
            this.gcmDeviceToken = storage.Load (GcmDeviceTokenSave);

            if (!androidPluginInitialized) {
                androidPluginInitialized = true;

                using (AndroidJavaClass unityPlayerClass = new AndroidJavaClass(UnityPlayerName)) {
                    string jniPluginClassName = SwrveAndroidPushPluginPackageName.Replace(".", "/");

                    if (AndroidJNI.FindClass(jniPluginClassName).ToInt32() != 0) {
                        androidPlugin = new AndroidJavaClass(SwrveAndroidPushPluginPackageName);

                        if (androidPlugin != null) {
                            // Check that the version is the same
                            int pluginVersion = androidPlugin.CallStatic<int>("getVersion");

                            if (pluginVersion != GooglePlayPushPluginVersion) {
                                // Plugin with changes to the public API not supported
                                androidPlugin = null;
                                throw new Exception("The version of the Swrve Android Push plugin is different. This Swrve SDK needs version " + GooglePlayPushPluginVersion);
                            } else {
                                androidPluginInitializedSuccessfully = true;
                            }
                        }
                    }
                }
            }

            if (androidPluginInitializedSuccessfully) {
                registered = androidPlugin.CallStatic<bool>("registerDevice", container.name, senderId, config.GCMPushNotificationTitle, config.GCMPushNotificationIconId, config.GCMPushNotificationMaterialIconId, config.GCMPushNotificationLargeIconId, config.GCMPushNotificationAccentColor);
            }

            if (!registered) {
                SwrveLog.LogError("Could not communicate with the Swrve Android Push plugin. Have you copied all the jars to the directory?");
            }
        } catch (Exception exp) {
            SwrveLog.LogError("Could not retrieve the device Registration Id: " + exp.ToString());
        }
    }

    public void SetGooglePlayAdvertisingId(string advertisingId)
    {
        this.googlePlayAdvertisingId = advertisingId;
        storage.Save(GoogleAdvertisingIdSave, advertisingId);
    }

    private void RequestGooglePlayAdvertisingId(MonoBehaviour container)
    {
        if (SwrveHelper.IsOnDevice ()) {
            try {
                this.googlePlayAdvertisingId = storage.Load(GoogleAdvertisingIdSave);
                using (AndroidJavaClass unityPlayerClass = new AndroidJavaClass(UnityPlayerName)) {
                    string jniPluginClassName = SwrveAndroidPushPluginPackageName.Replace(".", "/");

                    if (AndroidJNI.FindClass(jniPluginClassName).ToInt32() != 0) {
                        androidPlugin = new AndroidJavaClass(SwrveAndroidPushPluginPackageName);
                        if (androidPlugin != null) {
                            androidPlugin.CallStatic<bool>("requestAdvertisingId", container.name);
                        }
                    }
                }
            } catch (Exception exp) {
                SwrveLog.LogError("Could not retrieve the device Registration Id: " + exp.ToString());
            }
        }
    }

    private string AndroidGetTimezone()
    {
        try {
            AndroidJavaObject cal = new AndroidJavaObject("java.util.GregorianCalendar");
            return cal.Call<AndroidJavaObject>("getTimeZone").Call<string>("getID");
        } catch (Exception exp) {
            SwrveLog.LogWarning("Couldn't get the device timezone, make sure you are running on an Android device: " + exp.ToString());
        }

        return null;
    }

    private string AndroidGetRegion()
    {
        try {
            using (AndroidJavaClass localeJavaClass = new AndroidJavaClass("java.util.Locale")) {
                AndroidJavaObject defaultLocale = localeJavaClass.CallStatic<AndroidJavaObject>("getDefault");
                return defaultLocale.Call<string>("getCountry");
            }
        } catch (Exception exp) {
            SwrveLog.LogWarning("Couldn't get the device region, make sure you are running on an Android device: " + exp.ToString());
        }

        return null;
    }

    private string AndroidGetAppVersion()
    {
        if (SwrveHelper.IsOnDevice ()) {
            try {
                using (AndroidJavaClass unityPlayerClass = new AndroidJavaClass (UnityPlayerName)) {
                    AndroidJavaObject context = unityPlayerClass.GetStatic<AndroidJavaObject> (UnityCurrentActivityName);
                    string packageName = context.Call<string> ("getPackageName");
                    string versionName = context.Call<AndroidJavaObject> ("getPackageManager")
                    .Call<AndroidJavaObject> ("getPackageInfo", packageName, 0).Get<string> ("versionName");
                    return versionName;
                }
            } catch (Exception exp) {
                SwrveLog.LogWarning ("Couldn't get the device app version, make sure you are running on an Android device: " + exp.ToString ());
            }
        }

        return null;
    }

    private string _androidId;
    private string AndroidGetAndroidId()
    {
        if (SwrveHelper.IsOnDevice () && (_androidId == null)) {
            try {
                using (AndroidJavaClass unityPlayerClass = new AndroidJavaClass(UnityPlayerName)) {
                    AndroidJavaObject context = unityPlayerClass.GetStatic<AndroidJavaObject>(UnityCurrentActivityName);
                    AndroidJavaObject contentResolver = context.Call<AndroidJavaObject> ("getContentResolver");
                    AndroidJavaClass settingsSecure = new AndroidJavaClass ("android.provider.Settings$Secure");
                    _androidId = settingsSecure.CallStatic<string> ("getString", contentResolver, "android_id");
                }
            } catch (Exception exp) {
                SwrveLog.LogWarning("Couldn't get the \"android_id\" resource, make sure you are running on an Android device: " + exp.ToString());
            }
        }
        return _androidId;
    }

    /// <summary>
    /// Used internally by the Google Cloud Messaging plugin to notify
    /// of a device registration id.
    /// </summary>
    /// <param name="registrationId">
    /// The new device registration id.
    /// </param>
    public void RegistrationIdReceived(string registrationId)
    {
        if (!string.IsNullOrEmpty(registrationId)) {
            bool sendDeviceInfo = (this.gcmDeviceToken != registrationId);

            if (sendDeviceInfo) {
                this.gcmDeviceToken = registrationId;
                storage.Save (GcmDeviceTokenSave, gcmDeviceToken);
                if (qaUser != null) {
                    qaUser.UpdateDeviceInfo();
                }
                SendDeviceInfo();
            }
        }
    }

    /// <summary>
    /// Used internally by the Google Cloud Messaging plugin to notify
    /// of a received push notification, without any user interaction.
    /// </summary>
    /// <param name="notificationJson">
    /// Serialized push notification information.
    /// </param>
    public void NotificationReceived(string notificationJson)
    {
        Dictionary<string, object> notification = (Dictionary<string, object>)Json.Deserialize (notificationJson);
        if (androidPlugin != null && notification != null) {
            string pushId = GetPushId(notification);
            if (pushId != null) {
                // Acknowledge the received notification
                androidPlugin.CallStatic("sdkAcknowledgeReceivedNotification", pushId);
            }
        }

        if (PushNotificationListener != null) {
            try {
                PushNotificationListener.OnNotificationReceived(notification);
            } catch (Exception exp) {
                SwrveLog.LogError("Error processing the push notification: " + exp.Message);
            }
        }
    }

    /// <summary>
    /// Obtain the Swrve identifier from a received push notification.
    /// </summary>
    /// <param name="notification">
    /// Push notification received by the app.
    /// </param>
    private string GetPushId(Dictionary<string, object> notification)
    {
        if  (notification != null && notification.ContainsKey(PushTrackingKey)) {
            return notification[PushTrackingKey].ToString();
        } else {
            SwrveLog.Log("Got unidentified notification");
        }

        return null;
    }

    /// <summary>
    /// Used internally by the Google Cloud Messaging plugin to notify
    /// of a received push notification when the app was opened from it.
    /// </summary>
    /// <param name="notificationJson">
    /// Serialized push notification information.
    /// </param>
    public void OpenedFromPushNotification(string notificationJson)
    {
        Dictionary<string, object> notification = (Dictionary<string, object>)Json.Deserialize (notificationJson);
        string pushId = GetPushId(notification);
        SendPushNotificationEngagedEvent(pushId);
        if (pushId != null && androidPlugin != null) {
            // Acknowledge the received notification
            androidPlugin.CallStatic("sdkAcknowledgeOpenedNotification", pushId);
        }

        // Process push deeplink
        if (notification != null && notification.ContainsKey (PushDeeplinkKey)) {
            object deeplinkUrl = notification[PushDeeplinkKey];
            if (deeplinkUrl != null) {
                OpenURL(deeplinkUrl.ToString());
            }
        }

        if (PushNotificationListener != null) {
            try {
                PushNotificationListener.OnOpenedFromPushNotification(notification);
            } catch (Exception exp) {
                SwrveLog.LogError("Error processing the push notification: " + exp.Message);
            }
        }
    }

    private void initNative ()
    {
        AndroidInitNative();
    }

    private void AndroidInitNative()
    {
        try {
            AndroidGetBridge ();
        } catch (Exception exp) {
            SwrveLog.LogWarning ("Couldn't init common from Android: " + exp.ToString ());
        }
    }

    private AndroidJavaObject _androidBridge;
    private AndroidJavaObject AndroidGetBridge()
    {
        if (SwrveHelper.IsOnDevice ()) {
            using (AndroidJavaClass unityPlayerClass = new AndroidJavaClass (SwrveAndroidUnityCommonName)) {
                if (null == _androidBridge || !unityPlayerClass.CallStatic<bool> (IsInitialisedName)) {
                    _androidBridge = new AndroidJavaObject (SwrveAndroidUnityCommonName, GetNativeDetails ());
                }
            }
        }
        return _androidBridge;
    }

    private void setNativeAppVersion ()
    {
        config.AppVersion = AndroidGetAppVersion();
    }

    private void showNativeConversation (string conversation)
    {
        try {
            AndroidGetBridge().Call(ShowConversationName, conversation);
        } catch (Exception exp) {
            SwrveLog.LogWarning("Couldn't show conversation from Android: " + exp.ToString());
        }
    }

    private void startNativeLocation()
    {
        if (SwrveHelper.IsOnDevice ()) {
            try {
                AndroidGetBridge ();
                AndroidJavaClass swrvePlotClass = new AndroidJavaClass (SwrveAndroidPlotName);

                using (AndroidJavaClass unityPlayerClass = new AndroidJavaClass (UnityPlayerName)) {
                    AndroidJavaObject context = unityPlayerClass.GetStatic<AndroidJavaObject>(UnityCurrentActivityName);
                    swrvePlotClass.CallStatic (SwrvePlotOnCreateName, context);
                }
                startedPlot = true;
            } catch (Exception exp) {
                SwrveLog.LogWarning ("Couldn't StartPlot from Android: " + exp.ToString ());
            }
        }
    }

    private void setNativeConversationVersion()
    {
        try {
            SetConversationVersion (AndroidGetBridge().Call<int> (GetConversationVersionName));
        } catch (Exception exp) {
            SwrveLog.LogWarning("Couldn't get conversations version from Android: " + exp.ToString());
        }
    }

    private bool NativeIsBackPressed ()
    {
        return Input.GetKeyDown (KeyCode.Escape);
    }

}

#endif