using DevKit.Tool;
using System;
using UnityEngine;

public class Table : MonoBehaviour
{
    public UICollector uiCollector;

    private string protocolType;
    private int remotePort;
    private int localPort;
    private int receivedCount;
    private int comReceivedCount;
    private string receivedStatus;
    private string targetIP;
    public Action<string, int> delete;

    private void Start()
    {
        Subscribe();
    }

    public void Init(string protocolType, int remotePort, int localPort, string targetIP, int comReceivedCount, int receivedCount, string receivedStatus)
    {
        this.protocolType = protocolType;
        this.remotePort = remotePort;
        this.localPort = localPort;
        this.targetIP = targetIP;
        this.comReceivedCount = comReceivedCount;
        this.receivedCount = receivedCount;
        this.receivedStatus = receivedStatus;
        SetValue();
    }

    private void Subscribe()
    {
        if (uiCollector != null)
        {
            uiCollector.BindOnCheck(UIKey.table_Delete, () => { Delete(protocolType, remotePort); });
        }
    }

    public void SetValue()
    {
        if (uiCollector != null)
        {
            SetValue(UIKey.table_prococolText, protocolType);
            SetValue(UIKey.table_remoteText, remotePort.ToString());
            SetValue(UIKey.table_localPortText, localPort.ToString());
            SetValue(UIKey.table_COMReceived, comReceivedCount.ToString());
            SetValue(UIKey.table_netReceived, receivedCount.ToString());
            SetValue(UIKey.table_ForwardTargetText, targetIP);
            SetValue(UIKey.table_netReceivedStatus, receivedStatus);
        }
    }

    public void Delete(string protocolType, int remotePort)
    {
        delete?.Invoke(protocolType, remotePort);
    }

    public void SetValue(string uiKey, string content)
    {
        if (uiCollector != null)
        {
            uiCollector.SetText(uiKey, content);
        }
    }
}
