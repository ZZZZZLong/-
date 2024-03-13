using UnityEngine;
using Unity.Netcode;
using JetBrains.Annotations;
using TMPro;
using System.Collections;

public class PlayerController : NetworkBehaviour 
{
    [SerializeField] private float speed = 3f;//���л��ֶ� �Ա�����ڼ������

    [CanBeNull]public static event System.Action GameOverEvent;//ȷ�������п�����    
    
    private Camera _mainCamera;
    private Vector3 _mouseInput = Vector3.zero;
    private PlayerLength _playerLength;
    private WaitForSeconds _waitForSeconds = new WaitForSeconds(0.5f);
    private bool _canCollide = true;

    private readonly ulong[] _targetClientsArray = new ulong[1];//���鱾����ı� ����ֻ��Ҫ����һ���ͻ���

    private void Initialize()
    {
        _mainCamera = Camera.main;
        _playerLength = GetComponent<PlayerLength>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkDespawn();
        Initialize();//Ϊʲô����Awake������ ��Ϊ�˷�ֹ�ڵ�һ����������update��������������
    }

    private void Update()
    {
        
        if (!IsOwner||  !Application.isFocused) return;//ֻ���ڱ༭���ϲ����ƶ���� �������ڶ���༭���ϲ���

        MovePlayerServer();

    }
    private void MovePlayerServer()//�ڿͻ�����ִ�� ����Rpc�������� ��������ִ��
    {
        _mouseInput.x = Input.mousePosition.x;
        _mouseInput.y = Input.mousePosition.y;
        _mouseInput.z = _mainCamera.nearClipPlane;
        Vector3 mouseWorldCoordinates = _mainCamera.ScreenToWorldPoint(_mouseInput);
        mouseWorldCoordinates.z = 0f;
    
        MovePlayerServerRpc(mouseWorldCoordinates);
    
    }

    [ServerRpc]
    private void MovePlayerServerRpc(Vector3 mouseWorldCoordinates)//���ڿͻ�����ִ�д��� ��������ȫ ͨ��ServerRpc���������������ȡ����������ֵ ���¿ͻ���λ��
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


    //�ͻ���Ȩ���˶�
    private void MovePlayerClient()
    {
        _mouseInput.x = Input.mousePosition.x;
        _mouseInput.y = Input.mousePosition.y;
        _mouseInput.z = _mainCamera.nearClipPlane;//where the floor is ~z = 0  ���Խ�ͶӰ��Զ���ľ��ε�δͶӰ���ּ��е�
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




    [ServerRpc]//[ServerRpc(RequireOwnership = false)]ͨ�������Ϊtrue ֻ����������ӵ���߿��Ե���  ���ǿ�����Ҫ�κοͻ��˶����Ե��� //�ҳ���˭������ServerRpc ������ֱ�Ӵ���ͻ���  //���͵����ݱ����ǿ����л��� ��Ҫ���������Ļ� �ƶ��������л�
   //�������������ڿͻ��˵���ServerRpc��Server���д���
    
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
        _targetClientsArray[0] = winner;//�������Ӯ���ǵ�һ������
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = _targetClientsArray
            }
        };
        AtePlayerClientRpc(clientRpcParams);//��������

        _targetClientsArray[0] = loser;

        clientRpcParams.Send.TargetClientIds = _targetClientsArray;

        GameOverClientRpc(clientRpcParams);//����Rpc
    }

    [ClientRpc]//ȷ�����Է��͵��ͻ���
    //�ڷ�������ʹ��ClientRpc �ڿͻ��������д��� ����ʹ�ò�������
    private void AtePlayerClientRpc(ClientRpcParams clientRpcParams = default)//�����������ʱ �������Լ���ClientRpcParams���õ�player
    {
        if(!IsOwner) return;//�����ǽű���ӵ���߲���ִ��Debug
        Debug.Log("�����һ�����");
    }

    [ClientRpc]
    private void GameOverClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (IsOwner) return;
        Debug.Log("������");
        GameOverEvent?.Invoke();
        NetworkManager.Singleton.Shutdown();

    }


    private IEnumerator CollisionCheckCoroutine()
    {
        _canCollide = false;
        yield return _waitForSeconds;
        _canCollide = true;
    }



    private void OnCollisionEnter2D(Collision2D col)//�����ڿͻ���ִ�� �ڿͻ����Ϲ�����Щ����
    {
        Debug.Log("��ײ");
        if (!col.gameObject.CompareTag("Player")) return;//�ȷ� ��ǰ���� ���õı���ϰ��
        if (!IsOwner) return;
        if (!_canCollide) return;
        StartCoroutine(CollisionCheckCoroutine());

        //ͷ��ײ��
        if(col.gameObject.TryGetComponent(out PlayerLength playerLength))
        {
            Debug.Log("ͷ����ײ");
            var player1 = new PlayerData() { Id = OwnerClientId, Length =_playerLength.length.Value };//��ñ�������Ҫ���л��Ĳ���
            var player2 = new PlayerData() { Id = playerLength.OwnerClientId, Length = playerLength.length.Value };//ײ������Ĳ���

            DetermineCollisionWinnerServerRpc(player1, player2);//ʹ����Щ���� �����ڷ������Ͼ���ʤ�� Ȼ�󷵻ؿͻ���

        }
        else if (col.gameObject.TryGetComponent(out Tail tail))
        {
            Debug.Log("β����ײ");
            WinInformationServerRpc(tail.networkedOwner.GetComponent<PlayerController>().OwnerClientId, OwnerClientId);
        }


    }

    struct PlayerData : INetworkSerializable//�ṹ�� ��������л�����չ  
    {
        //������������������Ҫ���л�������
        public ulong Id;//�ͻ���id
        public ushort Length;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);//����ֱ������ֵ Ҫ���ݸ�ֵ������  
            serializer.SerializeValue(ref Length);
        }
    }
    
}












