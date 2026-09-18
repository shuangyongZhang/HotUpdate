using System;
using System.Security.Cryptography;
using System.Text;

public class MD5Util
{
    public static string GetMD5(string str)
    {
        return BitConverter.ToString(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(str)));
    }
}