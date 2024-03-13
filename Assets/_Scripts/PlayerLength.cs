using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerLength : NetworkBehaviour
{
    [SerializeField] private GameObject TailPrefabs;

    public NetworkVariable<ushort> length = new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);//������� �ڷ��������º�ͻ���Ҳ��ͬ������  Ĭ�������ֻ��Server���Ըı����ֵ 
    //���ϣ���ͻ��޸� ��Ҫ����Ȩ�� �ں���NetworkVariableWritePermission.Onwer �⽫ʹ���ɿͻ���Ȩ�� Ŀǰ��Ĭ�����

    [CanBeNull]public static event System.Action<ushort> ChangedLengthEvent;

    private List<GameObject> _tails;
    private Transform _lastTail;
    private Collider2D _collider2D;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _tails = new List<GameObject>();
        _lastTail = transform;//head
        _collider2D = GetComponent<Collider2D>();
        if (!IsServer) length.OnValueChanged += LengthChangedEvent;//ͨ�����ĳ��ȸı��¼���ʵ������ʵ���� ���Խ��ͻ��˵�β�ͳ��ȼӳ�
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        DestroyTails();
    }

    private void DestroyTails()
    {
        while(_tails.Count != 0)//�б��ж�����ʱ��
        {
            GameObject tail = _tails[0];
            _tails.RemoveAt(0);
            Destroy(tail);
        }
    }


    
    public void AddLength()//����ʵ���� ֻ������������ �ͻ���û�в���β��
    {
        length.Value += 1;
        LengthChanged();

    }//ʵ��β�ͼӳ����߼��� ���ڱ���ʵ������ָ���ǿ������������� �����ڿͻ��˿��������� valueֵ�����ı� ����������¼��ĺ��������еĿͻ��˸���

    private void LengthChanged()
    {
        
        InsantiateTail();

        if (!IsOwner) return;
        ChangedLengthEvent?.Invoke(length.Value);
        ClientMusicPlayer.Instance.PlayNomAudioClip();//���ȸı��ӵ�����
    }


    private void LengthChangedEvent(ushort previousValue , ushort newValue)
    {
        Debug.Log("���ȸı� callback");
        LengthChanged();
    }

    private void InsantiateTail() //����β�� β�͸��� 
    {
        GameObject tailGameObject = Instantiate(TailPrefabs , transform.position , Quaternion.identity);
        tailGameObject.GetComponent<SpriteRenderer>().sortingOrder = -length.Value;
        if(tailGameObject.TryGetComponent(out Tail tail)){
            tail.networkedOwner = transform;
            tail.followTransform = _lastTail;
            _lastTail = tailGameObject.transform;
            Physics2D.IgnoreCollision(tailGameObject.GetComponent<Collider2D>(),_collider2D);
        }


        _tails.Add(tailGameObject);
    }


}
