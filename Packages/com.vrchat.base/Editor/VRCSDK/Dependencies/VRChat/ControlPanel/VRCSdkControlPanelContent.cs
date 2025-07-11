﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using VRC.Core;
using VRC.SDKBase.Editor;
using VRC.SDKBase.Editor.Api;

// This file handles the Content tab of the SDK Panel

public partial class VRCSdkControlPanel : EditorWindow
{
    const int PageLimit = 20;

    static List<ApiAvatar> uploadedAvatars = null;
    static List<ApiWorld> uploadedWorlds = null;
    static List<ApiAvatar> testAvatars = null;
    
#if VRC_ENABLE_PROPS
    private static List<VRCProp> uploadedProps = null;
#endif
    
    public static Dictionary<string, Texture2D> ImageCache = new Dictionary<string, Texture2D>();

    static List<string> justDeletedContents;
    static List<ApiAvatar> justUpdatedAvatars;

    static EditorCoroutine fetchingAvatars = null, fetchingWorlds = null;
    
#if VRC_ENABLE_PROPS
    private static bool fetchingProps = false;
#endif

    private static string searchString = "";
    private static bool WorldsToggle = true;
    private static bool AvatarsToggle = true;
    private static bool TestAvatarsToggle = true;

#if VRC_ENABLE_PROPS
    private static bool PropsToggle = true;
#endif
    
    const string WORLDS_WEB_URL = "https://vrchat.com/home/content/worlds";
    const string WORLD_WEB_URL = "https://vrchat.com/home/content/worlds/";
    const string WORLD_WEB_URL_SUFFIX = "/edit";
    const string AVATARS_WEB_URL = "https://vrchat.com/home/avatars";
    const string AVATAR_WEB_URL = "https://vrchat.com/home/avatar/";

    const int SCROLLBAR_RESERVED_REGION_WIDTH = 50;
    const int OPEN_ON_WEB_BUTTON_WIDTH = 100;

    const int WORLD_DESCRIPTION_FIELD_WIDTH = 140;
    const int WORLD_IMAGE_BUTTON_WIDTH = 100;
    const int WORLD_IMAGE_BUTTON_HEIGHT = 100;
    const int WORLD_RELEASE_STATUS_FIELD_WIDTH = 150;
    const int COPY_WORLD_ID_BUTTON_WIDTH = 75;
    const int DELETE_WORLD_BUTTON_WIDTH = 75;
    const int WORLD_ALL_INFORMATION_MAX_WIDTH = WORLD_DESCRIPTION_FIELD_WIDTH + WORLD_IMAGE_BUTTON_WIDTH + WORLD_RELEASE_STATUS_FIELD_WIDTH + COPY_WORLD_ID_BUTTON_WIDTH + DELETE_WORLD_BUTTON_WIDTH + OPEN_ON_WEB_BUTTON_WIDTH + SCROLLBAR_RESERVED_REGION_WIDTH;
    const int WORLD_REDUCED_INFORMATION_MAX_WIDTH = WORLD_DESCRIPTION_FIELD_WIDTH + WORLD_IMAGE_BUTTON_WIDTH + WORLD_RELEASE_STATUS_FIELD_WIDTH + SCROLLBAR_RESERVED_REGION_WIDTH;

