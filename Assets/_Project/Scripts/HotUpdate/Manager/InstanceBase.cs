using System;
using System.Threading;

public class InstanceBase<T> where T : new()
{
    private static Lazy<T> _instance = new Lazy<T>(() => new T(), LazyThreadSafetyMode.PublicationOnly);
    public static T Instance => _instance.Value;
}