using System;
using System.Threading.Tasks;
using UnityEngine;

public class UIManager : InstanceBase<UIManager>
{
    public Canvas Canvas;
    public void Init()
    {
        Canvas = GameObject.FindObjectOfType<Canvas>();
    }
    public async Task<T> LoadUI<T>(string uiName) where T : BaseView
    {
        GameObject go = await ResourceLoadManager.Instance.InstantiateUIAsync(uiName);
        go.transform.SetParent(Canvas.transform, false);
        T view = go.GetComponent<T>();
        view.Init();
        view.OnShow();
        return view;
    }
    public void LoadUI<T>(string uiName, Action<GameObject> onComplete) where T : BaseView
    {
        ResourceLoadManager.Instance.InstantiateUI(uiName, go =>
        {
            T view = go.GetComponent<T>();
            view.transform.localPosition = Vector3.zero;
            view.transform.localScale = Vector3.one;
            view.Init();
            view.OnShow();
            onComplete?.Invoke(go);
        }, Canvas.transform, true, true);
    }
}