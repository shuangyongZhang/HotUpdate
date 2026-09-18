using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public static class AddressablesDiffJson
{
    static string BuildDir => Path.Combine(Application.streamingAssetsPath, "aa/Windows");
    const string LastListPath = "HotUpdateManifests/last_files.txt";
    const string DiffJsonPath = "HotUpdateOutput/diff.json";
    const string DiffDir = "HotUpdateOutput";

    [MenuItem("Tools/Addressables/生成差异 diff.json")]
    public static void GenerateDiff()
    {
        // 1. 收集本次所有 bundle 文件名（相对路径）
        if (!Directory.Exists(BuildDir))
            Directory.CreateDirectory(BuildDir);

        var newFiles = Directory
            .GetFiles(BuildDir, "*.bundle", SearchOption.AllDirectories)
            .Select(f => f.Replace("\\", "/").Replace(BuildDir + "/", ""))
            .ToHashSet();

        // 2. 读取上次列表
        var lastFiles = File.Exists(LastListPath)
            ? File.ReadAllLines(LastListPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToHashSet()
            : new HashSet<string>();

        // 3. 计算差集
        var added = newFiles.Except(lastFiles);   // 新增 → 1
        var removed = lastFiles.Except(newFiles);   // 删除 → -1

        // 4. 构造 diff.json
        var diff = new Dictionary<string, int>();
        foreach (var f in added) diff[f] = 1;
        foreach (var f in removed) diff[f] = -1;

        // 5. 输出
        Directory.CreateDirectory(DiffDir);
        File.WriteAllText(DiffJsonPath,
            JsonUtility.ToJson(new Wrapper { entries = diff }, true));

        // 6. 复制新增文件到差异目录
        if (Directory.Exists(DiffDir)) Directory.Delete(DiffDir, true);
        Directory.CreateDirectory(DiffDir);
        foreach (var rel in added)
        {
            string src = Path.Combine(BuildDir, rel);
            string dst = Path.Combine(DiffDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(src, dst, true);
        }

        // 7. 更新基准
        Directory.CreateDirectory(Path.GetDirectoryName(LastListPath));
        File.WriteAllLines(LastListPath, newFiles);

        Debug.Log($"diff: 新增 {added.Count()}，删除 {removed.Count()}");
    }

    [System.Serializable]
    class Wrapper { public Dictionary<string, int> entries; }
}