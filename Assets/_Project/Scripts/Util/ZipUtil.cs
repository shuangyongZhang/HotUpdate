using System;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using UnityEngine;

public static class ZipUtil
{
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
            ZipInputStream zipInputStream = new ZipInputStream(File.OpenRead(zipPath));
            ZipEntry entry;
            FileStream fileStream = null;
            while ((entry = zipInputStream.GetNextEntry()) != null)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                string fileFullPath = Path.Combine(extractPath, entry.Name);
                if (fileFullPath.EndsWith("/") || fileFullPath.EndsWith("\\"))
                {
                    Directory.CreateDirectory(fileFullPath);
                    continue;
                }
                fileStream = File.Create(fileFullPath);
                int size = 2048;
                byte[] buffer = new byte[size];

                while (true)
                {
                    size = zipInputStream.Read(buffer, 0, buffer.Length);
                    if (size <= 0) break;
                    fileStream.Write(buffer, 0, size);
                }

                File.Create(fileFullPath).Close();
            }
            if (fileStream != null) fileStream.Close();
            if (entry != null) entry = null;
            if (zipInputStream != null) zipInputStream.Close();
        }
        catch (Exception e)
        {
            Debug.LogError($"Unzip error: {e.Message}");
        }
    }
}