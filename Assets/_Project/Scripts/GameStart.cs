using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HybridCLR;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

public class GameStart : MonoBehaviour
{
    private VersionData localVersion;
    private VersionData serverVersion;

    private bool LoadSuccess = true;

    private int curCheckCount = 0;

    Assembly hotUpdateAssembly;
    System.Object main;
    private void Awake()
    {
        //重定向资源加载位置
        Addressables.InternalIdTransformFunc += TransformFunc;
        //1.加载本地版本号文件
        LoadLocalVersion();
        //2.加载服务器版本号文件
        StartCoroutine(LoadServerVersion());
    }



    private bool CheckZipSize()
    {
        //1.检测压缩包大小
        string hotUpdatePath = Application.persistentDataPath + "/HotUpdate";
        string hotUpdateZipPath = hotUpdatePath + "/" + CONST.HotUpdateFileName;
        long localSize = File.Exists(hotUpdateZipPath) ? new FileInfo(hotUpdateZipPath).Length : 0;
        long serverSize = long.Parse(serverVersion.UpdateZipSize);
        //2.如果大小不一致，丢弃安装包重新下载
        return localSize == serverSize;
    }
    private IEnumerator HotUpdateFaild()
    {
        curCheckCount++;
        if (curCheckCount > CONST.maxCheckCount)
        {
            Debug.LogError($"HotUpdateFaild maxCheckCount {CONST.maxCheckCount} failed");
            yield break;
        }
        LoadSuccess = true;
        //重新解压缩
        yield return StartCoroutine(UnzipHotUpdate());
        if (!LoadSuccess)
        {
            StartCoroutine(HotUpdateFaild());
            yield break;
        }
        //4.加载Dll、调用Dll中的开始方法
        LoadDll();
    }

    private string TransformFunc(IResourceLocation location)
    {
        string internalId = location.InternalId;
        //剔除非AssetBundle资源 不剔除会出现"Assets/_Project/Scenes/Main.unity"这种路径
        if (location.ResourceType != typeof(IAssetBundleResource))
            return internalId;
        internalId = internalId.Replace("\\", "/");
        Debug.Log($"TransformFunc1 {internalId}");
        string fileName = internalId.Split("/aa/").Last();
        Debug.Log($"TransformFunc2 {fileName}");
        string filePath = CONST.UpdateFilePath + fileName;
        Debug.Log($"TransformFunc3 {filePath}");
        if (!File.Exists(filePath))
            filePath = CONST.LocalFilePath + fileName;
        if (!File.Exists(filePath))
        {
            Debug.LogError($"TransformFunc {internalId} not found");
            return "";
        }
        return filePath;
    }
    private void LoadLocalVersion()
    {
        string localVersionPath = Application.persistentDataPath + "/version.json";
        if (!File.Exists(localVersionPath))
        {
            localVersionPath = Application.streamingAssetsPath + "/version.json";
        }
        string jsonStr = File.ReadAllText(localVersionPath);
        localVersion = JsonUtility.FromJson<VersionData>(jsonStr);
    }

    private void SetLocalVersion()
    {
        string localVersionPath = Application.persistentDataPath + "/version.json";
        File.WriteAllText(localVersionPath, JsonUtility.ToJson(localVersion));
    }

