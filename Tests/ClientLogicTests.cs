﻿using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using NitoriNetwork.Common;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using TouhouCardEngine;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using BJSYGameCore;
using System.Linq;

namespace Tests
{
    public class ClientLogicTests
    {
        const string TestServerUrl = "http://localhost:50112";

        [UnityTest]
        public IEnumerator localRoomCreateTest()
        {
            yield return createLocalRoomAndAssert(createRoomAssert);
        }
        IEnumerator createLocalRoomAndAssert(Func<ClientLogic, IEnumerator> onAssert)
        {
            using (ClientLogic client = new ClientLogic("Room", logger: new UnityLogger("Room")))
            {
                var task = client.createLocalRoom("本地玩家");
                yield return task.wait();
                yield return onAssert(client);
            }
        }
        IEnumerator createRoomAssert(ClientLogic client)
        {
            RoomData room = client.room;
            Assert.AreEqual(1, room.playerDataList.Count);
            Assert.AreNotEqual(0, room.playerDataList[0].id);
            Assert.AreEqual(RoomPlayerType.human, room.playerDataList[0].type);
            yield break;
        }
        [UnityTest]
        public IEnumerator localRoomAddAIPlayerTest()
        {
            yield return createLocalRoomAndAssert(addAIPlayerAssert);
        }
        IEnumerator addAIPlayerAssert(ClientLogic client)
        {
            yield return client.addAIPlayer().wait();
            Assert.AreEqual(2, client.room.playerDataList.Count);
            Assert.AreNotEqual(0, client.room.playerDataList[0].id);
            Assert.AreEqual(RoomPlayerType.human, client.room.getPlayer(client.room.playerDataList[0].id).type);
            Assert.AreNotEqual(0, client.room.playerDataList[1].id);
            Assert.AreEqual(RoomPlayerType.ai, client.room.getPlayer(client.room.playerDataList[1].id).type);
            yield break;
        }
        [UnityTest]
        public IEnumerator localRoomSetPropTest()
        {
            yield return createLocalRoomAndAssert(setPropAssert);
        }
        IEnumerator setPropAssert(ClientLogic client)
        {
            RoomData room = client.room;
            room.setProp("key", "value");
            Assert.AreEqual("value", room.getProp<string>("key"));
            yield break;
        }
        [UnityTest]
        public IEnumerator localRoomSetPlayerPropTest()
        {
            yield return createLocalRoomAndAssert(setPlayerPropAssert);
        }

        IEnumerator setPlayerPropAssert(ClientLogic client)
        {
            RoomData room = client.room;
            room.setPlayerProp(room.ownerId, "key", "value");
            Assert.AreEqual("value", room.getPlayerProp<string>(room.ownerId, "key"));
            yield break;
        }

        [Test]
        public void localRoomRemovePlayerTest()
        {
            createLocalRoomAndAssert(removePlayerAssert);
        }

        IEnumerator removePlayerAssert(ClientLogic client)
        {
            RoomData room = client.room;
            RoomPlayerData player = new RoomPlayerData("AI", RoomPlayerType.ai);
            room.playerDataList.Add(player);
            room.playerDataList.Remove(player);
            Assert.Null(room.getPlayer(player.id));
            yield break;
        }

