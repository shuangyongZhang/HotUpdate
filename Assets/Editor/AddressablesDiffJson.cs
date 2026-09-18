using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 热更新补丁工具：对比 Addressables 构建产物，收集变化文件 → 生成 MD5.txt → 压缩为 update.zip → 更新 version.json
/// </summary>
public static class AddressablesDiffJson
{
    // streamingAssetsPath 作为根目录基准，保证压缩包解压后路径与 TransformFunc 查找路径一致
    static string RootDir => Application.streamingAssetsPath;
    static string AABaseDir => Path.Combine(RootDir, "aa");
    static string BuildDir => Path.Combine(AABaseDir, "Windows");

    const string LastManifestPath = "HotUpdateManifests/last_manifest.json";
    const string DiffDir = "HotUpdateOutput";
    const string MD5FileName = "MD5.txt";
    const string ManifestCacheKey = "HotUpdateDiff_LastManifestCache"; // EditorPrefs 临时缓存

    // 需要额外纳入热更新的文件（相对 RootDir 的路径）
    static readonly string[] ExtraHotUpdateFiles =
    {
        "HotUpdate.dll.bytes",
    };

    [MenuItem("Tools/Addressables/生成差异补丁")]
    public static void GenerateDiff()
    {
        // 1. 确保构建目录存在
        if (!Directory.Exists(BuildDir))
        {
            Debug.LogError($"Build dir not found: {BuildDir}");
            return;
        }

        // 2. 收集本次所有文件：aa/Windows/ 下的 bundle + catalog.json 等 + 额外热更新文件
        var newManifest = CollectAllFiles(BuildDir, RootDir);

        // 额外收集 RootDir 下的热更新文件（如 HotUpdate.dll.bytes）
        foreach (var relPath in ExtraHotUpdateFiles)
        {
            string fullPath = Path.Combine(RootDir, relPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"额外热更新文件不存在，跳过: {fullPath}");
                continue;
            }
            newManifest[relPath] = MD5Util.GetFileMD5(fullPath);
        }

        Debug.Log($"本次构建文件总数: {newManifest.Count}");

        // 3. 读取上次清单
        var lastManifest = LoadLastManifest();
        Debug.Log($"上次构建文件总数: {lastManifest.Count}");

        // 4. 计算变化：新增 / 修改 / 删除
        var added = new Dictionary<string, string>();
        var removed = new HashSet<string>();
        var modified = new Dictionary<string, string>();

        foreach (var kv in newManifest)
        {
            string relPath = kv.Key;
            string newMd5 = kv.Value;
            if (!lastManifest.ContainsKey(relPath))
                added[relPath] = newMd5;
            else if (lastManifest[relPath] != newMd5)
                modified[relPath] = newMd5;
        }
        foreach (var relPath in lastManifest.Keys)
        {
            if (!newManifest.ContainsKey(relPath))
                removed.Add(relPath);
        }

        Debug.Log($"变化统计: 新增 {added.Count}，修改 {modified.Count}，删除 {removed.Count}");

        if (added.Count == 0 && modified.Count == 0 && removed.Count == 0)
        {
            Debug.Log("没有变化，跳过补丁生成");
            return;
        }

        // 5. 准备差异输出目录
        if (Directory.Exists(DiffDir)) Directory.Delete(DiffDir, true);
        Directory.CreateDirectory(DiffDir);

        // 6. 复制变化文件到差异目录（保持相对 RootDir 的路径结构）
        var changedFiles = new Dictionary<string, string>(added);
        foreach (var kv in modified) changedFiles[kv.Key] = kv.Value;

        int copyFailCount = 0;
        foreach (var rel in changedFiles.Keys)
        {
            string src;
            if (rel.StartsWith("aa/") || rel.StartsWith("aa\\"))
            {
                string buildDirRelPrefix = BuildDir.Replace("\\", "/").Replace(RootDir.Replace("\\", "/") + "/", ""); // "aa/Windows"
                string relInBuild = rel.Substring(buildDirRelPrefix.Length).TrimStart('/');
                src = Path.Combine(BuildDir, relInBuild);
            }
            else
            {
                src = Path.Combine(RootDir, rel);
            }

            string dst = Path.Combine(DiffDir, rel);
            try
            {
                SafeCopy(src, dst);
            }
            catch (Exception e)
            {
                Debug.LogError($"复制文件失败: {src} -> {dst}\n{e.Message}");
                copyFailCount++;
            }
        }