    const int AVATAR_DESCRIPTION_FIELD_WIDTH = 140;
    const int AVATAR_IMAGE_BUTTON_WIDTH = WORLD_IMAGE_BUTTON_WIDTH;
    const int AVATAR_IMAGE_BUTTON_HEIGHT = WORLD_IMAGE_BUTTON_HEIGHT;
    const int AVATAR_RELEASE_STATUS_FIELD_WIDTH = 150;
    const int SET_AVATAR_STATUS_BUTTON_WIDTH = 100;
    const int COPY_AVATAR_ID_BUTTON_WIDTH = COPY_WORLD_ID_BUTTON_WIDTH;
    const int DELETE_AVATAR_BUTTON_WIDTH = DELETE_WORLD_BUTTON_WIDTH;
    const int AVATAR_ALL_INFORMATION_MAX_WIDTH = AVATAR_DESCRIPTION_FIELD_WIDTH + AVATAR_IMAGE_BUTTON_WIDTH + AVATAR_RELEASE_STATUS_FIELD_WIDTH + SET_AVATAR_STATUS_BUTTON_WIDTH + COPY_AVATAR_ID_BUTTON_WIDTH + DELETE_AVATAR_BUTTON_WIDTH + OPEN_ON_WEB_BUTTON_WIDTH + SCROLLBAR_RESERVED_REGION_WIDTH;
    const int AVATAR_REDUCED_INFORMATION_MAX_WIDTH = AVATAR_DESCRIPTION_FIELD_WIDTH + AVATAR_IMAGE_BUTTON_WIDTH + AVATAR_RELEASE_STATUS_FIELD_WIDTH + SCROLLBAR_RESERVED_REGION_WIDTH;

    const int MAX_ALL_INFORMATION_WIDTH = WORLD_ALL_INFORMATION_MAX_WIDTH > AVATAR_ALL_INFORMATION_MAX_WIDTH ? WORLD_ALL_INFORMATION_MAX_WIDTH : AVATAR_ALL_INFORMATION_MAX_WIDTH;
    const int MAX_REDUCED_INFORMATION_WIDTH = WORLD_REDUCED_INFORMATION_MAX_WIDTH > AVATAR_REDUCED_INFORMATION_MAX_WIDTH ? WORLD_REDUCED_INFORMATION_MAX_WIDTH : AVATAR_REDUCED_INFORMATION_MAX_WIDTH;

    public static void ClearContent()
    {
        uploadedWorlds = null;
        uploadedAvatars = null;
        testAvatars = null;
#if VRC_ENABLE_PROPS
        uploadedProps = null;
        fetchingProps = false;
#endif
        ImageCache.Clear();
    }

    IEnumerator FetchUploadedData()
    {
        if (!ConfigManager.RemoteConfig.IsInitialized())
            ConfigManager.RemoteConfig.Init();

        if (!APIUser.IsLoggedIn)
            yield break;

        ApiCache.Clear();
        VRCCachedWebRequest.ClearOld();

        if (fetchingAvatars == null)
            fetchingAvatars = EditorCoroutine.Start(() => FetchAvatars());
        if (fetchingWorlds == null)
            fetchingWorlds = EditorCoroutine.Start(() => FetchWorlds());
        FetchTestAvatars();
    }

    private static void FetchAvatars(int offset = 0)
    {
        ApiAvatar.FetchList(
            delegate (IEnumerable<ApiAvatar> obj, bool _)
            {
                if (obj.FirstOrDefault() != null)
                    fetchingAvatars = EditorCoroutine.Start(() =>
                    {
                        var l = obj.ToList();
                        int count = l.Count;
                        SetupAvatarData(l);
                        FetchAvatars(offset + count);
                    });
                else
                {
                    fetchingAvatars = null;
                    foreach (ApiAvatar a in uploadedAvatars)
                        DownloadImage(a.id, a.thumbnailImageUrl);
                }
            },
            delegate (string obj)
            {
                Debug.LogError("Error fetching your uploaded avatars:\n" + obj);
                fetchingAvatars = null;
            },
            ApiAvatar.Owner.Mine,
            ApiAvatar.ReleaseStatus.All,
            null,
            PageLimit,
            offset,
            ApiAvatar.SortHeading.None,
            ApiAvatar.SortOrder.Descending,
            null,
            null,
            true,
            false,
            null,
            false
            );
    }

