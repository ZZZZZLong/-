using System;
using System.Collections;
using System.Collections.Generic;
using Unity.BossRoom.Infrastructure;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

public class FoodsSpawner : MonoBehaviour
{
    [SerializeField] private GameObject prefab;

    private const int MaxPrefabCount = 50;

    private void Start()
    {
        NetworkManager.Singleton.OnServerStarted += SpawnFoodStart;
    }

    private void SpawnFoodStart()
    {
        NetworkManager.Singleton.OnServerStarted -= SpawnFoodStart;
        NetworkObjectPool.Singleton.InitializePool();
        for (int i = 0; i < 30; i++)
        {
            SpawnFood();    
        }

        StartCoroutine(SpawnOverTime());
    }
    private void SpawnFood()
    {
        NetworkObject obj = NetworkObjectPool.Singleton.GetNetworkObject(prefab,GetRandomPositionOnMap(),Quaternion.identity);
        obj.GetComponent<Food>().prefab = prefab;
        if(!obj.IsSpawned)obj.Spawn(true);
        

    }

    private Vector3 GetRandomPositionOnMap()
    {
        return new Vector3(UnityEngine.Random.Range(-9f, 9f), UnityEngine.Random.Range(-5f, 5f), 0f);

    }

    private IEnumerator SpawnOverTime()
    {
        while(NetworkManager.Singleton.ConnectedClients.Count > 0)
        {
            yield return new WaitForSeconds(2f);
            if (NetworkObjectPool.Singleton.GetCurrentPrefabCount(prefab) < MaxPrefabCount)
            SpawnFood();
        }
    }








}
