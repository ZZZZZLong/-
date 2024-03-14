using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ConnectionApprovalHandler : MonoBehaviour
{
    private const int MaxPlayer = 2;
    //��׼���
    private void Start()
    {
        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        Debug.Log("��������׼");

        response.Approved = true;//��ζ�ſͻ���������
        response.CreatePlayerObject = true;
        response.PlayerPrefabHash = null;
        


        if (NetworkManager.Singleton.ConnectedClients.Count >= MaxPlayer)
        {
            
            response.Approved = false;
            response.Reason = "��������";

        }
        Debug.Log(NetworkManager.Singleton.ConnectedClients.Count);
        response.Pending = false;//
    }
}