        [Test]
        public void serializeTest()
        {
            RoomData data = new RoomData(Guid.NewGuid().ToString()) { ownerId = 1 };
            data.propDict.Add("randomSeed", 42);
            data.playerDataList.Add(new RoomPlayerData(1, "玩家", RoomPlayerType.human));
            data.playerDataList[0].propDict.Add("name", "you know who");

            string typeName = data.GetType().FullName;
            BsonDocument bsonDoc = data.ToBsonDocument();

            data = BsonSerializer.Deserialize(bsonDoc, TypeHelper.getType(typeName)) as RoomData;
            Assert.AreEqual(1, data.ownerId);
            Assert.AreEqual(42, data.propDict["randomSeed"]);
            Assert.AreEqual(1, data.playerDataList[0].id);
            Assert.AreEqual("玩家", data.playerDataList[0].name);
            Assert.AreEqual(RoomPlayerType.human, data.playerDataList[0].type);
            Assert.AreEqual("you know who", data.playerDataList[0].propDict["name"]);
        }
        IEnumerator LANRoomCreate2AndAssert(Func<ClientLogic, ClientLogic, IEnumerator> onAssert)
        {
            yield return LANRoomCreateManyAndAssert(2, clients => onAssert(clients[0], clients[1]));
            //using (ClientLogic client1 = new ClientLogic(new UnityLogger("RoomLocal")))
            //{
            //    new GameObject("Client1Updater").AddComponent<Updater>().action = () => client1.update();
            //    client1.switchNetToLAN();
            //    yield return client1.createOnlineRoom().wait();
            //    using (ClientLogic client2 = new ClientLogic(new UnityLogger("RoomRemote")))
            //    {
            //        new GameObject("Client2Updater").AddComponent<Updater>().action = () => client2.update();
            //        client2.switchNetToLAN();
            //        yield return onAssert(client1, client2);
            //    }
            //}
        }
        IEnumerator LANRoomCreateManyAndAssert(int count, Func<ClientLogic[], IEnumerator> onAssert)
        {
            ClientLogic[] clients = new ClientLogic[count];
            Updater[] updaters = new Updater[count];
            ServerClient serverClient = new ServerClient(TestServerUrl);

            for (int i = 0; i < count; i++)
            {
                ClientLogic client = new ClientLogic(i == 0 ? "Local" : "Remote" + i, sClient: serverClient, logger: new UnityLogger(i == 0 ? "Local" : "Remote" + i));
                updaters[i] = new GameObject("Client" + i + "Updater").AddComponent<Updater>();
                updaters[i].action = () => client.update();
                client.SwitchMode(true);
                clients[i] = client;
            }
            for (int i = 0; i < clients.Length; i++)
            {
                clients[i].LANNetwork.broadcastPorts = clients.Select(c => c.port).ToArray();
            }
            yield return clients[0].createOnlineRoom().wait();
            yield return onAssert(clients);
            for (int i = 0; i < count; i++)
            {
                clients[i].Dispose();
                Object.Destroy(updaters[i].gameObject);
            }
        }
        [UnityTest]
        public IEnumerator LANRoomCreateTest()
        {
            yield return LANRoomCreate2AndAssert(LANRoomCreateAssert);
        }
        IEnumerator LANRoomCreateAndAssert(Func<ClientLogic, IEnumerator> onAssert)
        {
            using (ClientLogic client = new ClientLogic("RoomLocal", logger: new UnityLogger("RoomLocal")))
            {
                client.SwitchMode(true);
                yield return client.createOnlineRoom().wait();
                yield return onAssert(client);
            }
        }
        IEnumerator LANRoomCreateAssert(ClientLogic client1, ClientLogic client2)
        {
            LobbyRoomData room = null;
            client2.onRoomListChange += r => {
                if (r.Count > 0) room = r.First().Value;
            };
            //客户端创建房间，并且广播新增房间信息
            yield return client1.createOnlineRoom().wait();
            //client2应该会收到创建房间信息
            yield return TestHelper.waitUntil(() => room != null, 5);
            Assert.NotNull(room);
        }
        [UnityTest]
        public IEnumerator LANRoomAddAIPlayerTest()
        {
            yield return LANRoomCreateAndAssert(addAIPlayerAssert);
        }
        //[UnityTest]
        //public IEnumerator LANRoomGetRoomsTest()
        //{
        //    yield return LANRoomCreate2AndAssert(LANRoomGetRoomsAssert);
        //}
        //IEnumerator LANRoomGetRoomsAssert(ClientLogic client1, ClientLogic client2)
        //{
        //    RoomData room = null;
        //    client2.onUpdateRoom += r => room = r;
        //    //client1先创建房间，但是其实这个时候client2就应该收到消息，得到房间了
        //    yield return client1.createOnlineRoom();
        //    //client2广播发现房间消息，会得到client1的回应，不过房间里面应该已经有了
        //    client2.refreshRooms();
        //    yield return TestHelper.waitUntil(() => room != null, 5);
        //    Assert.NotNull(room);
        //}
        [UnityTest]
        public IEnumerator LANRoomJoinTest()
        {
            yield return LANRoomCreate2AndAssert(LANRoomJoinAssert);
        }
        IEnumerator LANRoomJoinAssert(ClientLogic client1, ClientLogic client2)
        {
            LobbyRoomData room = null;
            //等待client2获取到房间
            client2.onRoomListChange += r => {
                if (r.Count > 0) room = r.First().Value;
            }; ;
            //client1创建房间
            yield return client1.createOnlineRoom();
            yield return TestHelper.waitUntil(() => room != null, 5);
            //client2加入房间
            yield return client2.joinRoom(room.RoomID).wait();
            var roomData = client2.room;
            Assert.AreEqual(client1.room.playerDataList[0].id, client1.room.ownerId);
            Assert.AreEqual(2, client1.room.playerDataList.Count);
            Assert.AreEqual(RoomPlayerType.human, client1.room.playerDataList[0].type);
            Assert.AreEqual(RoomPlayerType.human, client1.room.playerDataList[1].type);
            Assert.AreEqual(client2.room.playerDataList[0].id, client2.room.ownerId);
            Assert.AreEqual(2, client2.room.playerDataList.Count);
            Assert.AreEqual(RoomPlayerType.human, client2.room.playerDataList[0].type);
            Assert.AreEqual(RoomPlayerType.human, client2.room.playerDataList[1].type);
            Assert.AreEqual(client1.room.ownerId, client2.room.ownerId);
            Assert.AreEqual(client1.room.playerDataList[0].id, client2.room.playerDataList[0].id);
            Assert.AreEqual(client1.room.playerDataList[1].id, client2.room.playerDataList[1].id);
        }
        [UnityTest]
        public IEnumerator LANRoomQuitTest()
        {
            yield return LANRoomCreate2AndAssert(LANRoomQuitAssert);
        }
        IEnumerator LANRoomQuitAssert(ClientLogic client1, ClientLogic client2)
        {
            LobbyRoomData room = null;
            //等待client2获取到房间
            client2.onRoomListChange += r => {
                if (r.Count > 0) room = r.First().Value;
            };
            //client1创建房间
            yield return client1.createOnlineRoom();
            yield return TestHelper.waitUntil(() => room != null, 5);
            //client2加入房间
            yield return client2.joinRoom(room.RoomID).wait();
            Assert.AreEqual(2, client1.room.playerDataList.Count);
            Assert.AreEqual(2, client2.room.playerDataList.Count);
            //client2退出房间
            yield return client2.quitRoom().wait();
            Assert.Null(client2.room);
            yield return new WaitForSeconds(0.5f); // 要等一下才会退出
            Assert.AreEqual(1, client1.room.playerDataList.Count);
        }
        [UnityTest]
        public IEnumerator LANRoomSetPropTest()
        {
            yield return LANRoomCreate2AndAssert(LANRoomSetPropAssert);
        }
        IEnumerator LANRoomSetPropAssert(ClientLogic client1, ClientLogic client2)
        {
            LobbyRoomData room = null;
            //等待client2获取到房间
            client2.onRoomListChange += r => {
                if (r.Count > 0) room = r.First().Value;
            };
            //client1创建房间
            yield return client1.createOnlineRoom();
            yield return TestHelper.waitUntil(() => room != null, 5);
            //client2加入房间
            yield return client2.joinRoom(room.RoomID).wait();
            Assert.AreEqual(2, client1.room.playerDataList.Count);
            Assert.AreEqual(2, client2.room.playerDataList.Count);
            //client1修改房间属性
            yield return (client1 as IRoomClient).SetRoomProp("randomSeed", 42);
            //client1和client2都有这个属性
            Assert.AreEqual(42, client1.room.propDict["randomSeed"]);
            Assert.AreEqual(42, client2.room.propDict["randomSeed"]);
        }
        [UnityTest]
        public IEnumerator LANRoomSetPlayerPropTest()
        {
            yield return LANRoomCreate2AndAssert(LANRoomSetPlayerPropAssert);
        }
        IEnumerator LANRoomSetPlayerPropAssert(ClientLogic client1, ClientLogic client2)
        {
            LobbyRoomData room = null;
            //等待client2获取到房间
            client2.onRoomListChange += r => {
                if (r.Count > 0) room = r.First().Value;
            };
            //client1创建房间
            yield return client1.createOnlineRoom();
            yield return TestHelper.waitUntil(() => room != null, 5);
            //client2加入房间
            yield return client2.joinRoom(room.RoomID).wait();
            Assert.AreEqual(2, client1.room.playerDataList.Count);
            Assert.AreEqual(2, client2.room.playerDataList.Count);
            //client2修改自己的属性
            yield return (client2 as IRoomClient).SetPlayerProp("deckCount", 1);
            //client1和client2都能看到这个属性更改
            Assert.AreEqual(1, client1.room.playerDataList[1].propDict["deckCount"]);
            Assert.AreEqual(1, client2.room.playerDataList[1].propDict["deckCount"]);
        }
    }
    class Updater : MonoBehaviour, IDisposable
    {
        public Action action;
        protected void Update()
        {
            action?.Invoke();
        }
        public void Dispose()
        {
            DestroyImmediate(gameObject);
        }
    }
}