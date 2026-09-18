using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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

        string fileName = internalId.Split("/aa/").Last();
        string filePath = CONST.UpdateFilePath + fileName;
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
        //4.从CDN下载热更新资源包
        using (UnityWebRequest webRequest = UnityWebRequest.Get(serverVersion.Update))
        {
            webRequest.downloadHandler = new DownloadHandlerFile(hotUpdatePath);
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
        string hotUpdatePath = Application.persistentDataPath + "/HotUpdate";
        //5.热更新资源解压缩
        string hotUpdateZipPath = hotUpdatePath + "/" + CONST.HotUpdateFileName;
        if (!File.Exists(hotUpdateZipPath))
        {
            Debug.LogError($"CheckHotUpdate error: {hotUpdateZipPath} not exist");
            yield break;
        }
        //6.解压缩
        ZipUtil.Unzip(hotUpdateZipPath, hotUpdatePath);
        //7.对比md5码是否一致
        string md5FilePath = hotUpdatePath + "/" + CONST.md5FileName;
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
            if (string.IsNullOrEmpty(line)) continue;
            string[] lineParts = line.Split(':');
            if (lineParts.Length != 2) continue;
            string filePath = lineParts[0];
            string fileMd5 = lineParts[1];
            if (!File.Exists(filePath))
            {
                Debug.LogError($"CheckHotUpdate error: {filePath} not exist");
                LoadSuccess = false;
                yield break;
            }
            string localMD5 = MD5Util.GetMD5(filePath);
            if (localMD5 != fileMd5)
            {
                Debug.LogError($"CheckHotUpdate error: {filePath} md5 not match");
                LoadSuccess = false;
                yield break;
            }
        }
        //8.如果不一致，再次下载解压缩，重复3次，3次后使用当前文件
        //9.如果一致，修改本地版本号，删除压缩包，删除md5码文件
        if (LoadSuccess)
        {
            localVersion.Version = serverVersion.Version;
            SetLocalVersion();
        }
    }

    private void LoadDll()
    {
        Assembly hotUpdateAss;
#if UNITY_EDITOR
        hotUpdateAss = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "HotUpdate");
#else
        //1.加载Dll文件
        string hotUpdatePath = Application.persistentDataPath + "/HotUpdate.dll.bytes";
        if (!File.Exists(hotUpdatePath))
        {
            hotUpdatePath = Application.streamingAssetsPath + "/HotUpdate.dll.bytes";
        }
        hotUpdateAss = Assembly.Load(File.ReadAllBytes(hotUpdatePath));
#endif
        Type type = hotUpdateAss.GetType("Main");
        if (type == null)
        {
            Debug.LogError($"LoadDll error: {type} not found");
            return;
        }
        var instance = Activator.CreateInstance(type);
        //2.调用Dll中的开始方法
        MethodInfo methodInfo = type.GetMethod("Run");
        methodInfo.Invoke(instance, null);
    }

    void OnDestroy()
    {
        ResourceLoadManager.Instance.UnloadAll();
    }
}