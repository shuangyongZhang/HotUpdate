using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
[CreateAssetMenu(fileName = "AddressableScriptObject", menuName = "Addressable/AddressableScriptObject")]
public class AddressableScriptObject : ScriptableObject
{
    public AddressMatch[] AddressMatches;
}
[Serializable]
public class AddressMatch
{
    public string pathMatch;
    public string pathExclude;
    public string suffixMatch;
    public string groupName;
    public List<AddressPath> AddressPaths = new List<AddressPath>();

    public void SuffixMatch()
    {
        AddressPaths.Clear();
        string assetRoot = Application.dataPath;
        string[] allDirs = Directory.GetDirectories(assetRoot, "*", SearchOption.AllDirectories);
        string[] suffixes = suffixMatch.Split(',');
        string[] pathExcludes = pathExclude.Split(',');
        foreach (string dir in allDirs)
        {
            if (dir.ToLower().Contains(pathMatch.ToLower()) && !pathExcludes.Any(exclude => dir.ToLower().Contains(exclude.ToLower())))
            {
                for (int i = 0; i < suffixes.Length; i++)
                {
                    string[] files = Directory.GetFiles(dir, "*" + suffixes[i], SearchOption.TopDirectoryOnly);
                    foreach (string file in files)
                    {
                        if (file.EndsWith(".meta")) continue; // 跳过 meta
                        string fileName = Path.GetFileName(file);
                        string unityPath = "Assets" + file.Substring(assetRoot.Length).Replace("\\", "/");
                        AddressPath addressPath = new AddressPath();
                        addressPath.fullPath = unityPath;
                        addressPath.fileName = fileName;
                        addressPath.Address = unityPath.Substring("Assets/".Length).Split('.')[0];
                        addressPath.obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(unityPath);
                        AddressPaths.Add(addressPath);
                    }
                }
            }
        }
    }

    public void RefreshAddressData()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        AddressableAssetGroup group = settings.FindGroup(groupName);
        if (group == null)
            group = settings.CreateGroup(groupName, false, false, true, null, new Type[0]);
        else
            ClearGroup(group);
        foreach (AddressPath addressPath in AddressPaths)
        {
            string guid = AssetDatabase.AssetPathToGUID(addressPath.fullPath);
            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.SetAddress(addressPath.Address);
        }
    }
    public void ClearGroup(AddressableAssetGroup group)
    {
        List<AddressableAssetEntry> entries = group.entries.ToList();
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            group.RemoveAssetEntry(entries[i], true);
        }
    }
}

[Serializable]
public class AddressPath
{
    public string fullPath;
    public string fileName;
    public string Address;
    public UnityEngine.Object obj;
}


[CustomEditor(typeof(AddressableScriptObject))]
public class AddressableScriptObjectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        AddressableScriptObject addressableScriptObject = (AddressableScriptObject)target;
        if (GUILayout.Button("Refresh"))
        {
            foreach (AddressMatch addressMatch in addressableScriptObject.AddressMatches)
            {
                addressMatch.SuffixMatch();
                addressMatch.RefreshAddressData();
            }
            EditorUtility.SetDirty(addressableScriptObject);
            AssetDatabase.SaveAssets();
        }
        foreach (AddressMatch addressMatch in addressableScriptObject.AddressMatches)
        {
            if (GUILayout.Button($"Refresh {addressMatch.groupName}"))
            {
                addressMatch.SuffixMatch();
                addressMatch.RefreshAddressData();
                EditorUtility.SetDirty(addressableScriptObject);
                AssetDatabase.SaveAssets();
            }
        }
    }
}