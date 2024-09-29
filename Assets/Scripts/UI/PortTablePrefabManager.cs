using DevKit.Tool;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using static NetworkPortManager;

public class PortTablePrefabManager : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    private NetworkPortTableUIManager portTableUIManager;

    private void Start()
    {
        portTableUIManager = FindObjectOfType<NetworkPortTableUIManager>();
    }

    public void InstantiatePortTable(UICollector uiCollector, PortData portData)
    {
        Transform parentTransform = uiCollector.GetAsset<GameObject>(UIKey.UI_Tables)?.transform;
        if (parentTransform != null)
        {
            GameObject instance = Instantiate(prefab, parentTransform);
            instance.SetActive(true);
            InitializeTable(instance, portData);
        }
    }  

    private void InitializeTable(GameObject instance, PortData portData)
    {
        if (instance.TryGetComponent<Table>(out var table))
        {
            table.Init(portData);
            table.OnDelete += (protocol, PortData) => DeletePort(protocol, portData);
            table.OnConnect += (protocol, PortData) => ConnectPort(protocol, portData);
        }
    }

    public void RefreshAndRecreateTables(UICollector uiCollector)
    {
        Transform parentTransform = uiCollector.GetAsset<GameObject>(UIKey.UI_Tables)?.transform;
        int childCount = parentTransform.childCount;
        for (int i = childCount - 1; i > 0; i--)
        {
            Transform child = parentTransform.GetChild(i);
            Destroy(child.gameObject);
        }
    }

    public void DeletePort(string protocolType, PortData portData)
    {
        if (portTableUIManager != null)
        {
            portTableUIManager.OnRemove(protocolType, portData);
        }
    }
    private void ConnectPort(string protocolType, PortData portData)
    {
        if (portTableUIManager != null)
        {
            portTableUIManager.OnConnect(protocolType, portData);
        }
    }
}
