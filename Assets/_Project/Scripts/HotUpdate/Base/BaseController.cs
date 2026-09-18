public class BaseController<T> : InstanceBase<BaseController<T>> where T : BaseModel, new()
{
    public T model;

    public virtual void Init()
    {
        model = new T();
        model.Init();
    }
}