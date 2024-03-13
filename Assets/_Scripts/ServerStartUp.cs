
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Cn.Multiverse;
using UnityEngine;

public class ServerStartUp : MonoBehaviour
{
    private MultiverseSdk m_Multiverse;
    private string m_GameId = "your-game-id";
    private string m_AllocationId = "your-allocation-id";
    private string m_ProfileId = "your-profile-id";
    private string m_Region = "your-region";

    private void Start()
    {
        InitializeMultiverseSdk();
    }

    private async void InitializeMultiverseSdk()
    {
        // ���� MultiverseSdk ʵ��
        m_Multiverse = await MultiverseSdk.CreateInstance();

        // ��Ƿ���������
        bool ready = await m_Multiverse.Ready();
        if (!ready)
        {
            Debug.LogError("Failed to mark server as ready.");
            return;
        }

        // ��ȡ Label �ͻ�������
        Dictionary<string, string> labelEnvs = await m_Multiverse.GetLabelEnvs();
        if (labelEnvs == null)
        {
            Debug.LogError("Failed to get label and envs.");
            return;
        }

        // ��ȡ��Ϸ��������ϸ��Ϣ
        var gameServer = await m_Multiverse.GameServer();
        if (gameServer == null)
        {
            Debug.LogError("Failed to get game server info.");
            return;
        }

        // ������Ϸ������״̬
        m_Multiverse.WatchGameServer(server =>
        {
            if (server.Status.State == "Allocated")
            {
                Debug.Log("Game server allocated.");
            }
            else if (server.Status.State == "Terminated")
            {
                Debug.Log("Game server terminated.");
            }
        });

        // ���ӵ���Ϸ������
        string gameServerAddress = gameServer.Status.Address;
        int gameServerPort = gameServer.Status.Ports["default"];
        bool connected = ConnectToGameServer(gameServerAddress, gameServerPort);
        if (!connected)
        {
            Debug.LogError("Failed to connect to game server.");
            return;
        }

        Debug.Log("Multiverse SDK initialized.");
    }

    private bool ConnectToGameServer(string address, int port)
    {
        // TODO: �������д���ӵ���Ϸ�������Ĵ���
        return true;
    }

    private async void OnDestroy()
    {
        // �ر���Ϸ������
        bool shutdown = await m_Multiverse.Shutdown();
        if (!shutdown)
        {
            Debug.LogError("Failed to shutdown game server.");
        }

        // ���� MultiverseSdk ʵ��

    }
}
