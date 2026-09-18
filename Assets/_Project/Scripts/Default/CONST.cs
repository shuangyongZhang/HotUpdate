using UnityEngine;

public static class CONST
{
    public const string HotUpdateUrl = "http://192.168.0.47:82/WebGL/";
    public const string versionName = "version.json";
    public const string HotUpdateFileName = "update.zip";
    public const string md5FileName = "MD5.txt";
    public const int maxCheckCount = 3;
    public static string UpdateFilePath => Application.persistentDataPath + "/aa/";
    public static string LocalFilePath => Application.streamingAssetsPath + "/aa/";
}