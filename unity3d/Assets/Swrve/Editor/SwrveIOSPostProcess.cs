﻿#if UNITY_IPHONE
using UnityEngine;
using UnityEditor.Callbacks;
using UnityEditor;
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor.iOS.Xcode;

/// <summary>
/// Integrates the native code required for Conversations and Location campaigns support on iOS.
/// </summary>
public class SwrveIOSPostProcess : SwrveCommonBuildComponent
{
    [PostProcessBuild(100)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
#if UNITY_5
        if (target == BuildTarget.iOS)
#else
        if(target == BuildTarget.iPhone)
#endif
        {
            CorrectXCodeProject (pathToBuiltProject, true);
        }
    }

    private static void CorrectXCodeProject (string pathToProject, bool writeOut)
    {
        string path = Path.Combine (Path.Combine (pathToProject, "Unity-iPhone.xcodeproj"), "project.pbxproj");
        string xcodeproj = File.ReadAllText (path);

        // 1. Make sure it can run on devices and emus!
        xcodeproj = SetValueOfXCodeGroup ("SDKROOT", xcodeproj, "\"iphoneos\"");

        // 2. Enable Objective C exceptions
        xcodeproj = xcodeproj.Replace("GCC_ENABLE_OBJC_EXCEPTIONS = NO;", "GCC_ENABLE_OBJC_EXCEPTIONS = YES;");

        // 3. Remove Android content that gets injected in the XCode project
        xcodeproj = Regex.Replace (xcodeproj, @"^.*Libraries/Plugins/Android/SwrveSDKPushSupport.*$", "", RegexOptions.Multiline);

        // 4. Add required framewroks for Conversations
        PBXProject project = new PBXProject();
        project.ReadFromString (xcodeproj);
        string targetGuid = project.TargetGuidByName ("Unity-iPhone");
        project.AddFrameworkToProject (targetGuid, "AddressBook.framework", false);
        project.AddFrameworkToProject (targetGuid, "AssetsLibrary.framework", false);
        project.AddFrameworkToProject (targetGuid, "AdSupport.framework", false);
        project.AddFrameworkToProject (targetGuid, "Contacts.framework", true);
        project.AddFrameworkToProject (targetGuid, "Photos.framework", true);

        // 5. Add conversations resources to bundle (to project and to a new PBXResourcesBuildPhase)
        string resourcesProjectPath = "Libraries/Plugins/iOS/SwrveConversationSDK/Resources";
        string resourcesPath = pathToProject + Path.DirectorySeparatorChar + resourcesProjectPath;
        System.IO.Directory.CreateDirectory (resourcesPath);
        string[] resources = System.IO.Directory.GetFiles ("Assets/Plugins/iOS/SwrveConversationSDK/Resources");
        string fileGuids = "";

        if (resources.Length == 0) {
            UnityEngine.Debug.LogError ("Swrve SDK - Could not find any resources. If you want to use Conversations please contact support@swrve.com");
        }

        for (int i = 0; i < resources.Length; i++) {
            string resourcePath = resources [i];
            if (!resourcesPath.EndsWith (".meta")) {
                string resourceFileName = System.IO.Path.GetFileName (resourcePath);
                string newPath = resourcesPath + Path.DirectorySeparatorChar + resourceFileName;
                System.IO.File.Copy (resourcePath, newPath);
                string resourceGuid = project.AddFile (newPath, resourcesProjectPath + Path.DirectorySeparatorChar + resourceFileName);
                project.AddFileToBuild (targetGuid, resourceGuid);
                fileGuids += "        " + resourceGuid + ", /* " + resourceFileName + " */" + Environment.NewLine;
            }
        }
        xcodeproj = project.WriteToString ();

        // Write new PBXResourcesBuildPhase
        string resourcesPhaseGuid = GenerateResourceGuid (xcodeproj, "34D6B58518607986004707B7");
        string newResourcesPhase = Environment.NewLine + "/* Begin Swrve PBXResourcesBuildPhase section */" + Environment.NewLine;
        newResourcesPhase += resourcesPhaseGuid + " /* Swrve Resources */ = {" + Environment.NewLine;
        newResourcesPhase += "    isa = PBXResourcesBuildPhase;" + Environment.NewLine;
        newResourcesPhase += "    buildActionMask = 2147483647;" + Environment.NewLine;
        newResourcesPhase += "    files = (" + Environment.NewLine;
        newResourcesPhase += fileGuids;
        newResourcesPhase += "    );" + Environment.NewLine;
        newResourcesPhase += "    runOnlyForDeploymentPostprocessing = 0;" + Environment.NewLine;
        newResourcesPhase += "};" + Environment.NewLine + "/* End Swrve PBXResourcesBuildPhase section */" + Environment.NewLine;
        // Find injection point as first entry in tbe 'objects = {' entry
        Match resourcesInjectionPoint = Regex.Match(xcodeproj, @"objects(\s)*=(\s)*{");
        if (resourcesInjectionPoint.Success) {
            int injectionPoint = resourcesInjectionPoint.Index + resourcesInjectionPoint.Length;
            xcodeproj = xcodeproj.Insert (injectionPoint, newResourcesPhase);
        } else {
            UnityEngine.Debug.LogError ("Swrve SDK - Could not find injection point for resources in the pbxproj. If you want to use Conversations please contact support@swrve.com");
        }

        // Write changes to the Xcode project
        if (writeOut) {
            File.WriteAllText (path, xcodeproj);
        }

    }

    private static string SetValueOfXCodeGroup (string grouping, string project, string replacewith)
    {
        string pattern = string.Format (@"{0} = .*;$", grouping);
        string replacement = string.Format (@"{0} = {1};", grouping, replacewith);
        Match match = Regex.Match (project, pattern, RegexOptions.Multiline);
        project = Regex.Replace (project, pattern, replacement, RegexOptions.Multiline);
        return project;
    }

    private static string GenerateResourceGuid(string xcodeproj, string defaultGuid)
    {
        string guid = defaultGuid;
        while (xcodeproj.Contains(guid)) {
            guid = Guid.NewGuid().ToString().Replace("-", string.Empty).Substring(0, 24);
        }
        return guid;
    }
}
#endif
