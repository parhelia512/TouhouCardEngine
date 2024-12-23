﻿using NitoriNetwork.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TouhouCardEngine.Shared;

namespace TouhouCardEngine
{
    public partial class ClientLogic : IDisposable, IRoomClient
    {
        #region 公共成员
        #region 构造器
        public ClientLogic(string name, int[] ports = null, ServerClient sClient = null, ILogger logger = null, IResourceProvider resProvider = null)
        {
            this.logger = logger;
            if (sClient != null)
                LobbyNetwork = new LobbyClientNetworking(sClient, logger: logger);
            LANNetwork = new LANNetworking(name, logger, resProvider);

            if (ports != null)
                LANPorts = ports;

            LANNetwork.broadcastPorts = LANPorts;
        }
        #endregion
        #region 房间相关
        /// <summary>
        /// 根据所处的模式，创建局域网房间或服务器房间
        /// </summary>
        /// <param name="port">发送或广播创建房间信息的端口</param>
        /// <returns></returns>
        /// <remarks>port主要是在局域网测试下有用</remarks>
        public async Task createOnlineRoom(string name = "", string password = "")
        {
            logger?.logTrace("客户端创建在线房间");
            localPlayer = curNetwork.GetSelfPlayerData();
            room = await curNetwork.CreateRoom(MAX_PLAYER_COUNT, name, password);
            isLocalRoom = false;

            // lobby.addRoom(room); // 不要在自己的房间列表里面显示自己的房间。
            //this.room.maxPlayerCount = MAX_PLAYER_COUNT;
        }
        public Task createLocalRoom(string playerName)
        {
            logger?.logTrace("客户端创建本地房间");
            room = new RoomData(string.Empty);
            localPlayer = new RoomPlayerData(Guid.NewGuid().GetHashCode(), playerName, RoomPlayerType.human);
            room.playerDataList.Add(localPlayer);
            room.ownerId = localPlayer.id;

            isLocalRoom = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// 请求房间列表
        /// </summary>
        public void refreshRoomList()
        {
            logger?.logTrace("客户端请求房间列表");
            curNetwork?.RefreshRoomList();
        }

        public async Task<bool> joinRoom(string roomId, string password = "")
        {
            logger?.logTrace($"客户端请求加入房间{roomId}");
            room = await curNetwork.JoinRoom(roomId, password);
            return room != null;
        }

        public async Task<bool> joinRoom(string addr, int port, string password = "")
        {
            if (curNetwork != LANNetwork) 
                return false;
            logger?.logTrace($"客户端请求加入房间{addr}:{port}");
            room = await LANNetwork.JoinRoom(addr, port, password);
            return room != null;
        }

        public Task addAIPlayer()
        {
            logger?.logTrace("主机添加AI玩家");
            RoomPlayerData playerData = new RoomPlayerData(Guid.NewGuid().GetHashCode(), "AI", RoomPlayerType.ai);
            var host = curNetwork as INetworkingV3LANHost;
            if (!isLocalRoom && host != null)
            {
                return host.AddPlayer(playerData);
            }
            else
            {
                // 本地玩家。
                room.playerDataList.Add(playerData);
            }

            return Task.CompletedTask;
        }

        public Task quitRoom()
        {
            logger?.logTrace($"玩家退出房间{room.ID}");
            room = null;
            if (curNetwork != null && !isLocalRoom)
                curNetwork.QuitRoom();
            return Task.CompletedTask;
        }
        #endregion
        public void update()
        {
            if (curNetwork != null)
            {
                curNetwork.update();
            }
            //curNetwork.net.PollEvents();
        }
        public void Dispose()
        {
            if (LANNetwork != null)
            {
                LANNetwork.Dispose();
                LANNetwork = null;
            }
            if (LobbyNetwork != null)
            {
                LobbyNetwork.Dispose();
                LobbyNetwork = null;
            }
        }
        public void SwitchMode(bool isLAN)
        {
            if (curNetwork != null)
            {
                // 切换网络注销事件。
                curNetwork.OnRoomListUpdate -= roomListChangeEvtHandler;
                curNetwork.OnGameStart -= roomGameStartEvtHandler;
                curNetwork.onReceive -= roomReceiveEvtHandler;
                curNetwork.OnRoomDataChange -= roomDataChangeEvtHandler;
                curNetwork.PostRoomPropChange -= roomPropChangeEvtHandler;
                curNetwork.OnRoomPlayerDataListChanged -= roomPlayerDataChangeEvtHandler;
                curNetwork.onConfirmJoinAck -= onConfirmJoinAck;
                curNetwork.OnRecvChat -= onRecvChat;
                curNetwork.OnSuggestCardPools -= onReceiveCardPoolsSuggestion;
                curNetwork.OnCardPoolsSuggestionAnwsered -= onReceiveCardPoolsSuggestionAnwsered;

                if (curNetwork == LANNetwork)
                {
                    LANNetwork.onJoinRoomReq -= onLANJoinRoomReq;
                    LANNetwork.onConfirmJoinReq -= onLANConfirmJoinReq;
                }
            }

            if (isLAN)
            {
                logger.logTrace("切换到局域网网络");
                curNetwork = LANNetwork;
            } 
            else
            {
                logger.logTrace("切换到服务器网络");
                curNetwork = LobbyNetwork;
            }

            if (!curNetwork.isRunning)
            {
                if (curNetwork == LANNetwork)
                {
                    // 以指定的端口启动
                    for (int i = 0; i < LANPorts.Length; i++)
                    {
                        if (curNetwork.start(LANPorts[i]))
                            break;
                    }
                    if (!curNetwork.isRunning)
                    {
                        curNetwork.start();
                    }
                }
                else
                {
                    curNetwork.start();
                }
            }

            curNetwork.OnRoomListUpdate += roomListChangeEvtHandler;
            curNetwork.OnGameStart += roomGameStartEvtHandler;
            curNetwork.onReceive += roomReceiveEvtHandler;
            curNetwork.OnRoomDataChange += roomDataChangeEvtHandler;
            curNetwork.PostRoomPropChange += roomPropChangeEvtHandler;
            curNetwork.OnRoomPlayerDataListChanged += roomPlayerDataChangeEvtHandler;
            curNetwork.onConfirmJoinAck += onConfirmJoinAck;
            curNetwork.OnRecvChat += onRecvChat;
            curNetwork.OnSuggestCardPools += onReceiveCardPoolsSuggestion;
            curNetwork.OnCardPoolsSuggestionAnwsered += onReceiveCardPoolsSuggestionAnwsered;

            if (curNetwork == LANNetwork)
            {
                LANNetwork.onJoinRoomReq += onLANJoinRoomReq;
                LANNetwork.onConfirmJoinReq += onLANConfirmJoinReq;
            }
        }
        public RoomPlayerData getLocalPlayerData()
        {
            return curNetwork.GetSelfPlayerData();
        }
        #endregion
        #region 私有成员
        #region 事件回调
        private void roomPlayerDataChangeEvtHandler(RoomPlayerData[] obj)
        {
            if (room == null)
                return;

            room.playerDataList = obj?.ToList();
            OnRoomPlayerDataListChanged?.Invoke(obj);
        }

        private void roomDataChangeEvtHandler(RoomData obj)
        {
            room = obj;
            OnRoomDataChange?.Invoke(obj);
        }

        private void roomPropChangeEvtHandler(string key, object value)
        {
            if (room == null)
                return;

            room?.setProp(key, value);
            PostRoomPropChange?.Invoke(key, value);
        }

        private Task roomReceiveEvtHandler(int clientID, byte[] data)
        {
            return OnReceiveData?.Invoke(clientID, data);
        }

        private void onRecvChat(ChatMsg obj)
        {
            OnRecvChat?.Invoke(obj);
        }

        private void onReceiveCardPoolsSuggestion(int playerId, CardPoolSuggestion suggestion)
        {
            OnSuggestCardPools?.Invoke(playerId, suggestion);
        }

        private void onReceiveCardPoolsSuggestionAnwsered(CardPoolSuggestion suggestion, bool agree)
        {
            OnCardPoolsSuggestionAnwsered?.Invoke(suggestion, agree);
        }

        private void roomListChangeEvtHandler(LobbyRoomDataList list)
        {
            roomList = list;
            onRoomListChange?.Invoke(list);
        }
        /// <summary>
        /// 处理玩家连接请求，判断是否可以连接
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        private RoomData onLANJoinRoomReq(RoomPlayerData player)
        {
            if (room == null)
                throw new InvalidOperationException("房间不存在");
            if (room.maxPlayerCount < 1 || room.playerDataList.Count < room.maxPlayerCount)
            {
                player.state = ERoomPlayerState.connecting;
                room.playerDataList.Add(player);
                return room;
            }
            else
                throw new InvalidOperationException("房间已满");
        }
        /// <summary>
        /// 将玩家加入房间，然后返回一个房间信息
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        private RoomData onLANConfirmJoinReq(RoomPlayerData player)
        {
            if (room == null)
                throw new InvalidOperationException("房间不存在");
            player = room.playerDataList.Find(p => p.id == player.id);
            if (player != null)
                player.state = ERoomPlayerState.connected;
            else
                throw new NullReferenceException($"房间中不存在玩家{player.name}");
            return room;
        }
        /// <summary>
        /// 房间加入完成
        /// </summary>
        /// <param name="joinedRoom"></param>
        private void onConfirmJoinAck(RoomData joinedRoom)
        {
            if (room != null)
                throw new InvalidOperationException($"已经在房间{room.ID}中");
            localPlayer = joinedRoom.getPlayer(curNetwork.GetSelfPlayerData().id);
            room = joinedRoom;
        }

        private void roomGameStartEvtHandler()
        {
            onGameStart?.Invoke();
        }
        #endregion
        #region 接口实现
        Task IRoomClient.SetRoomProp(string propName, object value)
        {
            // 非房主不能修改
            if (curNetwork == LobbyNetwork && !isRoomOwner)
            {
                return Task.CompletedTask;
            }

            logger?.logTrace($"主机更改房间属性{propName}为{value}");
            room.setProp(propName, value);
            if (curNetwork != null && !isLocalRoom)
                return curNetwork.SetRoomProp(propName, value);
            return Task.CompletedTask;
        }

        Task IRoomClient.SetRoomPropBatch(List<KeyValuePair<string, object>> values)
        {
            if (curNetwork == LobbyNetwork && !isRoomOwner)
            {
                return Task.CompletedTask;
            }

            foreach (var item in values)
            {
                logger?.logTrace($"主机更改房间属性{item.Key}为{item.Value}");
                room.setProp(item.Key, item.Value);
            }

            if (curNetwork != null && !isLocalRoom)
                return curNetwork.SetRoomPropBatch(values);
            return Task.CompletedTask;
        }

        async Task IRoomClient.SetPlayerProp(string propName, object value)
        {
            logger?.logTrace($"玩家更改玩家属性{propName}为{value}");
            room.setPlayerProp(localPlayer.id, propName, value);
            if (curNetwork != null && !isLocalRoom)
                await curNetwork.SetPlayerProp(propName, value);
        }

        Task IRoomClient.SendChat(int channel, string message)
        {
            logger?.logTrace($"[{channel}] 玩家: {message}");

            if (curNetwork != null && !isLocalRoom)
                return curNetwork.SendChat(channel, message);
            return Task.CompletedTask;
        }

        Task IRoomClient.SuggestCardPools(CardPoolSuggestion cardPools)
        {
            logger?.logTrace($"玩家提议加入卡池: {cardPools.ToString()}");

            if (curNetwork != null && !isLocalRoom)
                return curNetwork.SuggestCardPools(cardPools);
            return Task.CompletedTask;
        }

        Task IRoomClient.AnwserCardPoolsSuggestion(int playerId, CardPoolSuggestion suggestion, bool agree)
        {
            logger?.logTrace($"回应来自玩家{playerId}的加入卡池提议:{agree}。");

            if (curNetwork != null && !isLocalRoom)
                return curNetwork.AnwserCardPoolsSuggestion(playerId, suggestion, agree);
            return Task.CompletedTask;
        }

        Task<byte[]> IRoomClient.GetResourceAsync(ResourceType type, string id)
        {
            logger?.logTrace($"获取类型为{type.GetString()}，id为{id}的资源。");

            if (curNetwork != null && !isLocalRoom)
                return curNetwork.GetResourceAsync(type, id);
            return Task.FromResult<byte[]>(null);
        }

        Task IRoomClient.UploadResourceAsync(ResourceType type, string id, byte[] bytes)
        {
            logger?.logTrace($"上传类型为{type.GetString()}，id为{id}的资源。");

            if (curNetwork != null && !isLocalRoom)
                return curNetwork.UploadResourceAsync(type, id, bytes);
            return Task.CompletedTask;
        }

        Task<bool> IRoomClient.ResourceExistsAsync(ResourceType type, string id)
        {
            logger?.logTrace($"检查类型为{type.GetString()}，id为{id}的资源是否存在。");

            if (curNetwork != null && !isLocalRoom)
                return curNetwork.ResourceExistsAsync(type, id);
            return Task.FromResult(false);
        }

        Task<bool[]> IRoomClient.ResourceBatchExistsAsync(Tuple<ResourceType, string>[] res)
        {
            logger?.logTrace($"批量检查{res.Length}个资源是否存在");

            if (curNetwork == null || isLocalRoom)
                return Task.FromResult(new bool[res.Length]);
            return curNetwork.ResourceBatchExistsAsync(res);
        }
        #endregion
        #endregion
        #region 事件
        public event Action<RoomPlayerData[]> OnRoomPlayerDataListChanged;

        public event Action<RoomData> OnRoomDataChange;

        public event Action<string, object> PostRoomPropChange;

        public event ResponseHandler OnReceiveData;

        public event Action<ChatMsg> OnRecvChat;

        public event Action<int, CardPoolSuggestion> OnSuggestCardPools;

        public event Action<CardPoolSuggestion, bool> OnCardPoolsSuggestionAnwsered;

        public event Action<LobbyRoomDataList> onRoomListChange;

        public event Action onGameStart;

        #endregion
        #region 属性字段
        const int MAX_PLAYER_COUNT = 2;
        public int[] LANPorts { get; } = { 32900, 32901 };
        public RoomPlayerData localPlayer { get; private set; } = null;
        public RoomData room { get; private set; } = null;

        /// <summary>
        /// 是否为房主
        /// </summary>
        public bool isRoomOwner => room?.ownerId == localPlayer?.id;

        public LobbyRoomDataList roomList { get; protected set; } = new LobbyRoomDataList();

        /// <summary>
        /// 网络端口
        /// </summary>
        public int port => curNetwork != null ? curNetwork.Port : -1;
        public LANNetworking LANNetwork { get; private set; }
        public LobbyClientNetworking LobbyNetwork { get; private set; }
        public CommonClientNetwokingV3 curNetwork { get; set; } = null;
        ILogger logger { get; }
        /// <summary>
        /// 是否为本地房间
        /// </summary>
        bool isLocalRoom { get; set; } = false;

        #endregion
    }
}