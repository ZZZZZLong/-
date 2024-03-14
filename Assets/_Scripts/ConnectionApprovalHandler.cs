using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ConnectionApprovalHandler : MonoBehaviour
{
    private const int MaxPlayer = 2;
    //批准玩家
    private void Start()
    {
        NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        Debug.Log("连接已批准");

        response.Approved = true;//意味着客户可以连接
        response.CreatePlayerObject = true;
        response.PlayerPrefabHash = null;
        


        if (NetworkManager.Singleton.ConnectedClients.Count >= MaxPlayer)
        {
            
            response.Approved = false;
            response.Reason = "人数满了";

        }
        Debug.Log(NetworkManager.Singleton.ConnectedClients.Count);
        response.Pending = false;//
    }
}
