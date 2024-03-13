
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
        // 创建 MultiverseSdk 实例
        m_Multiverse = await MultiverseSdk.CreateInstance();

        // 标记服务器就绪
        bool ready = await m_Multiverse.Ready();
        if (!ready)
        {
            Debug.LogError("Failed to mark server as ready.");
            return;
        }

        // 获取 Label 和环境变量
        Dictionary<string, string> labelEnvs = await m_Multiverse.GetLabelEnvs();
        if (labelEnvs == null)
        {
            Debug.LogError("Failed to get label and envs.");
            return;
        }

        // 获取游戏服务器详细信息
        var gameServer = await m_Multiverse.GameServer();
        if (gameServer == null)
        {
            Debug.LogError("Failed to get game server info.");
            return;
        }

        // 监听游戏服务器状态
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

        // 连接到游戏服务器
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
        // TODO: 在这里编写连接到游戏服务器的代码
        return true;
    }

    private async void OnDestroy()
    {
        // 关闭游戏服务器
        bool shutdown = await m_Multiverse.Shutdown();
        if (!shutdown)
        {
            Debug.LogError("Failed to shutdown game server.");
        }

        // 销毁 MultiverseSdk 实例

    }
}
