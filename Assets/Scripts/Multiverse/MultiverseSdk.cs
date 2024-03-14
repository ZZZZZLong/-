// Copyright 2019 Google LLC
// All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// Modified from https://github.com/googleforgames/agones unity sdk

using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Tasks.Task;
using Unity.Cn.Multiverse.Model;
using MiniJSON;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace Unity.Cn.Multiverse
{
    /// <summary>
    /// Multiverse SDK for Unity.
    /// </summary>
    public class MultiverseSdk : MonoBehaviour
    {
        /// <summary>
        /// Interval of the server sending a health ping to the Multiverse sidecar.
        /// </summary>
        [Range(0.01f, 5)] public float healthIntervalSecond = 5.0f;

        /// <summary>
        /// Whether the server sends a health ping to the Multiverse sidecar.
        /// </summary>
        public bool healthEnabled = true;

        /// <summary>
        /// Debug Logging Enabled. Debug logging for development of this Plugin.
        /// </summary>
        public bool logEnabled = false;

        protected string SidecarAddress;
        protected string MatchAddress;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private const string k_MatchPropertiesKey = "MATCH_PROPERTIES";
        private const string k_RoomIdKey = "ROOM_ID";
        private const string k_AppIdKey = "UOS_APP_ID";
        private const string k_AppSecretKey = "UOS_APP_SECRET";
        private const string k_ConfigIdKey = "MATCH_CONFIG_ID";
        private const string k_RegionIdKey = "REGION_ID";

        private volatile bool IsWatchingBackfill;
        private readonly object _backfillWatchSyncRoot = new object();
        private long m_BackfillUpdatedAt;
        private event Action<List<Team>> backfillUpdated;

        public MultiverseSdk()
        {
        }

        public MultiverseSdk(string name, HideFlags hideFlags, string tag, bool enabled, bool useGUILayout, bool runInEditMode, float healthIntervalSecond, bool healthEnabled, bool logEnabled, string sidecarAddress)
        {
            ((Object)this).name = name;
            ((Object)this).hideFlags = hideFlags;
            ((Component)this).tag = tag;
            ((Behaviour)this).enabled = enabled;
            base.useGUILayout = useGUILayout;
            #if UNITY_EDITOR
            base.runInEditMode = runInEditMode;
            #endif
            this.healthIntervalSecond = healthIntervalSecond;
            this.healthEnabled = healthEnabled;
            this.logEnabled = logEnabled;
            this.SidecarAddress = sidecarAddress;
        }

        private struct KeyValueMessage
        {
            public string key;
            public string value;
            public KeyValueMessage(string k, string v) => (key, value) = (k, v);
        }

        private struct AcquireCPUBoostRequest
        {
            public string boostFactor;
            public int duration;
            public AcquireCPUBoostRequest(string k, int v) => (boostFactor, duration) = (k, v);
        }

        #region Unity Methods
        // Use this for initialization.
        private void Awake()
        {
            MatchAddress = Environment.GetEnvironmentVariable("MATCHMAKING_ENDPOINT") ?? "https://m.unity.cn";
            String port = Environment.GetEnvironmentVariable("MULTIVERSE_SDK_HTTP_PORT");
            SidecarAddress = "http://localhost:" + (port ?? "9358");
        }

        private void Start()
        {
            HealthCheckAsync();
        }

        private void OnApplicationQuit()
        {
            cancellationTokenSource.Dispose();
            backfillUpdated = null;
        }
        #endregion

        #region MultiverseRestClient Public Methods

        /// <summary>
        /// Async method that waits to connect to the SDK Server. Will timeout
        /// and return false after 30 seconds.
        /// </summary>
        /// <returns>A task that indicated whether it was successful or not</returns>
        public async Task<bool> Connect()
        {
            for (var i = 0; i < 30; i++)
            {
                Log($"Attempting to connect...{i + 1}");
                try
                {
                    var gameServer = await GameServer();
                    if (gameServer != null)
                    {
                        Log("Connected!");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Log($"Connection exception: {ex.Message}");
                }

                Log("Connection failed, retrying.");
                await Task.Delay(1000);
            }

            return false;
        }

        /// <summary>
        /// create a multiverseSdk instance and try to connect sdk server
        /// </summary>
        /// <returns>
        /// a multiverseSdk instance that successfully connected to sdk server
        /// </returns>
        public static async Task<MultiverseSdk> CreateInstance()
        {
            var obj = new GameObject(typeof(MultiverseSdk).ToString());
            var sdk = obj.AddComponent<MultiverseSdk>();
            var ok = await sdk.Connect();
            if (!ok)
            {
                return null!;
            }
            return sdk;
        }


        /// <summary>
        /// Marks this Game Server as ready to receive connections.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation and returns true if the request was successful.
        /// </returns>
        public async Task<bool> Ready()
        {
            return await SendRequestAsync(SidecarAddress, "/ready", "{}").ContinueWith(task => task.Result.ok);
        }

        /// <summary>
        /// Retrieve the GameServer details
        /// </summary>
        /// <returns>The current GameServer configuration</returns>
        public async Task<GameServer> GameServer()
        {
            var result = await SendRequestAsync(SidecarAddress,"/gameserver", "{}", "",UnityWebRequest.kHttpVerbGET);
            if (!result.ok)
            {
                return null;
            }

            var data = Json.Deserialize(result.json) as Dictionary<string, object>;
            return new GameServer(data);
        }

        /// <summary>
        /// Retrieve the GameServer labels
        /// </summary>
        /// <returns>The current GameServer labels</returns>
        public async Task<Dictionary<string, string> > GetLabels()
        {
            var result = await SendRequestAsync(SidecarAddress,"/gameserver", "{}", "",UnityWebRequest.kHttpVerbGET);
            if (!result.ok)
            {
                return null;
            }

            var data = Json.Deserialize(result.json) as Dictionary<string, object>;
            var gs = new GameServer(data);
            var retVal = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> label in gs.ObjectMeta.Labels)
            {
                if (!label.Key.StartsWith("agones"))
                {
                    retVal.Add(label.Key, label.Value);
                }
            }

            return retVal;
        }
        
        public Dictionary<string, string> GetLabelEnvs(GameServer gs)
        {
            var retVal = new Dictionary<string, string>();
            if (gs.ObjectMeta.Labels != null)
            {
                foreach (KeyValuePair<string, string> label in gs.ObjectMeta.Labels)
                {
                    retVal.Add(label.Key, label.Value);
                }
            }
            if (gs.Spec.Env != null)
            {
                foreach (KeyValuePair<string, string> env in gs.Spec.Env)
                {
                    retVal.Add(env.Key, env.Value);
                }
            }
            return retVal;
        }

        public async Task<Dictionary<string, string>> GetLabelEnvs()
        {
            var gs = await GameServer();
            if (gs == null)
            {
                return null;
            }
            return GetLabelEnvs(gs);
        }
        
        /// <summary>
        /// GetExpireAt returns the timestamp when the gameserver will be shutdown.
        /// This value is defined by game server TTL.
        /// Zero value means that the game server will be alive until calling Shutdown or Deallocate.
        /// </summary>
        public async Task<long> GetExpireAt()
        {
            var result = await SendRequestAsync(SidecarAddress,"/gameserver", "{}", "",UnityWebRequest.kHttpVerbGET);
            if (!result.ok)
            {
                return 0;
            }
            var data = Json.Deserialize(result.json) as Dictionary<string, object>;
            var gs = new GameServer(data);
            if (gs.ObjectMeta.Labels != null)
            {
                var expireAt = gs.ObjectMeta.Labels["ExpireAt"];
                return Convert.ToInt64(expireAt);
            }
            else
            {
                return 0;
            }
        }        

        /// <summary>
        /// Marks this Game Server as ready to shutdown.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation and returns true if the request was successful.
        /// </returns>
        public async Task<bool> Shutdown()
        {
            return await SendRequestAsync(SidecarAddress,"/shutdown", "{}").ContinueWith(task => task.Result.ok);
        }

        /// <summary>
        /// Marks this Game Server as Allocated.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation and returns true if the request was successful.
        /// </returns>
        public async Task<bool> Allocate()
        {
            return await SendRequestAsync(SidecarAddress,"/allocate", "{}").ContinueWith(task => task.Result.ok);
        }

        /// <summary>
        /// Set a metadata label that is stored in k8s.
        /// </summary>
        /// <param name="key">label key</param>
        /// <param name="value">label value</param>
        /// <returns>
        /// A task that represents the asynchronous operation and returns true if the request was successful.
        /// </returns>
        public async Task<bool> SetLabel(string key, string value)
        {
            string json = JsonUtility.ToJson(new KeyValueMessage(key, value));
            return await SendRequestAsync( SidecarAddress,"/metadata/label", json, "",UnityWebRequest.kHttpVerbPUT)
                .ContinueWith(task => task.Result.ok);
        }

        public async Task<bool> AcquireCPUBoost(string boostFactor, int duration)
        {
            string json = JsonUtility.ToJson(new AcquireCPUBoostRequest(boostFactor, duration));
            return await SendRequestAsync(SidecarAddress,"/acquire-cpu", json)
                .ContinueWith(task => task.Result.ok);
        }
        /// <summary>
        /// Set a metadata annotation that is stored in k8s.
        /// </summary>
        /// <param name="key">annotation key</param>
        /// <param name="value">annotation value</param>
        /// <returns>
        /// A task that represents the asynchronous operation and returns true if the request was successful.
        /// </returns>
        public async Task<bool> SetAnnotation(string key, string value)
        {
            string json = JsonUtility.ToJson(new KeyValueMessage(key, value));
            return await SendRequestAsync(SidecarAddress,"/metadata/annotation", json, "",UnityWebRequest.kHttpVerbPUT)
                .ContinueWith(task => task.Result.ok);
        }
        
        private struct Duration
        {
            public int seconds;

            public Duration(int seconds)
            {
                this.seconds = seconds;
            }
        }

        /// <summary>
        /// Move the GameServer into the Reserved state for the specified Timespan (0 seconds is forever)
        /// Smallest unit is seconds.
        /// </summary>
        /// <param name="duration">The time span to reserve for</param>
        /// <returns>
        /// A task that represents the asynchronous operation and returns true if the request was successful
        /// </returns>
        public async Task<bool> Reserve(TimeSpan duration)
        {
            string json = JsonUtility.ToJson(new Duration(seconds: duration.Seconds));
            return await SendRequestAsync(SidecarAddress,"/reserve", json).ContinueWith(task => task.Result.ok);
        }

        /// <summary>
        /// WatchGameServerCallback is the callback that will be executed every time
        /// a GameServer is changed and WatchGameServer is notified
        /// </summary>
        /// <param name="gameServer">The GameServer value</param>
        public delegate void WatchGameServerCallback(GameServer gameServer);
        

        /// <summary>
        /// WatchGameServer watches for changes in the backing GameServer configuration.
        /// </summary>
        /// <param name="callback">This callback is executed whenever a GameServer configuration change occurs</param>
        public void WatchGameServer(WatchGameServerCallback callback)
        {
            var req = new UnityWebRequest(SidecarAddress + "/watch/gameserver", UnityWebRequest.kHttpVerbGET);
            req.downloadHandler = new GameServerHandler(callback);
            req.SetRequestHeader("Content-Type", "application/json");
            req.SendWebRequest();
            Log("Multiverse Watch Started");
        }
        
        /// <summary>
        /// Get Matchmaker data from matchmaking
        /// </summary>
        public async Task<(List<Team>, BackfillAPIError)> GetMatchProperties()
        {
            var labelEnvs = await GetLabelEnvs();
            if (!labelEnvs.ContainsKey(k_MatchPropertiesKey))
            {
                return (null, new BackfillAPIError("can not get MatchProperties without matchmaking related env"));
            }

            return (JsonConvert.DeserializeObject<List<Team>>(labelEnvs[k_MatchPropertiesKey]), null);
        }
        
        /// <summary>
        /// Get Matchmaker data from matchmaking backfill
        /// </summary>
        /// <returns></returns>
        public async Task<(List<Team>, BackfillAPIError)> GetMatchPropertiesFromBackfill()
        {
            var (backfillData, errMsg) = await GetBackfill();
            if (errMsg != null)
            {
                return (null, new BackfillAPIError( $"failed to get matchProperties from backfill: err: {errMsg}"));
            }
            return (JsonConvert.DeserializeObject<List<Team>>(backfillData.MatchProperties), null);
        }
        
        /// <summary>
        /// Get Matchmaker data from gameServer
        /// you can use it when the backfill has not been started
        /// </summary>
        public async Task<(List<Team>, BackfillAPIError)> GetMatchProperties(GameServer gs)
        {
            var labelEnvs =  GetLabelEnvs(gs);
            
            if (!labelEnvs.ContainsKey(k_MatchPropertiesKey))
            {
                return (null, new BackfillAPIError("can not get MatchProperties without matchmaking related env"));
            }

            return (JsonConvert.DeserializeObject<List<Team>>(labelEnvs[k_MatchPropertiesKey]), null);
        }
        
        /// <summary>
        /// start backfill to match more players for the game
        /// </summary>
        public async Task<BackfillAPIError> StartBackfill(List<Team> teams)
        {
            var gs = await GameServer();
            if (gs == null)
            {
                return new BackfillAPIError("failed to get gameServer");
            }
            if (gs.Status.Address == "")
            {
                return new BackfillAPIError("can not start backfill for current gameServer's status");
            }
            var labelEnvs = GetLabelEnvs(gs);
            if (labelEnvs == null)
            {
                return new BackfillAPIError("failed to get labelEnvs");
            }

            if (!labelEnvs.ContainsKey(k_ConfigIdKey))
            {
                return new BackfillAPIError("can not create backfill without matchmaking related env");
            }

            string gsPortStr = "";
            for (var i = 0; i < gs.Status.Ports.Count; i++)
            {
                 gsPortStr += $"{gs.Status.Ports[i].Name}/{gs.Status.Ports[i].Port}";
                 if (i != gs.Status.Ports.Count - 1)
                 {
                     gsPortStr += ",";
                 }
            }
            
            var backfill = new Backfill
            {
                 AppId = labelEnvs[k_AppIdKey],
                 ConfigId = labelEnvs[k_ConfigIdKey],
                 RoomId = labelEnvs[k_RoomIdKey],  
                 RegionId = labelEnvs[k_RegionIdKey],
                 Ip = gs.Status.Address,
                 GamePorts = gsPortStr,
                 MatchProperties = JsonConvert.SerializeObject(teams)
            };
            
            var result = await SendRequestAsync(MatchAddress, "/v1/backfill/start", JsonConvert.SerializeObject(backfill), GetMmAuthHeader(labelEnvs));
            if (!result.ok)
            {
                var serverError = JsonConvert.DeserializeObject<ServerError>(result.json);
                return new BackfillAPIError($"failed to create backfill, err: {serverError.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get backfill info
        /// </summary>
        public async Task<(Backfill, BackfillAPIError)> GetBackfill()
        {
            var labelEnvs = await GetLabelEnvs();

            if (!labelEnvs.ContainsKey(k_ConfigIdKey))
            {
                return (null, new BackfillAPIError("can not get backfill without matchmaking related env"));
            }
            var result = await SendRequestAsync(MatchAddress, $"/v1/backfill?roomId={labelEnvs[k_RoomIdKey]}", "", GetMmAuthHeader(labelEnvs),UnityWebRequest.kHttpVerbGET);
            if (!result.ok)
            {
                var serverError = JsonConvert.DeserializeObject<ServerError>(result.json);
                return (null, new BackfillAPIError($"failed to get backfill, err: {serverError.Message}"));
            }
            return (JsonConvert.DeserializeObject<Backfill>(result.json), null);
        }

        /// <summary>
        /// Stop backfill
        /// </summary>
        public async Task<(Backfill, BackfillAPIError)> StopBackfill()
        {
            var labelEnvs = await GetLabelEnvs();
            if (!labelEnvs.ContainsKey(k_ConfigIdKey))
            {
                return (null, new BackfillAPIError("can not stop backfill without matchmaking related env"));
            }
            var result =
                await SendRequestAsync(MatchAddress, $"/v1/backfill/stop?roomId={k_RoomIdKey}", "",  GetMmAuthHeader(labelEnvs),UnityWebRequest.kHttpVerbDELETE);
            if (!result.ok)
            {
                var serverError = JsonConvert.DeserializeObject<ServerError>(result.json);
                return (null, new BackfillAPIError($"failed to stop backfill, err: {serverError.Message}"));
            }
            return (JsonConvert.DeserializeObject<Backfill>(result.json), null);
        }
        
        /// <summary>
		/// Starts watching the Backfill updates in the background in it's own task.
		/// On update, it fires the BackfillUpdate event.
		/// </summary>
		private async Task BeginInternalWatchBackfillAsync()
		{
			// Begin WatchBackfill in the background for the provided callback(s).
			while (!cancellationTokenSource.IsCancellationRequested)
			{
				try
                {
                    var (backfillData, err) = await GetBackfill();
					if (err != null)
					{
                        if (err.ToString().Contains("backfill not found"))
                        {
                            Debug.Log("backfill not found, WatchBackfill stopped");
                            return;
                        }
						Debug.Log($"error watchBackfill: failed to get backfill, err: {err}");
                        Thread.Sleep(1000);
						continue;
					}
					if (backfillData.UpdatedAt != m_BackfillUpdatedAt)
					{
						try
						{
							var teams = JsonConvert.DeserializeObject<List<Team>>(backfillData.MatchProperties);
							if (teams == null)
							{
                                Debug.Log("failed to get backfill: failed to convert matchProperties");
                                Thread.Sleep(1000);
								continue;
							}
							backfillUpdated?.Invoke(teams);
							m_BackfillUpdatedAt = backfillData.UpdatedAt;
						}
						catch (Exception ex)
						{
							// Swallow any exception thrown here. We don't want a callback's exception to cause
							// our watch to be torn down.
                            Debug.Log($"A {nameof(WatchBackfill)} callback threw an exception, ex: {ex.Message}");
						}
					}
				}
				catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
				{
					return;
				}
                Thread.Sleep(1000);
			}
		}

        /// <summary>
		/// This executes the passed in callback with the current Backfill details whenever the underlying Backfill is updated.
		/// This can be useful to track Backfill metadata changes.
		/// </summary>
		/// <param name="callback">The action to be called when the underlying Backfill metadata changes.</param>
		public void WatchBackfill(Action<List<Team>> callback)
		{
			backfillUpdated += callback;
			
			lock (_backfillWatchSyncRoot)
			{
				if (IsWatchingBackfill)
				{
					return;
				}
				
				IsWatchingBackfill = true;
			}

            // Kick off the watch in a task so the caller doesn't need to handle exceptions that could potentially be
            // thrown before reaching the first yielding async point.
            Run(async () => await BeginInternalWatchBackfillAsync(), cancellationTokenSource.Token);
        }
        
        #endregion

        #region MultiverseRestClient Private Methods

        private async void HealthCheckAsync()
        {
            while (healthEnabled)
            {
                await Task.Delay(TimeSpan.FromSeconds(healthIntervalSecond));

                try
                {
                    await SendRequestAsync(SidecarAddress,"/health", "{}");
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

            }
        }

        /// <summary>
        /// Result of a Async HTTP request
        /// </summary>
        protected struct AsyncResult
        {
            public bool ok;
            public string json;
        }

        private string GetMmAuthHeader(Dictionary<string, string> labelEnvs)
        {
            return  $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{labelEnvs[k_AppIdKey]}:{labelEnvs[k_AppSecretKey]}"))}";
        }

        protected async Task<AsyncResult> SendRequestAsync(string endpoint, string api, string json, string authHeader = "",
            string method = UnityWebRequest.kHttpVerbPOST)
        {
            // To prevent that an async method leaks after destroying this gameObject.
            cancellationTokenSource.Token.ThrowIfCancellationRequested();

            var req = new UnityWebRequest(endpoint + api, method)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            req.SetRequestHeader("Content-Type", "application/json");
            if (endpoint == MatchAddress)
            {
                req.SetRequestHeader("Authorization", $"{authHeader}");
            }

            await new MultiverseAsyncOperationWrapper(req.SendWebRequest());

            var result = new AsyncResult();

            result.ok = req.responseCode == (long) HttpStatusCode.OK;
            result.json = req.downloadHandler.text;

            if (result.ok)
            {
                Log($"Multiverse SendRequest ok: {api} {req.downloadHandler.text}");
            }
            else
            {
                Log($"Multiverse SendRequest failed: {api} {req.error}");
            }

            return result;
        }

        private void Log(object message)
        {
            if (!logEnabled)
            {
                return;
            }

            Debug.Log(message);
        }
        #endregion

        #region MultiverseRestClient Nested Classes
        private class MultiverseAsyncOperationWrapper
        {
            public UnityWebRequestAsyncOperation AsyncOp { get; }
            public MultiverseAsyncOperationWrapper(UnityWebRequestAsyncOperation unityOp)
            {
                AsyncOp = unityOp;
            }

            public MultiverseAsyncOperationAwaiter GetAwaiter()
            {
                return new MultiverseAsyncOperationAwaiter(this);
            }
        }

        private class MultiverseAsyncOperationAwaiter : INotifyCompletion
        {
            private UnityWebRequestAsyncOperation asyncOp;
            private Action continuation;
            public bool IsCompleted => asyncOp.isDone;

            public MultiverseAsyncOperationAwaiter(MultiverseAsyncOperationWrapper wrapper)
            {
                asyncOp = wrapper.AsyncOp;
                asyncOp.completed += OnRequestCompleted;
            }

            // C# Awaiter Pattern requires that the GetAwaiter method has GetResult(),
            // And MultiverseAsyncOperationAwaiter does not return a value in this case.
            public void GetResult()
            {
                asyncOp.completed -= OnRequestCompleted;
            }

            public void OnCompleted(Action continuation)
            {
                this.continuation = continuation;
            }

            private void OnRequestCompleted(AsyncOperation _)
            {
                continuation?.Invoke();
                continuation = null;
            }
        }

        /// <summary>
        /// Custom UnityWebRequest http data handler
        /// that fires a callback whenever it receives data
        /// from the SDK.Watch() REST endpoint
        /// </summary>
        private class GameServerHandler : DownloadHandlerScript
        {
            private WatchGameServerCallback callback;
            public GameServerHandler(WatchGameServerCallback callback)
            {
                this.callback = callback;
            }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                string json = Encoding.UTF8.GetString(data);
                var dictionary = (Dictionary<string, object>) Json.Deserialize(json);
                var gameServer = new GameServer(dictionary["result"] as Dictionary<string, object>);
                callback(gameServer);
                return true;
            }
        }
        
        #endregion
    }
}