    private static void FetchTestAvatars()
    {
#if VRC_SDK_VRCSDK3
        string sdkAvatarFolder = VRC.SDKBase.Editor.VRC_SdkBuilder.GetLocalLowPath() + "/VRChat/VRChat/Avatars/";
        string[] sdkavatars = Directory.GetFiles(sdkAvatarFolder);
        string filename = "";
        List<ApiAvatar> avatars = new List<ApiAvatar>();
        foreach (string sdkap in sdkavatars)
        {
            if (Path.GetExtension(sdkap) != ".vrca")
                continue;

            filename = Path.GetFileNameWithoutExtension(sdkap);
            ApiAvatar sdka = API.FromCacheOrNew<ApiAvatar>("local:sdk_" + filename);
            sdka.assetUrl = sdkap;
            sdka.name = filename;
            sdka.releaseStatus = "public";
            ApiAvatar.AddLocal(sdka);
            avatars.Add(sdka);
        }

        testAvatars = avatars;
#else
        testAvatars = new List<ApiAvatar>();
#endif
    }

#if VRC_ENABLE_PROPS
    private static async Task<List<VRCProp>> FetchProps()
    {
        fetchingProps = true;
        var offset = 0;
        List<VRCProp> propsResponse;
        var fetchedProps = new List<VRCProp>();
        do
        {
            try
            {
                propsResponse = await VRCApi.GetProps(100, offset, true);
                foreach (var prop in propsResponse)
                {
                    var image = await VRCApi.GetImage(prop.ThumbnailImageUrl);
                    if (image != null)
                    {
                        ImageCache[prop.ID] = image;
                    }
                }

                fetchedProps.AddRange(propsResponse);
                offset += 100;
                propsResponse.Clear();
            }
            catch (ApiErrorException e)
            {
                Debug.LogError($"[{e.StatusCode}] Error fetching props: {e.ErrorMessage}");
                break;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                break;
            }
        } while (propsResponse.Count > 0);
        
        fetchingProps = false;

        uploadedProps = fetchedProps;
        return fetchedProps;
    }
#endif

    private static void FetchWorlds(int offset = 0)
    {
        ApiWorld.FetchList(
            delegate (IEnumerable<ApiWorld> obj)
            {
                if (obj.FirstOrDefault() != null)
                    fetchingWorlds = EditorCoroutine.Start(() =>
                    {
                        var l = obj.ToList();
                        int count = l.Count;
                        SetupWorldData(l);
                        FetchWorlds(offset + count);
                    });
                else
                {
                    fetchingWorlds = null;

                    foreach (ApiWorld w in uploadedWorlds)
                        DownloadImage(w.id, w.thumbnailImageUrl);
                }
            },
            delegate (string obj)
            {
                Debug.LogError("Error fetching your uploaded worlds:\n" + obj);
                fetchingWorlds = null;
            },
            "updated",
            ApiWorld.SortOwnership.Mine,
            ApiWorld.SortOrder.Descending,
            offset,
            PageLimit,
            "",
            null,
            null,
            null,
            null,
            "",
            ApiWorld.ReleaseStatus.All,
            null,
            null,
            true,
            false);
    }

    static void SetupWorldData(List<ApiWorld> worlds)
    {
        if (worlds == null || uploadedWorlds == null)
            return;

        worlds.RemoveAll(w => w == null || w.name == null || uploadedWorlds.Any(w2 => w2.id == w.id));

        if (worlds.Count > 0)
        {
            uploadedWorlds.AddRange(worlds);
            uploadedWorlds.Sort((w1, w2) => -w1.updated_at.CompareTo(w2.updated_at));
        }
    }

    static void SetupAvatarData(List<ApiAvatar> avatars)
    {
        if (avatars == null || uploadedAvatars == null)
            return;

        avatars.RemoveAll(a => a == null || uploadedAvatars.Any(a2 => a2.id == a.id));
        foreach (var avatar in avatars)
        {
            if (string.IsNullOrEmpty(avatar.name))
                avatar.name = "(unnamed)";
        }

        if (avatars.Count > 0)
        {
            uploadedAvatars.AddRange(avatars);
            uploadedAvatars.Sort((a1, a2) => -a1.updated_at.CompareTo(a2.updated_at));
        }
    }

