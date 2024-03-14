using UnityEngine;
using Unity.Netcode;
using JetBrains.Annotations;
using TMPro;
using System.Collections;

public class PlayerController : NetworkBehaviour 
{
    [SerializeField] private float speed = 3f;//序列化字段 以便出现在检查器中

    [CanBeNull]public static event System.Action GameOverEvent;//确保不会有空引用    
    
    private Camera _mainCamera;
    private Vector3 _mouseInput = Vector3.zero;
    private PlayerLength _playerLength;
    private WaitForSeconds _waitForSeconds = new WaitForSeconds(0.5f);
    private bool _canCollide = true;

    private readonly ulong[] _targetClientsArray = new ulong[1];//数组本身不会改变 我们只需要发送一个客户端

    private void Initialize()
    {
        _mainCamera = Camera.main;
        _playerLength = GetComponent<PlayerLength>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkDespawn();
        Initialize();//为什么不在Awake里启动 是为了防止在第一次运行中在update不引用相机的情况
    }

    private void Update()
    {
        
        if (!IsOwner||  !Application.isFocused) return;//只能在编辑器上才能移动玩家 有助于在多个编辑器上测试

        MovePlayerServer();

    }
    private void MovePlayerServer()//在客户端上执行 发送Rpc到服务器 服务器再执行
    {
        _mouseInput.x = Input.mousePosition.x;
        _mouseInput.y = Input.mousePosition.y;
        _mouseInput.z = _mainCamera.nearClipPlane;
        Vector3 mouseWorldCoordinates = _mainCamera.ScreenToWorldPoint(_mouseInput);
        mouseWorldCoordinates.z = 0f;
    
        MovePlayerServerRpc(mouseWorldCoordinates);
    
    }

    [ServerRpc]
    private void MovePlayerServerRpc(Vector3 mouseWorldCoordinates)//不在客户端上执行代码 这样更安全 通过ServerRpc向服务器传输鼠标获取的世界坐标值 更新客户的位置
    {
        transform.position = Vector3.MoveTowards(transform.position, mouseWorldCoordinates, Time.deltaTime * speed);

        //Rotate
        if (mouseWorldCoordinates != transform.position)
        {
            Vector3 targetDirection = mouseWorldCoordinates - transform.position;
            targetDirection.z = 0f;
            transform.up = targetDirection;

        }
    }


    //客户端权威运动
    private void MovePlayerClient()
    {
        _mouseInput.x = Input.mousePosition.x;
        _mouseInput.y = Input.mousePosition.y;
        _mouseInput.z = _mainCamera.nearClipPlane;//where the floor is ~z = 0  可以将投影到远处的矩形的未投影部分剪切掉
        Vector3 mouseWorldCoordinates = _mainCamera.ScreenToWorldPoint(_mouseInput);
        mouseWorldCoordinates.z = 0f;
        transform.position = Vector3.MoveTowards(transform.position, mouseWorldCoordinates, Time.deltaTime * speed);

        //Rotate
        if (mouseWorldCoordinates != transform.position)
        {
            Vector3 targetDirection = mouseWorldCoordinates - transform.position;
            targetDirection.z = 0f;
            transform.up = targetDirection;

        }
    }




    [ServerRpc]//[ServerRpc(RequireOwnership = false)]通常情况下为true 只有网络对象的拥有者可以调用  我们可以需要任何客户端都可以调用 //找出是谁发送了ServerRpc 而不是直接传输客户端  //发送的内容必须是可序列化的 需要其他参数的话 制定网络序列化
   //本质上来讲是在客户端调用ServerRpc到Server运行代码
    
    private void DetermineCollisionWinnerServerRpc(PlayerData player1, PlayerData player2)
    {
        if(player1.Length > player2.Length)
        {
            WinInformationServerRpc(player1.Id, player2.Id);

        }
        else
        {
            WinInformationServerRpc(player2.Id, player1.Id);
        }
    }
    [ServerRpc]
    private void WinInformationServerRpc(ulong winner, ulong loser)
    {
        _targetClientsArray[0] = winner;//排完序后赢家是第一个索引
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = _targetClientsArray
            }
        };
        AtePlayerClientRpc(clientRpcParams);//参数传入

        _targetClientsArray[0] = loser;

        clientRpcParams.Send.TargetClientIds = _targetClientsArray;

        GameOverClientRpc(clientRpcParams);//不懂Rpc
    }

    [ClientRpc]//确保可以发送到客户端
    //在服务器上使用ClientRpc 在客户端上运行代码 可以使用参数传递
    private void AtePlayerClientRpc(ClientRpcParams clientRpcParams = default)//调用这个函数时 将创造自己的ClientRpcParams作用到player
    {
        if(!IsOwner) return;//我们是脚本的拥有者才能执行Debug
        Debug.Log("你吃了一个玩家");
    }

    [ClientRpc]
    private void GameOverClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (IsOwner) return;
        Debug.Log("你输了");
        GameOverEvent?.Invoke();
        NetworkManager.Singleton.Shutdown();

    }


    private IEnumerator CollisionCheckCoroutine()
    {
        _canCollide = false;
        yield return _waitForSeconds;
        _canCollide = true;
    }



    private void OnCollisionEnter2D(Collision2D col)//功能在客户端执行 在客户端上构建这些数据
    {
        Debug.Log("碰撞");
        if (!col.gameObject.CompareTag("Player")) return;//先否定 提前返回 良好的编码习惯
        if (!IsOwner) return;
        if (!_canCollide) return;
        StartCoroutine(CollisionCheckCoroutine());

        //头部撞击
        if(col.gameObject.TryGetComponent(out PlayerLength playerLength))
        {
            Debug.Log("头部相撞");
            var player1 = new PlayerData() { Id = OwnerClientId, Length =_playerLength.length.Value };//获得本对象想要序列化的参数
            var player2 = new PlayerData() { Id = playerLength.OwnerClientId, Length = playerLength.length.Value };//撞击对象的参数

            DetermineCollisionWinnerServerRpc(player1, player2);//使用这些方法 让其在服务器上决定胜负 然后返回客户端

        }
        else if (col.gameObject.TryGetComponent(out Tail tail))
        {
            Debug.Log("尾部相撞");
            WinInformationServerRpc(tail.networkedOwner.GetComponent<PlayerController>().OwnerClientId, OwnerClientId);
        }


    }

    struct PlayerData : INetworkSerializable//结构化 网络可序列化中扩展  
    {
        //这样就能输入我们想要序列化的数据
        public ulong Id;//客户端id
        public ushort Length;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);//不能直接输入值 要传递改值的引用  
            serializer.SerializeValue(ref Length);
        }
    }
    
}












