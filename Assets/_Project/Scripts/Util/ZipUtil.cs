using System;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using UnityEngine;

public static class ZipUtil
{
    /// <summary>
    /// 将整个目录压缩为 zip 文件
    /// </summary>
    /// <param name="sourceDir">要压缩的目录</param>
    /// <param name="zipPath">输出的 zip 文件路径</param>
    public static void ZipDirectory(string sourceDir, string zipPath)
    {
        if (!Directory.Exists(sourceDir))
        {
            Debug.LogError($"ZipDirectory: source dir not found: {sourceDir}");
            return;
        }

        // 确保目标目录存在
        string zipDir = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrEmpty(zipDir) && !Directory.Exists(zipDir))
            Directory.CreateDirectory(zipDir);

        using (var fs = File.Create(zipPath))
        using (var zos = new ZipOutputStream(fs))
        {
            zos.SetLevel(9); // 最大压缩级别

            string[] files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                string relPath = file.Substring(sourceDir.Length + 1).Replace("\\", "/");
                var entry = new ZipEntry(relPath)
                {
                    DateTime = File.GetLastWriteTime(file),
                    Size = new FileInfo(file).Length
                };
                zos.PutNextEntry(entry);

                using (var stream = File.OpenRead(file))
                {
                    stream.CopyTo(zos);
                }
                zos.CloseEntry();
            }
            zos.Finish();
        }

        Debug.Log($"ZipDirectory done: {zipPath} ({new FileInfo(zipPath).Length} bytes)");
    }

    /// <summary>
    /// 将指定文件列表压缩为 zip，保留相对于 baseDir 的路径结构
    /// </summary>
    public static void ZipFiles(string baseDir, string[] files, string zipPath)
    {
        string zipDir = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrEmpty(zipDir) && !Directory.Exists(zipDir))
            Directory.CreateDirectory(zipDir);

        using (var fs = File.Create(zipPath))
        using (var zos = new ZipOutputStream(fs))
        {
            zos.SetLevel(9);

            foreach (var file in files)
            {
                if (!File.Exists(file)) continue;
                string relPath = file.Substring(baseDir.Length + 1).Replace("\\", "/");
                var entry = new ZipEntry(relPath)
                {
                    DateTime = File.GetLastWriteTime(file),
                    Size = new FileInfo(file).Length
                };
                zos.PutNextEntry(entry);

                using (var stream = File.OpenRead(file))
                {
                    stream.CopyTo(zos);
                }
                zos.CloseEntry();
            }
            zos.Finish();
        }

        Debug.Log($"ZipFiles done: {zipPath} ({new FileInfo(zipPath).Length} bytes)");
    }

    public static void Unzip(string zipPath, string extractPath)
    {
        try
        {
            if (!File.Exists(zipPath))
            {
                Debug.LogError($"Zip file not found: {zipPath}");
                return;
            }
            if (!Directory.Exists(extractPath))
                Directory.CreateDirectory(extractPath);

            using (var zipInputStream = new ZipInputStream(File.OpenRead(zipPath)))
            {
                ZipEntry entry;
                while ((entry = zipInputStream.GetNextEntry()) != null)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    string fileFullPath = Path.Combine(extractPath, entry.Name);
                    if (fileFullPath.EndsWith("/") || fileFullPath.EndsWith("\\"))
                    {
                        Directory.CreateDirectory(fileFullPath);
                        continue;
                    }

                    // 确保父目录存在
                    string dir = Path.GetDirectoryName(fileFullPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    using (var fileStream = File.Create(fileFullPath))
                    {
                        byte[] buffer = new byte[8192];
                        int read;
                        while ((read = zipInputStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            fileStream.Write(buffer, 0, read);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Unzip error: {e.Message}");
        }
    }
}
