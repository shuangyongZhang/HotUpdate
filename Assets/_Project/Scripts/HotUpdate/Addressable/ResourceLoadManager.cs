using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Objece = System.Object;

public class ResourceLoadManager : InstanceBase<ResourceLoadManager>
{
    private Dictionary<Objece, AsyncOperationHandle> _loadDict = new Dictionary<Objece, AsyncOperationHandle>();

    private void AddOrSetLoadDict(AsyncOperationHandle handle, Objece obj)
    {
        if (_loadDict.ContainsKey(obj))
            _loadDict[obj] = handle;
        else
            _loadDict.Add(obj, handle);
    }
    private async Task<GameObject> InstantiateAsync(string key, Transform parent = null, bool worldPosition = false, bool keepOriginalParent = false)
    {
        var go = Addressables.InstantiateAsync(key, parent, worldPosition, keepOriginalParent);
        await go.Task;
        AddOrSetLoadDict(go, go.Result);
        return go.Result;
    }
    public async Task<GameObject> InstantiateModelAsync(string key, Transform parent = null, bool worldPosition = false, bool keepOriginalParent = false)
    {
        string modelPath = $"_Project/Prefabs/Model/{key}";
        return await InstantiateAsync(modelPath, parent, worldPosition, keepOriginalParent);
    }

    public async Task<GameObject> InstantiateUIAsync(string key, Transform parent = null, bool worldPosition = false, bool keepOriginalParent = false)
    {
        string uiPath = $"_Project/Prefabs/UI/{key}";
        return await InstantiateAsync(uiPath, parent, worldPosition, keepOriginalParent);
    }

    private void Instantiate(string key, Action<GameObject> onComplete, Transform parent = null, bool worldPosition = false, bool keepOriginalParent = false)
    {
        var handle = Addressables.InstantiateAsync(key, parent, worldPosition, keepOriginalParent);
        handle.Completed += (obj) =>
        {
            if (obj.Status == AsyncOperationStatus.Succeeded)
            {
                AddOrSetLoadDict(handle, handle.Result);
                onComplete?.Invoke(handle.Result);
            }
            else
                Debug.LogError($"Instantiate {key} failed");
        };
    }

    public void InstantiateUI(string key, Action<GameObject> onComplete, Transform parent = null, bool worldPosition = false, bool keepOriginalParent = false)
    {
        string uiPath = $"_Project/Prefabs/UI/{key}";
        Instantiate(uiPath, onComplete, parent, worldPosition, keepOriginalParent);
    }

    public void InstantiateModel(string key, Action<GameObject> onComplete, Transform parent = null, bool worldPosition = false, bool keepOriginalParent = false)
    {
        string modelPath = $"_Project/Prefabs/Model/{key}";
        Instantiate(modelPath, onComplete, parent, worldPosition, keepOriginalParent);
    }

    public async Task<T> LoadAsync<T>(string key)
    {
        var handle = Addressables.LoadAssetAsync<T>(key);
        await handle.Task;
        AddOrSetLoadDict(handle, handle.Result);
        return handle.Result;
    }

    public void Load<T>(string key, Action<T> onComplete)
    {
        var handle = Addressables.LoadAssetAsync<T>(key);
        handle.Completed += (obj) =>
        {
            if (obj.Status == AsyncOperationStatus.Succeeded)
            {
                AddOrSetLoadDict(handle, handle.Result);
                onComplete?.Invoke(handle.Result);
            }
            else
                Debug.LogError($"Load {key} failed");
        };
    }

    public async Task<SceneInstance> LoadSceneAsync(string key, LoadSceneMode mode)
    {
        string scenePath = $"_Project/Scenes/{key}";
        var handle = Addressables.LoadSceneAsync(scenePath, mode);
        await handle.Task;
        AddOrSetLoadDict(handle, handle.Result);
        return handle.Result;
    }
    public async Task UnLoadSceneAsync(SceneInstance scene)
    {
        var handle = Addressables.UnloadSceneAsync(scene, true);
        await handle.Task;
    }

    public void LoadScene(string key, LoadSceneMode mode, Action<Scene> onComplete)
    {
        string scenePath = $"_Project/Scenes/{key}";
        try
        {
            var handle = Addressables.LoadSceneAsync(scenePath, mode);
            handle.Completed += (obj) =>
            {
                if (obj.Status == AsyncOperationStatus.Succeeded)
                {
                    AddOrSetLoadDict(handle, handle.Result);
                    onComplete?.Invoke(handle.Result.Scene);
                }
                else
                    Debug.LogError($"Load {scenePath} failed");
            };
        }
        catch (Exception e)
        {
            Debug.LogError($"Load {scenePath} failed: {e.Message}");
        }
    }

    public void LoadTexture(string key, Action<Texture2D> onComplete)
    {
        string texturePath = $"_Project/Texture/{key}";
        Load<Texture2D>(texturePath, onComplete);
    }
    public async Task<Texture2D> LoadTextureAsync(string key)
    {
        string texturePath = $"_Project/Texture/{key}";
        return await LoadAsync<Texture2D>(texturePath);
    }

    private void UnloadSingle(Objece obj)
    {
        if (obj is GameObject go)
        {
            UnInstantiate(go);
        }
        else if (obj is SceneInstance scene)
        {
            UnLoadSceneAsync(scene);
        }
        else
        {
            Addressables.Release(_loadDict[obj]);
        }
    }
    public void Unload(Objece obj)
    {
        if (_loadDict.ContainsKey(obj))
        {
            UnloadSingle(obj);
            _loadDict.Remove(obj);
        }
    }
    private void UnInstantiate(GameObject go)
    {
        if (_loadDict.ContainsKey(go))
        {
            Addressables.ReleaseInstance(go);
        }
    }

    public void UnloadAll()
    {
        foreach (var item in _loadDict)
        {
            UnloadSingle(item.Key);
        }
        _loadDict.Clear();
    }
}