    private static void DownloadImage(string id, string url)
    {
        if (string.IsNullOrEmpty(url))
            return;

        if (ImageCache.ContainsKey(id) && ImageCache[id] != null)
            return;

        EditorCoroutine.Start(VRCCachedWebRequest.Get(url, OnDone));
        void OnDone(Texture2D texture)
        {
            if (texture != null)
                ImageCache[id] = texture;
            else if (ImageCache.ContainsKey(id))
                ImageCache.Remove(id);
        }
    }

    Vector2 contentScrollPos;

    bool OnGUIUserInfo()
    {
        bool updatedContent = false;

        if (!ConfigManager.RemoteConfig.IsInitialized())
            ConfigManager.RemoteConfig.Init();

        if (APIUser.IsLoggedIn && uploadedWorlds != null && uploadedAvatars != null && testAvatars != null)
        {
            bool expandedLayout = false; // (position.width > MAX_ALL_INFORMATION_WIDTH);    // uncomment for future wide layouts

            if (!expandedLayout)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
            }

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(searchBarStyle);

            EditorGUILayout.BeginHorizontal();

            float searchFieldShrinkOffset = 30f;
            GUILayoutOption layoutOption = (expandedLayout ? GUILayout.Width(position.width - searchFieldShrinkOffset) : GUILayout.Width(SdkWindowWidth - searchFieldShrinkOffset - 8));
            searchString = EditorGUILayout.TextField(searchString, GUI.skin.FindStyle("SearchTextField"), layoutOption);
            GUIStyle searchButtonStyle = searchString == string.Empty
                ? GUI.skin.FindStyle("SearchCancelButtonEmpty")
                : GUI.skin.FindStyle("SearchCancelButton");
            if (GUILayout.Button(string.Empty, searchButtonStyle))
            {
                searchString = string.Empty;
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            if (!expandedLayout)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
            }

            layoutOption = expandedLayout ? GUILayout.Width(position.width) : GUILayout.Width(SdkWindowWidth - 8);
            
            using (var scroll = new EditorGUILayout.ScrollViewScope(contentScrollPos, layoutOption))
            {
                contentScrollPos = scroll.scrollPosition;
                
                #if UDON
                if (uploadedWorlds.Count > 0)
                {
                    WorldsListGUI(expandedLayout, ref updatedContent);  
                }
                
                if (uploadedAvatars.Count > 0)
                {
                    AvatarsListGUI(expandedLayout, ref updatedContent);
                }

#if VRC_ENABLE_PROPS
                if (uploadedProps?.Count > 0)
                {
                    PropsListGUI(ref updatedContent);
                }
#endif
                
                if (testAvatars.Count > 0)
                {
                    TestAvatarsListGUI(expandedLayout, ref updatedContent);
                }
                #else
                if (uploadedAvatars.Count > 0)
                {
                    AvatarsListGUI(expandedLayout, ref updatedContent);
                }
                
                if (testAvatars.Count > 0)
                {
                    TestAvatarsListGUI(expandedLayout, ref updatedContent);
                }
                
                if (uploadedWorlds.Count > 0)
                {
                    WorldsListGUI(expandedLayout, ref updatedContent);  
                }
                #endif
            }
            
            if (!expandedLayout)
            {
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            if (updatedContent && (null != window)) window.Reset();

            return true;
        }
        else
        {
            return false;
        }
    }