        // 7. 生成 MD5.txt（相对 DiffDir 的完整路径:md5，每行一条）
        GenerateMD5File(DiffDir);

        // 8. 压缩为 update.zip
        string temp = Path.Combine(Directory.GetParent(DiffDir).FullName, "Temp1");
        if (Directory.Exists(temp))
            Directory.Delete(temp, true);
        Directory.CreateDirectory(temp);
        string updateZipPath = Path.Combine(temp, "update.zip");
        if (File.Exists(updateZipPath)) File.Delete(updateZipPath);
        ZipUtil.ZipDirectory(DiffDir, updateZipPath);
        File.Copy(updateZipPath, Path.Combine(DiffDir, "update.zip"));

        // 9. 更新 version.json（只更新 UpdateZipSize，版本号请手动调整）
        string versionJsonPath = Path.Combine(RootDir, "version.json");
        UpdateVersionJson(versionJsonPath, updateZipPath);

        // 10. 缓存本次 manifest 到 EditorPrefs，等确认补丁没问题后再手动刷新基准
        var manifestData = new ManifestData { files = newManifest.Select(kv => new FileEntry { path = kv.Key, md5 = kv.Value }).ToList() };
        string manifestJson = JsonUtility.ToJson(manifestData, false);
        EditorPrefs.SetString(ManifestCacheKey, manifestJson);

        // 同时保存到 StreamingAssets 下方便对比
        string currentManifestPath = Path.Combine(RootDir, "current_manifest.json");
        File.WriteAllText(currentManifestPath, manifestJson);

