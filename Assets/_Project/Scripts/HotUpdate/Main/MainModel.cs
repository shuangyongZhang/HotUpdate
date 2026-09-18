public class MainModel : BaseModel
{
    public string showText = "";
    public override void Init()
    {
        base.Init();
        SetData();
    }

    public void SetData()
    {
        showText = "Hello World!";
    }
}