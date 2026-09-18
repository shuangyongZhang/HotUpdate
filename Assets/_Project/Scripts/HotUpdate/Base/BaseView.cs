using UnityEngine;

public class BaseView : MonoBehaviour
{

    public virtual void Init()
    {

    }
    public virtual void OnShow()
    {
        gameObject.SetActive(true);
    }
    public virtual void OnHide()
    {
        gameObject.SetActive(false);
    }

    public virtual void OnClose()
    {
        ResourceLoadManager.Instance.Unload(gameObject);
    }
}