        // 11. 打印总结
        long zipSize = new FileInfo(updateZipPath).Length;
        Debug.Log($"========== 热更新补丁生成完成 ==========");
        Debug.Log($"新增: {added.Count}, 修改: {modified.Count}, 删除: {removed.Count}");
        Debug.Log($"复制失败: {copyFailCount}");
        Debug.Log($"压缩包大小: {zipSize} bytes ({zipSize / 1024.0:F2} KB)");
        Debug.Log($"MD5.txt: {Path.Combine(DiffDir, MD5FileName)}");
        Debug.Log($"update.zip: {updateZipPath}");
        Debug.Log($"version.json: {versionJsonPath}");
        Debug.Log($"current_manifest.json: {currentManifestPath}");
        Debug.Log($"基准清单未更新 → 确认补丁没问题后点击 Tools/Addressables/刷新差异文件");
    }

    /// <summary>
    /// 手动刷新基准：把上一次 GenerateDiff 收集的 manifest 写入 last_manifest.json
    /// </summary>
    [MenuItem("Tools/Addressables/刷新差异文件")]
    public static void RefreshLastManifest()
    {
        if (!EditorPrefs.HasKey(ManifestCacheKey))
        {
            Debug.LogWarning("没有缓存的 manifest，请先执行 '生成差异补丁'");
            return;
        }

        string json = EditorPrefs.GetString(ManifestCacheKey);
        var data = JsonUtility.FromJson<ManifestData>(json);
        if (data == null || data.files == null || data.files.Count == 0)
        {
            Debug.LogWarning("缓存的 manifest 为空");
            return;
        }

        SaveLastManifest(data);
        EditorPrefs.DeleteKey(ManifestCacheKey);
        Debug.Log($"========== 基准清单已刷新（{data.files.Count} 个文件） ==========");
    }

    // ---------------------------------------------------------------
    // 核心辅助方法
    // ---------------------------------------------------------------

    /// <summary>
    /// 收集 dir 下所有文件，返回相对于 rootDir 的 路径→MD5 映射
    /// </summary>
    static Dictionary<string, string> CollectAllFiles(string dir, string rootDir)
    {
        var result = new Dictionary<string, string>();
        string rootNorm = rootDir.Replace("\\", "/").TrimEnd('/');
        string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            if (file.EndsWith(".meta")) continue;

            // 跳过 MD5.txt / update.zip 等产物
            string fileName = Path.GetFileName(file);
            if (fileName == MD5FileName || fileName == "update.zip") continue;

            string fileNorm = file.Replace("\\", "/");
            if (fileNorm.StartsWith(rootNorm + "/"))
            {
                string relPath = fileNorm.Substring(rootNorm.Length + 1);
                result[relPath] = MD5Util.GetFileMD5(file);
            }
        }
        return result;
    }

    /// <summary>
    /// 从 HotUpdateManifests/last_manifest.json 读取上次的 MD5 清单
    /// </summary>
    static Dictionary<string, string> LoadLastManifest()
    {
        if (!File.Exists(LastManifestPath))
            return new Dictionary<string, string>();

        var json = File.ReadAllText(LastManifestPath);
        var data = JsonUtility.FromJson<ManifestData>(json);
        if (data == null || data.files == null)
            return new Dictionary<string, string>();

        var result = new Dictionary<string, string>();
        foreach (var kv in data.files)
        {
            result[kv.path] = kv.md5;
        }
        return result;
    }

    /// <summary>
    /// 保存本次清单为 HotUpdateManifests/last_manifest.json
    /// </summary>
    static void SaveLastManifest(ManifestData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LastManifestPath));
        File.WriteAllText(LastManifestPath, JsonUtility.ToJson(data, true));
        Debug.Log($"已保存基准清单: {LastManifestPath}");
    }

    /// <summary>
    /// 生成 MD5.txt：相对路径:md5，每行一条
    /// </summary>
    static void GenerateMD5File(string diffDir)
    {
        var sb = new System.Text.StringBuilder();
        string[] files = Directory.GetFiles(diffDir, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            string fileName = Path.GetFileName(file);
            if (fileName == MD5FileName || fileName == "update.zip") continue;

            string relPath = file.Substring(diffDir.Length + 1).Replace("\\", "/");
            string md5 = MD5Util.GetFileMD5(file);
            sb.AppendLine($"{relPath}:{md5}");
        }
        File.WriteAllText(Path.Combine(diffDir, MD5FileName), sb.ToString());
        Debug.Log($"MD5.txt 已生成，共 {files.Length - 1} 个文件");
    }

    /// <summary>
    /// 更新 version.json：只更新 UpdateZipSize（版本号请手动调整）
    /// </summary>
    static void UpdateVersionJson(string versionJsonPath, string updateZipPath)
    {
        VersionData versionData;
        if (File.Exists(versionJsonPath))
        {
            versionData = JsonUtility.FromJson<VersionData>(File.ReadAllText(versionJsonPath)) ?? new VersionData();
        }
        else
        {
            versionData = new VersionData { Version = "1.0" };
        }

        long zipSize = new FileInfo(updateZipPath).Length;
        versionData.UpdateZipSize = zipSize.ToString();

        File.WriteAllText(versionJsonPath, JsonUtility.ToJson(versionData, true));
        Debug.Log($"version.json 已更新: Version={versionData.Version}, UpdateZipSize={versionData.UpdateZipSize}");
    }

    // ---------------------------------------------------------------
    // 长路径兼容（Windows MAX_PATH 260 限制）
    // ---------------------------------------------------------------

    static void SafeCopy(string src, string dst)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dst));

        try
        {
            File.Copy(src, dst, true);
        }
        catch (PathTooLongException)
        {
            SafeCopyLongPath(src, dst);
        }
    }

    static void SafeCopyLongPath(string src, string dst)
    {
        string longSrc = @"\\?\" + src;
        string longDst = @"\\?\" + dst;

        string dir = @"\\?\" + Path.GetDirectoryName(dst);
        Directory.CreateDirectory(dir);

        using (var fsSrc = new FileStream(longSrc, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.SequentialScan))
        using (var fsDst = new FileStream(longDst, FileMode.Create, FileAccess.Write, FileShare.None, 8192, FileOptions.SequentialScan))
        {
            fsSrc.CopyTo(fsDst);
        }
        Debug.LogWarning($"使用长路径方式复制成功: {Path.GetFileName(dst)}");
    }

    // ---------------------------------------------------------------
    // 序列化辅助类
    // ---------------------------------------------------------------

    [Serializable]
    class ManifestData
    {
        public List<FileEntry> files = new List<FileEntry>();
    }

    [Serializable]
    class FileEntry
    {
        public string path;
        public string md5;
    }
}
