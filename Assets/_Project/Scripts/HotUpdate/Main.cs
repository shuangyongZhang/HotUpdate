using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public class Main
{
    public void Run()
    {
        InitController();
        LoadScene();
    }

    void InitController()
    {
        MainController.Instance.Init();
    }
    async Task LoadShaders()
    {
        await ResourceLoadManager.Instance.LoadAsync<Shader>("_Project/Shader/BumpShader");
    }
    async void LoadScene()
    {
        await LoadShaders();
        SceneInstance scene = await ResourceLoadManager.Instance.LoadSceneAsync("Game", LoadSceneMode.Single);
        SceneManager.SetActiveScene(scene.Scene);
        UIManager.Instance.Init();
        await LoadModel();
        LoadUI();
    }

    void LoadUI()
    {
        UIManager.Instance.LoadUI<MainView>("MainView", (go) =>
        {

        });
    }
    async Task LoadModel()
    {
        GameObject model = await ResourceLoadManager.Instance.InstantiateModelAsync("Sphere");
        model.transform.position = Vector3.zero;
        model.transform.localScale = Vector3.one;
        model.transform.rotation = Quaternion.identity;
    }
}