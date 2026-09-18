using UnityEngine;

public class MonoInstanceBase<T> where T : MonoBehaviour
{
    private static T _instance;
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.LogError($"MonoInstanceBase<T> {typeof(T).Name} not found in scene");
                return null;
            }
            return _instance;
        }
    }
    private void Awake()
    {
        _instance = this as T;
    }
}