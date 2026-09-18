using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public class MD5Util
{
    /// <summary>
    /// 计算字符串的 MD5
    /// </summary>
    public static string GetMD5(string str)
    {
        return BitConverter.ToString(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(str))).Replace("-", "").ToLower();
    }

    /// <summary>
    /// 计算文件的 MD5
    /// </summary>
    public static string GetFileMD5(string filePath)
    {
        using (var md5 = MD5.Create())
        {
            using (var stream = File.OpenRead(filePath))
            {
                return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLower();
            }
        }
    }
}
