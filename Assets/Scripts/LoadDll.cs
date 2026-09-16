using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

public class LoadDll : MonoBehaviour
{
    private static LoadDll _instance;
    public static LoadDll Instance => _instance;

    public IHello iHello;
    public void Awake()
    {
        _instance = this;
        StartCoroutine(LoadNewDll());
    }

    IEnumerator LoadNewDll()
    {
        Assembly assembly;
#if !UNITY_EDITOR
        yield return LoadFile("HotUpdate.dll.bytes", "http://192.168.0.47:82/WebGL/HotUpdate.dll.bytes");   
        assembly =  Assembly.Load(File.ReadAllBytes($"{Application.persistentDataPath}/HotUpdate.dll.bytes"));
#else
        assembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "HotUpdate");
#endif
        yield return null;
        Type type = assembly.GetType("Hello");
        object instance = Activator.CreateInstance(type) as IHello;
        iHello = instance as IHello;
    }

    IEnumerator LoadFile(string srcPath, string dstPath)
    {
        string savePath = Path.Combine(Application.streamingAssetsPath, srcPath);
        using (UnityWebRequest webRequest = UnityWebRequest.Get(dstPath))
        {
            webRequest.downloadHandler = new DownloadHandlerFile(savePath);
            DownloadHandlerFile downloadHandler = webRequest.downloadHandler as DownloadHandlerFile;
            downloadHandler.removeFileOnAbort = true;
            yield return webRequest.SendWebRequest();
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"Download {srcPath} to {savePath} success");
            }
            else
            {
                Debug.LogError($"Download {srcPath} to {savePath} failed");
            }
        }
    }
}