    private void AvatarsListGUI(bool expandedLayout, ref bool updatedContent)
    {
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Avatars", EditorStyles.boldLabel, GUILayout.ExpandWidth(false),
            GUILayout.Width(65));
        AvatarsToggle = EditorGUILayout.Foldout(AvatarsToggle, new GUIContent(""));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (AvatarsToggle)
        {
            List<ApiAvatar> tmpAvatars = new List<ApiAvatar>();

            if (uploadedAvatars.Count > 0)
                tmpAvatars = new List<ApiAvatar>(uploadedAvatars);

            if (justUpdatedAvatars != null)
            {
                foreach (ApiAvatar a in justUpdatedAvatars)
                {
                    int index = tmpAvatars.FindIndex((av) => av.id == a.id);
                    if (index != -1)
                        tmpAvatars[index] = a;
                }
            }

            foreach (ApiAvatar a in tmpAvatars)
            {
                if (justDeletedContents != null && justDeletedContents.Contains(a.id))
                {
                    uploadedAvatars.Remove(a);
                    continue;
                }

                if (!a.name.ToLowerInvariant().Contains(searchString.ToLowerInvariant()))
                    continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                if (ImageCache.ContainsKey(a.id))
                {
                    if (GUILayout.Button(ImageCache[a.id], GUILayout.Height(AVATAR_IMAGE_BUTTON_HEIGHT),
                            GUILayout.Width(AVATAR_IMAGE_BUTTON_WIDTH)))
                        Application.OpenURL(a.imageUrl);
                }
                else
                {
                    if (GUILayout.Button("", GUILayout.Height(AVATAR_IMAGE_BUTTON_HEIGHT),
                            GUILayout.Width(AVATAR_IMAGE_BUTTON_WIDTH)))
                        Application.OpenURL(a.imageUrl);
                }

                if (expandedLayout)
                    EditorGUILayout.BeginHorizontal();
                else
                    EditorGUILayout.BeginVertical();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(a.name, contentTitleStyle);
                if (GUILayout.Button("Open on web", GUILayout.Width(OPEN_ON_WEB_BUTTON_WIDTH)))
                    Application.OpenURL(AVATAR_WEB_URL + a.id);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("Release Status: " + a.releaseStatus,
                    GUILayout.Width(AVATAR_RELEASE_STATUS_FIELD_WIDTH));

                string oppositeReleaseStatus = a.releaseStatus == "public" ? "private" : "public";
                if (GUILayout.Button("Make " + oppositeReleaseStatus,
                        GUILayout.Width(SET_AVATAR_STATUS_BUTTON_WIDTH)))
                {
                    a.releaseStatus = oppositeReleaseStatus;

                    a.SaveReleaseStatus((c) =>
                        {
                            ApiAvatar savedBP = (ApiAvatar) c.Model;

                            if (justUpdatedAvatars == null) justUpdatedAvatars = new List<ApiAvatar>();
                            justUpdatedAvatars.Add(savedBP);
                        },
                        (c) =>
                        {
                            Debug.LogError(c.Error);
                            EditorUtility.DisplayDialog("Avatar Updated",
                                "Failed to change avatar release status", "OK");
                        });
                }

                if (GUILayout.Button("Copy ID", GUILayout.Width(COPY_AVATAR_ID_BUTTON_WIDTH)))
                {
                    TextEditor te = new TextEditor();
                    te.text = a.id;
                    te.SelectAll();
                    te.Copy();
                }

                if (GUILayout.Button("Delete", GUILayout.Width(DELETE_AVATAR_BUTTON_WIDTH)))
                {
                    if (EditorUtility.DisplayDialog("Delete " + a.name + "?",
                            "Are you sure you want to delete " + a.name + "? This cannot be undone.", "Delete",
                            "Cancel"))
                    {
                        foreach (VRC.Core.PipelineManager pm in FindObjectsOfType<VRC.Core.PipelineManager>()
                                     .Where(pm => pm.blueprintId == a.id))
                        {
                            pm.blueprintId = "";

                            EditorUtility.SetDirty(pm);
                            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pm.gameObject.scene);
                            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(pm.gameObject.scene);
                        }

                        API.Delete<ApiAvatar>(a.id);
                        uploadedAvatars.RemoveAll(avatar => avatar.id == a.id);
                        if (ImageCache.ContainsKey(a.id))
                            ImageCache.Remove(a.id);

                        if (justDeletedContents == null) justDeletedContents = new List<string>();
                        justDeletedContents.Add(a.id);
                        updatedContent = true;
                    }
                }

                if (expandedLayout)
                    EditorGUILayout.EndHorizontal();
                else
                    EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
            }
        }
    }
