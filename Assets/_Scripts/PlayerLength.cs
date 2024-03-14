using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerLength : NetworkBehaviour
{
    [SerializeField] private GameObject TailPrefabs;

    public NetworkVariable<ushort> length = new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);//网络变量 在服务器更新后客户端也能同步更新  默认情况下只有Server可以改变这个值 
    //如果希望客户修改 就要覆盖权限 在后面NetworkVariableWritePermission.Onwer 这将使其变成客户端权威 目前是默认情况

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
        if (!IsServer) length.OnValueChanged += LengthChangedEvent;//通过订阅长度改变事件来实现网络实例化 可以将客户端的尾巴长度加长
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        DestroyTails();
    }

    private void DestroyTails()
    {
        while(_tails.Count != 0)//列表有东西的时候
        {
            GameObject tail = _tails[0];
            _tails.RemoveAt(0);
            Destroy(tail);
        }
    }


    
    public void AddLength()//本地实例化 只能在主机看见 客户端没有产生尾巴
    {
        length.Value += 1;
        LengthChanged();

    }//实现尾巴加长的逻辑是 先在本地实例化（指的是可以在主机看见 但是在客户端看不见）后 value值发生改变 订阅了这个事件的函数在所有的客户端更新

    private void LengthChanged()
    {
        
        InsantiateTail();

        if (!IsOwner) return;
        ChangedLengthEvent?.Invoke(length.Value);
        ClientMusicPlayer.Instance.PlayNomAudioClip();//长度改变后加的声音
    }


    private void LengthChangedEvent(ushort previousValue , ushort newValue)
    {
        Debug.Log("长度改变 callback");
        LengthChanged();
    }

    private void InsantiateTail() //产生尾巴 尾巴跟随 
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
