
using UnityEngine;
using Unity.Netcode;

public class StartNetwork : MonoBehaviour
{
   
    public void startServer()
    {
        NetworkManager.Singleton.StartServer();
    }
    public void startHost()
    {
        NetworkManager.Singleton.StartHost();
    }
    public void startClient()
    {
        NetworkManager.Singleton.StartClient();
    }




}
