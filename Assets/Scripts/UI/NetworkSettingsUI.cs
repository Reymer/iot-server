using DevKit.Tool;
using System;
using UnityEngine;
using TMPro;
using DevKit.Console;

public class NetworkSettingsUI : MonoBehaviour
{
    private UICollector uiCollector;
    public Action<string, int, int, string> confirm;
    private string protocolType = "UDP";
    private int? remotePort;
    private int? localPort;
    private string targetIP;
    private ConsoleUI consoleUi;
    private bool isOpenConsole = true;

    private void Start()
    {
        Init();
        Subscribe();
    }

    private void Init()
    {
        consoleUi = GameObject.FindObjectOfType<ConsoleUI>(true);
        uiCollector = GetComponent<UICollector>();
        CloseMenu(UIKey.UI_MenuRoot);
    }

    private void Subscribe()
    {
        if (uiCollector == null) return;

        uiCollector.BindOnCheck(UIKey.UI_DeleteButton, () => CloseMenu(UIKey.UI_MenuRoot));
        uiCollector.BindOnCheck(UIKey.UI_MenuCancel, () => CloseMenu(UIKey.UI_MenuRoot));
        uiCollector.BindOnCheck(UIKey.UI_AddPort, () => OpenMenu(UIKey.UI_MenuRoot));
        uiCollector.BindOnCheck(UIKey.UI_OK, Confirm);
        uiCollector.BindOnCheck(UIKey.UI_Console, OnConsole);
        uiCollector.BindOnCheck(UIKey.UI_clear, OnClearConsole);
        uiCollector.GetAsset<TMP_Dropdown>(UIKey.UI_NetProtocolDropdowm).onValueChanged.AddListener(OnDropdownValueChanged);
        uiCollector.GetAsset<TMP_InputField>(UIKey.UI_RemotePortInput).onValueChanged.AddListener(OnRemotePortInput);
        uiCollector.GetAsset<TMP_InputField>(UIKey.UI_LocalPortInput).onValueChanged.AddListener(OnLocalPortInput);
        uiCollector.GetAsset<TMP_InputField>(UIKey.UI_TargetIPInput).onValueChanged.AddListener(OnTargetInput);
    }

    private void Update()
    {
        Transform parentTransform = uiCollector.GetAsset<GameObject>(UIKey.UI_Consolelayout)?.transform;
        int childCount = parentTransform.childCount;
        if (childCount < 50)
        {
            return;
        }
        OnClearConsole();
    }

    private void OnClearConsole()
    {
        Transform parentTransform = uiCollector.GetAsset<GameObject>(UIKey.UI_Consolelayout)?.transform;

        if (parentTransform == null) return;
        int childCount = parentTransform.childCount;
        // Iterate over all children and destroy them
        for (int i = childCount - 1; i >= 0; i--)
        {
            Transform child = parentTransform.GetChild(i);
            Destroy(child.gameObject);
        }
    }


    private void OnDropdownValueChanged(int index)
    {
        protocolType = index == 0 ? "UDP" : "TCP";
    }

    private void OnRemotePortInput(string value)
    {
        remotePort = ParsePort(value);
    }

    private void OnTargetInput(string value)
    {
        targetIP = value;
    }

    private void OnLocalPortInput(string value)
    {
        localPort = ParsePort(value);
    }

    private int? ParsePort(string portString)
    {
        if (int.TryParse(portString, out int port) && port > 0 && port <= 65535)
        {
            return port;
        }
        return null;
    }

    private bool IsValidIPv4(string ipString)
    {
        if (string.IsNullOrWhiteSpace(ipString)) return false;

        string[] parts = ipString.Split('.');
        if (parts.Length != 4) return false;

        foreach (var part in parts)
        {
            if (!int.TryParse(part, out int number) || number < 0 || number > 255)
            {
                return false;
            }
        }

        return true;
    }

    private void Confirm()
    {
        if (!remotePort.HasValue || !localPort.HasValue)
        {
            consoleUi.AddLog("連接埠號碼無效。請檢查輸入");
            return;
        }
        if (!string.IsNullOrEmpty(targetIP) && !IsValidIPv4(targetIP))
        {
            consoleUi.AddLog("IP 位址無效。請輸入有效的 IPv4 位址。");
            return;
        }
        confirm?.Invoke(protocolType, remotePort.Value, localPort.Value, targetIP);
        CloseMenu(UIKey.UI_MenuRoot);
    }

    private void OnConsole()
    {
        isOpenConsole = !isOpenConsole;
        SetUiStatus(isOpenConsole, UIKey.UI_ConsoleUI);
    }

    private void CloseMenu(string uiKey)
    {
        SetUiStatus(false, uiKey);
    }

    private void OpenMenu(string uiKey)
    {
        Clear();
        SetUiStatus(true, uiKey);
    }

    private void Clear()
    {
        var remoteInput = uiCollector.GetAsset<TMP_InputField>(UIKey.UI_RemotePortInput);
        var localInput = uiCollector.GetAsset<TMP_InputField>(UIKey.UI_LocalPortInput);
        var targetInput = uiCollector.GetAsset<TMP_InputField>(UIKey.UI_TargetIPInput);
        var protocolDropdown = uiCollector.GetAsset<TMP_Dropdown>(UIKey.UI_NetProtocolDropdowm);
        remoteInput.text = string.Empty;
        localInput.text = string.Empty;
        targetInput.text = string.Empty;
        remotePort = null;
        localPort = null;
        targetIP = null;
        protocolType = "UDP";
        protocolDropdown.value = 0;
        protocolDropdown.RefreshShownValue(); 
    }

    private void SetUiStatus(bool status, string uiKey)
    {
        if (uiCollector != null)
        {
            uiCollector.SetActive(uiKey, status);
        }
    }
}
