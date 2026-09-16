using System;
using System.Reflection;
using TMPro;
using UnityEngine;

public class ShowUI : MonoBehaviour
{
    private TextMeshProUGUI ui;
    public void Awake()
    {
        ui = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        if (LoadDll.Instance.iHello != null)
        {
            ui.text = LoadDll.Instance.iHello.Run();
        }
    }
}