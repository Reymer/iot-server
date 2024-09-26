using DevKit.Tool;
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
            table.Init(portData.NetProtocol, portData.RemotePortDetails.Port, portData.LocalPortDetails.Port, portData.TargetIP, portData.COMReceived, portData.NetReceived, "Connecting");
            table.delete += (protocol, remotePort) => DeletePort(protocol, remotePort);
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

    public void DeletePort(string protocolType, int remotePort)
    {
        if (portTableUIManager != null)
        {
            portTableUIManager.OnRemove(protocolType, remotePort);
        }
    }
}
