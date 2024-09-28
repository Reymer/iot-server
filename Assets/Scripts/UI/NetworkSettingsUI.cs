using DevKit.Tool;
using System;
using UnityEngine;
using TMPro;
using DevKit.Console;
using System.Net.Sockets;
using System.Net;
using Random = System.Random;

public class NetworkSettingsUI : MonoBehaviour
{
    private UICollector uiCollector;
    private ConsoleUI consoleUi;
    public event Action<string, string, string, string> Confirm;
    private string protocolType = "UDP";
    private int? remotePort;
    private int? localPort;
    private string targetIP;
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
        uiCollector.BindOnCheck(UIKey.UI_OK, OnConfirm);
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
        for (int i = childCount - 1; i >= 0; i--)
        {
            Transform child = parentTransform.GetChild(i);
            Destroy(child.gameObject);
        }
    }

    private void OnDropdownValueChanged(int index)
    {
        switch (index)
        {
            case 0:
                protocolType = "UDP";
                SetUiStatus(true, UIKey.UI_TargetIPMask);
                SetUiStatus(false, UIKey.UI_RemotePortsMask);
                SetUiStatus(false, UIKey.UI_LocalPortsMask);
                uiCollector.GetAsset<TMP_InputField>(UIKey.UI_RemotePortInput).text = string.Empty;
                uiCollector.GetAsset<TMP_InputField>(UIKey.UI_LocalPortInput).text = string.Empty;
                break;
            case 1:
                protocolType = "TCP Server";
                var remotePort = GetRandomAvailablePort().ToString();
                uiCollector.GetAsset<TMP_InputField>(UIKey.UI_RemotePortInput).text = remotePort;
                uiCollector.GetAsset<TMP_InputField>(UIKey.UI_LocalPortInput).text = string.Empty;
                SetUiStatus(true, UIKey.UI_TargetIPMask);
                SetUiStatus(true, UIKey.UI_RemotePortsMask);
                SetUiStatus(false, UIKey.UI_LocalPortsMask);
                break;
            case 2:
                protocolType = "TCP Client";
                var localPort = GetRandomAvailablePort().ToString();
                uiCollector.GetAsset<TMP_InputField>(UIKey.UI_LocalPortInput).text = localPort;
                uiCollector.GetAsset<TMP_InputField>(UIKey.UI_RemotePortInput).text = string.Empty;
                SetUiStatus(false, UIKey.UI_TargetIPMask);
                SetUiStatus(false, UIKey.UI_RemotePortsMask);
                SetUiStatus(true, UIKey.UI_LocalPortsMask);
                break;
        }
        Debug.Log(protocolType);
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

    private void OnConfirm()
    {
        if (protocolType.Equals("TCP Server", StringComparison.OrdinalIgnoreCase) && !remotePort.HasValue)
        {
            consoleUi.AddLog("遠程端口號碼無效。請檢查輸入");
            return;
        }

        if (protocolType.Equals("TCP Client", StringComparison.OrdinalIgnoreCase) && !localPort.HasValue)
        {
            consoleUi.AddLog("本地端口號碼無效。請檢查輸入");
            return;
        }

        if (protocolType.Equals("TCP Server", StringComparison.OrdinalIgnoreCase))
        {
            Confirm?.Invoke(protocolType, "--", localPort.ToString(), targetIP);
        }
        else if(protocolType.Equals("TCP Client", StringComparison.OrdinalIgnoreCase))
        {
            Confirm?.Invoke(protocolType, remotePort.ToString(), "--", targetIP);
        }
        else
        {
            Confirm?.Invoke(protocolType, remotePort.ToString(), localPort.ToString(), targetIP = string.Empty);
        }

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
        if (uiCollector.GetAsset<GameObject>(UIKey.UI_MenuRoot).activeSelf) { return; }
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
        SetUiStatus(true, UIKey.UI_TargetIPMask);
        SetUiStatus(false, UIKey.UI_RemotePortsMask);
        SetUiStatus(false, UIKey.UI_LocalPortsMask);
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

    private int GetRandomAvailablePort()
    {
        var random = new Random();
        int port;

        while (true)
        {
            port = random.Next(49152, 65535); // 随机选择一个动态端口范围
            if (IsPortAvailable(port)) // 检查端口是否可用
            {
                break;
            }
        }

        return port;
    }

    private bool IsPortAvailable(int port)
    {
        bool isAvailable = true;

        try
        {
            TcpListener listener = new(IPAddress.Any, port);
            listener.Start();
            listener.Stop();
        }
        catch (SocketException)
        {
            isAvailable = false; // 如果抛出异常，表示端口被占用
        }

        return isAvailable;
    }
}