    private IEnumerator LoadServerVersion()
    {
        string serverVersionPath = CONST.HotUpdateUrl + "/version.json";
        //1.从CDN下载版号文件，对比版本号是否一致
        using (UnityWebRequest webRequest = UnityWebRequest.Get(serverVersionPath))
        {
            var downloadHandler = new DownloadHandlerBuffer();
            webRequest.downloadHandler = downloadHandler;
            yield return webRequest.SendWebRequest();
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"CheckHotUpdate error: {webRequest.error}");
                yield break;
            }
            string jsonStr = webRequest.downloadHandler.text;
            serverVersion = JsonUtility.FromJson<VersionData>(jsonStr);
        }

        if (localVersion.Version != serverVersion.Version)
        {
            //3.热更新
            yield return StartCoroutine(CheckHotUpdate());
            yield return StartCoroutine(UnzipHotUpdate());
            if (!LoadSuccess)
                StartCoroutine(HotUpdateFaild());
            else
                LoadDll();
        }
        else
        {
            LoadDll();
        }
    }

    private IEnumerator CheckHotUpdate()
    {
        string hotUpdatePath = Application.persistentDataPath + "/HotUpdate";
        if (!Directory.Exists(hotUpdatePath))
            Directory.CreateDirectory(hotUpdatePath);
        //3.从版号文件读取热更新资源位置
        //4.从CDN下载热更新资源包（DownloadHandlerFile 需要完整文件路径，不是目录！）
        string hotUpdateZipPath = hotUpdatePath + "/" + CONST.HotUpdateFileName;
        using (UnityWebRequest webRequest = UnityWebRequest.Get(serverVersion.Update))
        {
            webRequest.downloadHandler = new DownloadHandlerFile(hotUpdateZipPath);
            DownloadHandlerFile downloadHandler = webRequest.downloadHandler as DownloadHandlerFile;
            downloadHandler.removeFileOnAbort = true;
            yield return webRequest.SendWebRequest();
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"CheckHotUpdate error: {webRequest.error}");
                yield break;
            }
        }
        if (!CheckZipSize())
        {
            curCheckCount++;
            if (curCheckCount > CONST.maxCheckCount)
            {
                Debug.LogError($"HotUpdateFaild maxCheckCount {CONST.maxCheckCount} failed");
                yield break;
            }
            yield return CheckHotUpdate();
        }
    }
    private IEnumerator UnzipHotUpdate()
    {
        string persistentRoot = Application.persistentDataPath;
        string hotUpdatePath = Path.Combine(persistentRoot, "HotUpdate");
        string hotUpdateZipPath = Path.Combine(hotUpdatePath, CONST.HotUpdateFileName);
        //5.热更新资源解压缩（zip 内结构为 aa/Windows/xxx.bundle，解压到 persistentDataPath 根目录即可落到正确位置）
        if (!File.Exists(hotUpdateZipPath))
        {
            Directory.CreateDirectory(hotUpdatePath);
            yield break;
        }
        //6.解压缩到 persistentDataPath 根目录（aa/ 结构直接展开）
        ZipUtil.Unzip(hotUpdateZipPath, persistentRoot);

        //7.对比md5码是否一致（MD5.txt 在 zip 根目录，解压后在 persistentDataPath/MD5.txt）
        string md5FilePath = Path.Combine(persistentRoot, CONST.md5FileName);
        if (!File.Exists(md5FilePath))
        {
            Debug.LogError($"CheckHotUpdate error: {md5FilePath} not exist");
            yield break;
        }
        string md5Str = File.ReadAllText(md5FilePath);
        string[] md5Lines = md5Str.Split('\n');
        for (int i = 0; i < md5Lines.Length; i++)
        {
            string line = md5Lines[i];
            line = line.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // 格式: 相对路径:md5（注意路径里可能包含冒号，用 LastIndexOf 分离）
            int sepIdx = line.LastIndexOf(':');
            if (sepIdx < 0) continue;
            string relPath = line.Substring(0, sepIdx);
            string fileMd5 = line.Substring(sepIdx + 1);

            // 拼接完整路径（解压根目录 + 相对路径）
            string fullPath = Path.Combine(persistentRoot, relPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"CheckHotUpdate error: {fullPath} not exist");
                LoadSuccess = false;
                yield break;
            }

            // 用文件 MD5 而非字符串 MD5
            string localMD5 = MD5Util.GetFileMD5(fullPath);
            if (localMD5 != fileMd5)
            {
                Debug.LogError($"CheckHotUpdate error: {relPath} md5 not match (local={localMD5}, server={fileMd5})");
                LoadSuccess = false;
                yield break;
            }
        }

        //8.如果一致，修改本地版本号，清理临时文件
        if (LoadSuccess)
        {
            localVersion.Version = serverVersion.Version;
            SetLocalVersion();
            // 清理 HotUpdate 临时目录和 MD5.txt
            try
            {
                if (Directory.Exists(hotUpdatePath)) Directory.Delete(hotUpdatePath, true);
                if (File.Exists(md5FilePath)) File.Delete(md5FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"清理临时文件失败: {e.Message}");
            }
        }
    }

    private async void LoadDll()
    {
        await LoadMetadataForAOTAssemblies();
#if UNITY_EDITOR
        hotUpdateAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "HotUpdate");
        await Task.Delay(1);
#else
        //1.加载Dll文件
        TextAsset bytes = await LoadBytes("_Project/Bytes/HotUpdate");
        hotUpdateAssembly = Assembly.Load(bytes.bytes);
#endif
        Type type = hotUpdateAssembly.GetType("Main");
        if (type == null)
        {
            Debug.LogError($"LoadDll error: {type} not found");
            return;
        }
        main = Activator.CreateInstance(type);
        //2.调用Dll中的开始方法
        MethodInfo methodInfo = type.GetMethod("Run");
        methodInfo.Invoke(main, null);
    }
    /// <summary>
    /// 补充元数据，因为加载的时候使用了泛型，需要补充元数据才能正常运行
    /// </summary>
    private async Task LoadMetadataForAOTAssemblies()
    {
#if UNITY_EDITOR
        hotUpdateAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "mscorlib");
        await Task.Delay(1);
#else
        //1.加载Dll文件
        TextAsset bytes = await LoadBytes("_Project/Bytes/mscorlib");
        hotUpdateAssembly = Assembly.Load(bytes.bytes);
#endif
    }

    private async Task<TextAsset> LoadBytes(string key)
    {
        var handle = Addressables.LoadAssetAsync<TextAsset>(key);
        await handle.Task;
        return handle.Result;
    }

    void OnDestroy()
    {
        if (hotUpdateAssembly == null || main == null) return;
        Type type = hotUpdateAssembly.GetType("Main");
        MethodInfo methodInfo = type.GetMethod("UnLoad");
        methodInfo.Invoke(main, null);
    }
}