#if VRC_ENABLE_PROPS
    private void PropsListGUI(ref bool updatedContent)
    {
        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Props", EditorStyles.boldLabel, GUILayout.ExpandWidth(false),
                GUILayout.Width(65));
            PropsToggle = EditorGUILayout.Foldout(PropsToggle, new GUIContent(""));
        }

        EditorGUILayout.Space();

        if (PropsToggle)
        {
            foreach (var prop in uploadedProps)
            {
                if (!prop.Name.ToLowerInvariant().Contains(searchString.ToLowerInvariant()))
                    continue;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    if (ImageCache.ContainsKey(prop.ID))
                    {
                        if (GUILayout.Button(ImageCache[prop.ID], GUILayout.Height(AVATAR_IMAGE_BUTTON_HEIGHT),
                                GUILayout.Width(AVATAR_IMAGE_BUTTON_WIDTH)))
                            Application.OpenURL(prop.ImageUrl);
                    }
                    else
                    {
                        if (GUILayout.Button("", GUILayout.Height(AVATAR_IMAGE_BUTTON_HEIGHT),
                                GUILayout.Width(AVATAR_IMAGE_BUTTON_WIDTH)))
                            Application.OpenURL(prop.ImageUrl);
                    }

                    using (new EditorGUILayout.VerticalScope())
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(prop.Name, contentTitleStyle);
                            if (GUILayout.Button("Open on web", GUILayout.Width(OPEN_ON_WEB_BUTTON_WIDTH)))
                                Application.OpenURL(AVATAR_WEB_URL + prop.ID);
                        }
                        
                        if (GUILayout.Button("Copy ID", GUILayout.Width(COPY_AVATAR_ID_BUTTON_WIDTH)))
                        {
                            TextEditor te = new TextEditor();
                            te.text = prop.ID;
                            te.SelectAll();
                            te.Copy();
                        }

                        if (GUILayout.Button("Delete", GUILayout.Width(DELETE_AVATAR_BUTTON_WIDTH)))
                        {
                            if (EditorUtility.DisplayDialog("Delete " + prop.Name + "?",
                                    "Are you sure you want to delete " + prop.Name + "? This cannot be undone.", "Delete",
                                    "Cancel"))
                            {
                                foreach (VRC.Core.PipelineManager pm in FindObjectsOfType<VRC.Core.PipelineManager>()
                                             .Where(pm => pm.blueprintId == prop.ID))
                                {
                                    pm.blueprintId = "";

                                    EditorUtility.SetDirty(pm);
                                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pm.gameObject.scene);
                                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(pm.gameObject.scene);
                                }
                                
                                VRCApi.Delete("props/" + prop.ID).ConfigureAwait(false);
                                uploadedProps.RemoveAll(p => p.ID == prop.ID);
                                if (ImageCache.ContainsKey(prop.ID))
                                    ImageCache.Remove(prop.ID);
                                
                                updatedContent = true;
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
#endif
    
    private void TestAvatarsListGUI(bool expandedLayout, ref bool updatedContent)
    {
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Test Avatars", EditorStyles.boldLabel, GUILayout.ExpandWidth(false),
            GUILayout.Width(100));
        TestAvatarsToggle = EditorGUILayout.Foldout(TestAvatarsToggle, new GUIContent(""));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (TestAvatarsToggle)
        {
            List<ApiAvatar> tmpAvatars = new List<ApiAvatar>();

            if (testAvatars.Count > 0)
                tmpAvatars = new List<ApiAvatar>(testAvatars);

            foreach (ApiAvatar a in tmpAvatars)
            {
                if (!a.name.ToLowerInvariant().Contains(searchString.ToLowerInvariant()))
                    continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                if (expandedLayout)
                    EditorGUILayout.BeginHorizontal();
                else
                    EditorGUILayout.BeginVertical();

                EditorGUILayout.LabelField(a.name, contentDescriptionStyle,
                    GUILayout.Width(expandedLayout
                        ? position.width - MAX_ALL_INFORMATION_WIDTH + AVATAR_DESCRIPTION_FIELD_WIDTH
                        : AVATAR_DESCRIPTION_FIELD_WIDTH));

                if (GUILayout.Button("Delete", GUILayout.Width(DELETE_AVATAR_BUTTON_WIDTH)))
                {
                    if (EditorUtility.DisplayDialog("Delete " + a.name + "?",
                            "Are you sure you want to delete " + a.name + "? This cannot be undone.", "Delete",
                            "Cancel"))
                    {
                        API.Delete<ApiAvatar>(a.id);
                        testAvatars.RemoveAll(avatar => avatar.id == a.id);
                        File.Delete(a.assetUrl);

                        updatedContent = true;
                    }
                }

                if (expandedLayout)
                    EditorGUILayout.EndHorizontal();
                else
                    EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
            }
        }
    }
    
    private void WorldsListGUI(bool expandedLayout, ref bool updatedContent)
    {
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Worlds", EditorStyles.boldLabel, GUILayout.ExpandWidth(false), GUILayout.Width(58));
        WorldsToggle = EditorGUILayout.Foldout(WorldsToggle, new GUIContent(""));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (WorldsToggle)
        {
            List<ApiWorld> tmpWorlds = new List<ApiWorld>();

            if (uploadedWorlds.Count > 0)
                tmpWorlds = new List<ApiWorld>(uploadedWorlds);

            foreach (ApiWorld w in tmpWorlds)
            {
                if (justDeletedContents != null && justDeletedContents.Contains(w.id))
                {
                    uploadedWorlds.Remove(w);
                    continue;
                }

                if (!w.name.ToLowerInvariant().Contains(searchString.ToLowerInvariant()))
                    continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                if (ImageCache.ContainsKey(w.id))
                {
                    if (GUILayout.Button(ImageCache[w.id], GUILayout.Height(WORLD_IMAGE_BUTTON_HEIGHT),
                            GUILayout.Width(WORLD_IMAGE_BUTTON_WIDTH)))
                        Application.OpenURL(w.imageUrl);
                }
                else
                {
                    if (GUILayout.Button("", GUILayout.Height(WORLD_IMAGE_BUTTON_HEIGHT),
                            GUILayout.Width(WORLD_IMAGE_BUTTON_WIDTH)))
                        Application.OpenURL(w.imageUrl);
                }

                if (expandedLayout)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(w.name, contentDescriptionStyle,
                        GUILayout.Width(position.width - MAX_ALL_INFORMATION_WIDTH +
                                        WORLD_DESCRIPTION_FIELD_WIDTH));
                }
                else
                {
                    EditorGUILayout.BeginVertical();

                    EditorGUILayout.BeginHorizontal();
                    #if UDON
                    if (w.id == _currentBlueprintId)
                    {
                        EditorGUILayout.LabelField(w.name + " (Current)", contentTitleStyle);
                    }
                    else
                    {
                        EditorGUILayout.LabelField(w.name, contentTitleStyle);
                    }
                    #else
                    EditorGUILayout.LabelField(w.name, contentTitleStyle);
                    #endif

                    if (GUILayout.Button("Open on web", GUILayout.Width(OPEN_ON_WEB_BUTTON_WIDTH)))
                        Application.OpenURL(WORLD_WEB_URL + w.id + WORLD_WEB_URL_SUFFIX);
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.LabelField("Release Status: " + w.releaseStatus,
                    GUILayout.Width(WORLD_RELEASE_STATUS_FIELD_WIDTH));

                EditorGUILayout.Space(5);

                using (new GUILayout.HorizontalScope())
                {
                    #if UDON
                    if (GUILayout.Button("Set Current", GUILayout.Width(COPY_WORLD_ID_BUTTON_WIDTH)))
                    {
                        var pM = FindObjectOfType<PipelineManager>();
                        if (pM != null)
                        {
                            Undo.RecordObject(pM, "Set Current World");
                            pM.blueprintId = w.id;
                            _currentBlueprintId = w.id;
                        }
                    }
                    #endif
                    if (GUILayout.Button("Copy ID", GUILayout.Width(COPY_WORLD_ID_BUTTON_WIDTH)))
                    {
                        TextEditor te = new TextEditor();
                        te.text = w.id;
                        te.SelectAll();
                        te.Copy();
                    }
                }

                if (GUILayout.Button("Delete", GUILayout.Width(DELETE_WORLD_BUTTON_WIDTH)))
                {
                    if (EditorUtility.DisplayDialog("Delete " + w.name + "?",
                            "Are you sure you want to delete " + w.name + "? This cannot be undone.", "Delete",
                            "Cancel"))
                    {
                        foreach (VRC.Core.PipelineManager pm in FindObjectsOfType<VRC.Core.PipelineManager>()
                                     .Where(pm => pm.blueprintId == w.id))
                        {
                            pm.blueprintId = "";

                            EditorUtility.SetDirty(pm);
                            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pm.gameObject.scene);
                            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(pm.gameObject.scene);
                        }

                        API.Delete<ApiWorld>(w.id);
                        uploadedWorlds.RemoveAll(world => world.id == w.id);
                        if (ImageCache.ContainsKey(w.id))
                            ImageCache.Remove(w.id);

                        if (justDeletedContents == null) justDeletedContents = new List<string>();
                        justDeletedContents.Add(w.id);
                        updatedContent = true;
                    }
                }

                if (expandedLayout)
                    EditorGUILayout.EndHorizontal();
                else
                    EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
            }
        }
    }
    
    private string _currentBlueprintId;
    private void FetchCurrentBlueprintId()
    {
        #if UDON
        var pM = FindObjectOfType<PipelineManager>();
        _currentBlueprintId = pM != null ? pM.blueprintId : null;
        #endif
    }

    void ShowContent()
    {
        GUIStyle centeredDescriptionStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
        centeredDescriptionStyle.wordWrap = true;
        centeredDescriptionStyle.alignment = TextAnchor.MiddleCenter;

        FetchCurrentBlueprintId();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical();

        GUILayout.BeginVertical(infoGuiStyle, GUILayout.Width(SdkWindowWidth));
        EditorGUILayout.LabelField("We recommend that you use the VRChat website to manage your content.", centeredDescriptionStyle);
        EditorGUILayout.Space();
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Worlds", GUILayout.Width(OPEN_ON_WEB_BUTTON_WIDTH)))
            Application.OpenURL(WORLDS_WEB_URL);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Avatars", GUILayout.Width(OPEN_ON_WEB_BUTTON_WIDTH)))
            Application.OpenURL(AVATARS_WEB_URL);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        if (((PanelTab) VRCSettings.ActiveWindowPanel) == PanelTab.ContentManager)
        {
            if (uploadedWorlds == null || uploadedAvatars == null || testAvatars == null)
            {
                if (uploadedWorlds == null)
                    uploadedWorlds = new List<ApiWorld>();
                if (uploadedAvatars == null)
                    uploadedAvatars = new List<ApiAvatar>();
                if (testAvatars == null)
                    testAvatars = new List<ApiAvatar>();

                EditorCoroutine.Start(FetchUploadedData());
            }

#if VRC_ENABLE_PROPS
            if (uploadedProps == null)
            {
                if (!fetchingProps && APIUser.CurrentUser != null && !string.IsNullOrEmpty(APIUser.CurrentUser.id))
                {
                    FetchProps().ConfigureAwait(false);
                }
            }
#endif

            if (
                fetchingWorlds != null
                || fetchingAvatars != null
#if VRC_ENABLE_PROPS
                || fetchingProps
#endif
            )
            {
                GUILayout.BeginVertical(boxGuiStyle, GUILayout.Width(SdkWindowWidth - 8));
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Fetching Records", titleGuiStyle);
                EditorGUILayout.Space();
                GUILayout.EndVertical();
            }
            else
            {
                GUILayout.BeginVertical(boxGuiStyle, GUILayout.Width(SdkWindowWidth - 8));
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Fetch updated records from the VRChat server");
                if (GUILayout.Button("Fetch"))
                    ClearContent();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
                GUILayout.EndVertical();
            }

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            OnGUIUserInfo();
        }

    }
}
