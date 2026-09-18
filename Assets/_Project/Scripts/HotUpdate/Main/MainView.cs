using TMPro;

public class MainView : BaseView
{
    public TextMeshProUGUI textText;
    public override void Init()
    {
        base.Init();
        textText = GetComponent<TextMeshProUGUI>();
    }
    public override void OnShow()
    {
        base.OnShow();
        textText.text = (MainController.Instance.model as MainModel).showText;
    }
}