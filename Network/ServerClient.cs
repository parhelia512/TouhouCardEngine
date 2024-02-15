﻿using System.Net;
using RestSharp;
using RestSharp.Serialization;

using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using TouhouCardEngine;
using TouhouCardEngine.Shared;
using MongoDB.Bson.Serialization.Attributes;

namespace NitoriNetwork.Common
{
    /// <summary>
    /// 服务器客户端
    /// 提供了与服务器交互的基本API
    /// </summary>
    public class ServerClient
    {
        const string uaVersionKey = "zmcs";

        /// <summary>
        /// 用户Session
        /// </summary>
        public string UserSession { get; internal set; } = "";

        /// <summary>
        /// 当前用户的UID
        /// </summary>
        public int UID { get; internal set; } = 0;

        ILogger logger { get; }

        #region 基础行为
        RestClient client { get; }

        string cookieFilePath { get; }

        public string baseUri { get; private set; }

        Dictionary<int, PublicBasicUserInfo> userInfoCache = new Dictionary<int, PublicBasicUserInfo>();

        /// <summary>
        /// 指定一个服务器初始化Client
        /// </summary>
        /// <param name="baseUri"></param>
        public ServerClient(string baseUri, string gameVersion = "1.0", string cookieFile = "", ILogger logger = null)
        {
            this.baseUri = baseUri;
            this.logger = logger;

            client = new RestClient(baseUri);
            client.UserAgent = uaVersionKey + "/" + gameVersion + " " + additionalUserAgent();
            cookieFilePath = cookieFile;
            client.AddDefaultHeader("Accept-Language", CultureInfo.CurrentCulture.Name);

            if (string.IsNullOrEmpty(cookieFile))
            {
                client.CookieContainer = new CookieContainer();
            }
            else
            {
                client.CookieContainer = CookieContainerExtension.ReadFrom(cookieFile);
                loadCookie();
            }

            client.ThrowOnDeserializationError = true;
            client.UseSerializer(
                () => new MongoDBJsonSerializer()
            );
        }

        public void SetClientBaseUri(string baseUri)
        {
            this.baseUri = baseUri;
            var uri = new Uri(baseUri);
            client.BaseUrl = uri;

            // 重设信息
            loadCookie();
            UID = 0;
            UserSession = "";
        }

        /// <summary>
        /// 附加的UserAgent，用于统计
        /// </summary>
        /// <returns></returns>
        string additionalUserAgent()
        {
            StringBuilder sb = new StringBuilder();
            var os = Environment.OSVersion;
            sb.Append("OS/");
            sb.Append(os.VersionString.Replace(" ", "_"));
            sb.Append(" Lang/");
            var culture = CultureInfo.CurrentCulture;
            sb.Append(culture.Name);
            return sb.ToString();
        }
        #endregion

        #region Cookie
        /// <summary>
        /// 保存小饼干（？）
        /// </summary>
        void saveCookie()
        {
#if UNITY_EDITOR
            var cookies = client.CookieContainer.GetCookies(new Uri(baseUri));
            string cookieStr = "";
            foreach (Cookie item in cookies)
            {
                cookieStr += item.ToString() + "; ";
            }
            logger?.logTrace($"保存Cookie: {cookieStr}");
#endif
            if (!string.IsNullOrEmpty(cookieFilePath))
            {
                try
                {
                    client.CookieContainer.WriteTo(cookieFilePath);
                }
                catch (Exception e)
                {
                    logger?.logError($"Cookie 保存出现问题: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 从Cookie里面加载部分需要的数据
        /// </summary>
        void loadCookie()
        {
            var cookies = client.CookieContainer.GetCookies(new Uri(baseUri));
            foreach (Cookie cookie in cookies)
            {
                if (cookie.Name == "Session")
                {
                    UserSession = cookie.Value;
                }
            }
        }

        /// <summary>
        /// 清空已有的用户鉴权用Cookie
        /// </summary>
        void clearUserCookie()
        {
            var cookies = client.CookieContainer.GetCookies(new Uri(baseUri));
            foreach (Cookie cookie in cookies)
            {
                if (cookie.Name == "Session" || cookie.Name == "Token")
                    cookie.Expired = true;
            }
        }
        #endregion

        #region 错误处理
        /// <summary>
        /// 资源错误处理
        /// </summary>
        /// <remarks>
        /// 当网络异常时，报对应异常错误；当HTTP代码不为200时报错。
        /// </remarks>
        /// <param name="response"></param>
        void errorHandler(IRestResponse response, IRestRequest request)
        {
            if (response.ErrorException != null)
            {
                throw new NetClientException(response.ErrorException, request.Resource);
            }
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new NetClientException(response.StatusCode, request?.Resource);
            }
        }
        /// <summary>
        /// API 错误处理。
        /// </summary>
        /// <remarks>
        /// 当网络异常时，报对应异常错误；
        /// 当HTTP代码不为200时，若 Message 不空，则报对应 Message，否则报HTTP错误。
        /// </remarks>
        /// <param name="response"></param>
        /// <param name="data"></param>
        void errorHandler(IRestResponse response, IExecuteResult data, IRestRequest request)
        {
            if (response.ErrorException != null)
            {
                throw new NetClientException(response.ErrorException, request.Resource);
            }
            if (response.StatusCode != HttpStatusCode.OK)
            {
                if (!string.IsNullOrEmpty(data.message))
                {
                    throw new NetClientException(data.message, request.Resource);
                }
                else
                {
                    throw new NetClientException(response.StatusCode, request.Resource);
                }
            }
        }
        /// <summary>
        /// 登录错误处理。
        /// </summary>
        /// <remarks>
        /// 当网络异常时，报对应异常错误；
        /// 当HTTP代码不为200时，如果是 400 Bad Request，并且登录结果是Fail，返回false，否则抛出异常。
        /// 如果不是400就抛出状态描述异常。
        /// 最后如果登录没成功，就返回false。
        /// </remarks>
        /// <param name="response"></param>
        /// <param name="data"></param>
        bool errorHandlerLogin(IRestResponse response, IExecuteResult data, IRestRequest request)
        {
            if (response.ErrorException != null)
            {
                throw new NetClientException(response.ErrorException, request.Resource);
            }
            if (response.StatusCode != HttpStatusCode.OK)
            {
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    // 登录失败
                    if (data.code == ResultCode.Fail)
                        return false;

                    throw new NetClientException(data.message);
                }
                else
                {
                    throw new NetClientException(response.StatusDescription);
                }
            }
            if (data.code != ResultCode.Success)
            {
                return false;
            }
            return true;
        }
        #endregion

        #region Kratos
        /// <summary>
        /// Kratos 认证客户端
        /// </summary>
        public KratosClient Kratos { get; set; }

        /// <summary>
        /// 云下发的服务器配置
        /// </summary>
        [Serializable]
        class KratosServerConfig
        {
            public string url { get; set; }
        }

        /// <summary>
        /// 新增一个认证客户端。后续账户认证相关都需要经过这个客户端。
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NetClientException"></exception>
        public async Task<bool> NewKratosClient()
        {
            RestRequest request = new RestRequest("/api/User/auth/kratos", Method.GET);

            var response = await client.ExecuteAsync<KratosServerConfig>(request);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new NetClientException(response.StatusDescription);
            }

            Kratos = new KratosClient(response.Data.url, client.UserAgent, logger);
            return true;
        }

        /// <summary>
        /// 使用邮箱注册
        /// </summary>
        /// <param name="mail"></param>
        /// <param name="password"></param>
        /// <param name="nickname"></param>
        /// <returns></returns>
        public async Task<string> RegisterViaPassword(string mail, string password, string nickname)
        {
            string flow = await Kratos.CreateRegistrationFlow();
            return await Kratos.UpdateRegistrationFlow(flow, new KratosClient.PasswordRegistrationRequest(mail, password, nickname));
        }

        /// <summary>
        /// 使用Steam注册
        /// </summary>
        /// <param name="client">客户端ID，根据客户端版本不同设置不同的值</param>
        /// <param name="ticket"></param>
        /// <param name="nickname"></param>
        /// <returns></returns>
        public async Task<string> RegisterViaSteam(string client, string ticket, string nickname)
        {
            string flow = await Kratos.CreateRegistrationFlow();
            return await Kratos.UpdateRegistrationFlow(flow, new KratosClient.SteamRegistrationRequest(client, ticket, nickname));
        }

        /// <summary>
        /// 使用密码登录
        /// </summary>
        /// <param name="mail"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public async Task<string> LoginViaPassword(string mail, string password)
        {
            string flow = await Kratos.CreateLoginFlow();
            return await Kratos.UpdateLoginFlow(flow, new KratosClient.PasswordLoginRequest(mail, password));
        }

        /// <summary>
        /// 使用Steam登录
        /// </summary>
        /// <param name="client"></param>
        /// <param name="ticket"></param>
        /// <returns></returns>
        public async Task<string> LoginViaSteam(string client, string ticket)
        {
            string flow = await Kratos.CreateLoginFlow();
            return await Kratos.UpdateLoginFlow(flow, new KratosClient.SteamLoginRequest(client, ticket));
        }

        /// <summary>
        /// 登录
        /// </summary>
        /// <remarks>
        /// 在登录前必须调用LoginViaXXX获取Kratos的登录凭证。可以通过Kratos.WhoAmI方法验证是否已经有对应登录凭证
        /// </remarks>
        /// <param name="sessionToken">LoginViaXXX得到的登录凭证</param>
        /// <returns></returns>
        public async Task<bool> LoginByKratosAsync(string sessionToken)
        {
            // 防止重复登录
            if (UID != 0)
                clearUserCookie();

            RestRequest request = new RestRequest("/api/User/session", Method.POST);
            request.AddParameter("type", "kratos");
            request.AddParameter("session", sessionToken);

            var response = await client.ExecuteAsync<ExecuteResult<string>>(request);
            if (!errorHandlerLogin(response, response.Data, request))
            {
                return false;
            }

            saveCookie();
            // 登录换取的是Token，我们需要Session
            await GetSessionAsync();

            return true;
        }
        /// <summary>
        /// 登出
        /// </summary>
        /// <param name="mail"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public async Task LogoutKratos()
        {
            try
            {
                await Kratos.Logout();
            }
            catch (Exception e)
            {
                logger?.logError($"Kratos登出失败：{e}");
            }
            RestRequest request = new RestRequest("/api/User/session", Method.DELETE);

            var response = await client.ExecuteAsync<ExecuteResult<string>>(request);
            errorHandler(response, response.Data, request);

            UID = 0;
            UserSession = "";
            saveCookie();
        }


        /// <summary>
        /// 当前登录信息绑定Steam账户
        /// </summary>
        /// <param name="client"></param>
        /// <param name="ticket"></param>
        /// <returns></returns>
        public async Task<bool> LinkSteam(string client, string ticket)
        {
            string flow = await Kratos.CreateSettingFlow();
            return await Kratos.UpdateSettingFlow(flow, KratosClient.SteamSettingRequest.Link(client, ticket));
        }

        /// <summary>
        /// 当前账户解绑Steam登录信息
        /// </summary>
        /// <returns></returns>
        public async Task<bool> UnlinkSteam()
        {
            string flow = await Kratos.CreateSettingFlow();
            return await Kratos.UpdateSettingFlow(flow, KratosClient.SteamSettingRequest.Unlink());
        }

        /// <summary>
        /// 修改当前登录账户的密码
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public async Task<bool> ChangePassword(string password)
        {
            string flow = await Kratos.CreateSettingFlow();
            return await Kratos.UpdateSettingFlow(flow, new KratosClient.PasswordSettingRequest(password));
        }

        /// <summary>
        /// 请求找回账户，账户找回请求会发送到对应邮箱
        /// </summary>
        /// <param name="mail"></param>
        /// <returns></returns>
        public async Task<bool> RecoveryRequest(string mail)
        {
            string flow = await Kratos.CreateRecoveryFlow();
            return await Kratos.UpdateRecoveryFlow(flow, new KratosClient.EmailRecoveryRequest(mail));
        }

        #endregion

        #region 登录
        /// <summary>
        /// 获取Session
        /// </summary>
        /// <returns></returns>
        public string GetSession()
        {
            RestRequest request = new RestRequest("/api/User/session", Method.GET);

            var response = client.Execute<ExecuteResult<string>>(request);
            errorHandler(response, response.Data, request);

            // 更新暂存的Session
            // 虽然Cookie里面也能获取到，但是获取比较麻烦
            UserSession = response.Data.result;
            saveCookie();

            logger?.logTrace($"注册用户登录. Session: {UserSession}");

            // 更新当前登录用户信息
            GetUserInfo();

            return UserSession;
        }

        /// <summary>
        /// 获取Session
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetSessionAsync()
        {
            RestRequest request = new RestRequest("/api/User/session", Method.GET);

            var response = await client.ExecuteAsync<ExecuteResult<string>>(request);
            errorHandler(response, response.Data, request);

            // 更新暂存的Session
            // 虽然Cookie里面也能获取到，但是获取比较麻烦
            UserSession = response.Data.result;
            saveCookie();

            logger?.logTrace($"注册用户登录. Session: {UserSession}");

            // 更新当前登录用户信息
            await GetUserInfoAsync();

            return UserSession;
        }

        /// <summary>
        /// 游客登录
        /// </summary>
        /// <return>是否登录成功，失败则意味着服务器压力过大</return>
        public bool GuestLogin()
        {
            RestRequest request = new RestRequest("/api/User/guest", Method.POST);

            var response = client.Execute<ExecuteResult<string>>(request);
            if (response.Data?.code == ResultCode.Fail)
                return false;

            errorHandler(response, response.Data, request);

            UserSession = response.Data.result;
            saveCookie();

            logger?.logTrace($"游客登录. Session: {UserSession}");

            GetUserInfo();

            return true;
        }

        /// <summary>
        /// 游客登录
        /// </summary>
        public async Task<bool> GuestLoginAsync()
        {
            RestRequest request = new RestRequest("/api/User/guest", Method.POST);

            var response = await client.ExecuteAsync<ExecuteResult<string>>(request);
            if (response.Data?.code == ResultCode.Fail)
                return false;

            errorHandler(response, response.Data, request);

            UserSession = response.Data.result;
            saveCookie();

            logger?.logTrace($"游客登录. Session: {UserSession}");

            await GetUserInfoAsync();

            return true;
        }
        #endregion

        #region 验证码
        /// <summary>
        /// 获取验证码图像
        /// </summary>
        /// <returns></returns>
        public byte[] GetCaptchaImage()
        {
            RestRequest request = new RestRequest("/api/Captcha/image", Method.GET);

            var response = client.Execute(request);
            errorHandler(response, request);

            return response.RawBytes;
        }

        /// <summary>
        /// 获取验证码的图像
        /// </summary>
        /// <returns></returns>
        public async Task<byte[]> GetCaptchaImageAsync()
        {
            RestRequest request = new RestRequest("/api/Captcha/image", Method.GET);

            var response = await client.ExecuteAsync(request);
            errorHandler(response, request);

            return response.RawBytes;
        }
        #endregion

        #region 房间
        /// <summary>
        /// 创建一个房间
        /// </summary>
        /// <returns></returns>
        public LobbyRoomData CreateRoom(string name = "", string password = "")
        {
            RestRequest request = new RestRequest("/api/Room", Method.POST);
            request.AddParameter("name", name);
            request.AddParameter("password", password);
            var response = client.Execute<ExecuteResult<LobbyRoomData>>(request);

            errorHandler(response, response.Data, request);
            return response.Data.result;
        }

        /// <summary>
        /// 创建一个房间
        /// </summary>
        /// <returns></returns>
        public async Task<LobbyRoomData> CreateRoomAsync(string name = "", string password = "")
        {
            RestRequest request = new RestRequest("/api/Room", Method.POST);
            request.AddParameter("name", name);
            request.AddParameter("password", password);
            var response = await client.ExecuteAsync<ExecuteResult<LobbyRoomData>>(request);

            errorHandler(response, response.Data, request);
            return response.Data.result;
        }

        /// <summary>
        /// 获取房间信息
        /// </summary>
        /// <returns></returns>
        public LobbyRoomData[] GetRoomInfos()
        {
            RestRequest request = new RestRequest("/api/Room", Method.GET);
            var response = client.Execute<ExecuteResult<LobbyRoomData[]>>(request);

            errorHandler(response, response.Data, request);
            return response.Data.result;
        }

        /// <summary>
        /// 获取房间信息
        /// </summary>
        /// <returns></returns>
        public async Task<LobbyRoomData[]> GetRoomInfosAsync()
        {
            RestRequest request = new RestRequest("/api/Room", Method.GET);
            var response = await client.ExecuteAsync<ExecuteResult<LobbyRoomData[]>>(request);

            errorHandler(response, response.Data, request);
            return response.Data.result;
        }
        #endregion

        #region 用户信息
        /// <summary>
        /// 获取当前登录用户的信息
        /// </summary>
        /// <returns></returns>
        public PublicBasicUserInfo GetUserInfo()
        {
            RestRequest request = new RestRequest("/api/User/me", Method.GET);
            var response = client.Execute<ExecuteResult<PublicBasicUserInfo>>(request);

            errorHandler(response, response.Data, request);

            UID = response.Data.result.UID;
            userInfoCache[response.Data.result.UID] = response.Data.result;
            return response.Data.result;
        }

        /// <summary>
        /// 获取缓存的当前用户登录的信息
        /// </summary>
        /// <returns></returns>
        public PublicBasicUserInfo GetUserInfoCached()
        {
            return userInfoCache[UID];
        }

        /// <summary>
        /// 获取指定ID用户的用户信息
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        public PublicBasicUserInfo GetUserInfo(int uid, bool refresh = false)
        {
            if (!refresh && userInfoCache.ContainsKey(uid))
            {
                return userInfoCache[uid];
            }

            RestRequest request = new RestRequest("/api/User/" + uid, Method.GET);
            var response = client.Execute<ExecuteResult<PublicBasicUserInfo>>(request);

            errorHandler(response, response.Data, request);

            userInfoCache[uid] = response.Data.result;
            return response.Data.result;
        }

        /// <summary>
        /// 获取当前登录用户的信息
        /// </summary>
        /// <returns></returns>
        public async Task<PublicBasicUserInfo> GetUserInfoAsync()
        {
            RestRequest request = new RestRequest("/api/User/me", Method.GET);
            var response = await client.ExecuteAsync<ExecuteResult<PublicBasicUserInfo>>(request);

            errorHandler(response, response.Data, request);

            UID = response.Data.result.UID;
            userInfoCache[response.Data.result.UID] = response.Data.result;
            return response.Data.result;
        }

        /// <summary>
        /// 获取指定用户的信息
        /// </summary>
        /// <returns></returns>
        public async Task<PublicBasicUserInfo> GetUserInfoAsync(int uid, bool refresh)
        {
            if (!refresh && userInfoCache.ContainsKey(uid))
            {
                return userInfoCache[uid];
            }

            RestRequest request = new RestRequest("/api/User/" + uid, Method.GET);
            var response = await client.ExecuteAsync<ExecuteResult<PublicBasicUserInfo>>(request);

            errorHandler(response, response.Data, request);

            userInfoCache[uid] = response.Data.result;
            return response.Data.result;
        }

        /// <summary>
        /// 更新用户信息
        /// </summary>
        /// <param name="nickname">要更新的昵称</param>
        public async Task ChangeUserInfoAsync(string nickname = "")
        {
            RestRequest request = new RestRequest("/api/User", Method.PATCH);
            request.AddParameter("nickname", nickname);

            var response = await client.ExecuteAsync<ExecuteResult<string>>(request);

            errorHandler(response, response.Data, request);
        }
        #endregion

        #region 版本更新
        /// <summary>
        /// 获取最新的版本更新信息
        /// </summary>
        /// <returns></returns>
        public ClientUpdateInfo GetLatestUpdate()
        {
            RestRequest request = new RestRequest("/api/Update/latest", Method.GET);
            var response = client.Execute<ExecuteResult<ClientUpdateInfo>>(request);

            errorHandler(response, response.Data, request);
            return response.Data.result;
        }

        /// <summary>
        /// 获取最新的版本更新信息
        /// </summary>
        /// <returns></returns>
        public async Task<ClientUpdateInfo> GetLatestUpdateAsync()
        {
            RestRequest request = new RestRequest("/api/Update/latest", Method.GET);
            var response = await client.ExecuteAsync<ExecuteResult<ClientUpdateInfo>>(request);

            if (response.ErrorException != null)
                throw response.ErrorException;

            errorHandler(response, response.Data, request);
            return response.Data.result;
        }

        /// <summary>
        /// 获取指定版本的更新信息
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public ClientUpdateInfo GetUpdateByVersion(string version)
        {
            RestRequest request = new RestRequest("/api/Update/" + version, Method.GET);
            var response = client.Execute<ExecuteResult<ClientUpdateInfo>>(request);

            errorHandler(response, response.Data, request);
            return response.Data.result;
        }

        /// <summary>
        /// 获取指定版本的更新信息
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public async Task<ClientUpdateInfo> GetUpdateByVersionAsync(string version)
        {
            RestRequest request = new RestRequest("/api/Update/" + version, Method.GET);
            var response = await client.ExecuteAsync<ExecuteResult<ClientUpdateInfo>>(request);

            errorHandler(response, response.Data, request);

            return response.Data.result;
        }

        /// <summary>
        /// 获取指定版本之后到最新版本的所有更新信息
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public ClientUpdateInfo[] GetUpdateDeltaByVersion(string version)
        {
            RestRequest request = new RestRequest("/api/Update/" + version + "/delta", Method.GET);
            var response = client.Execute<ExecuteResult<ClientUpdateInfo[]>>(request);

            errorHandler(response, response.Data, request);
            return response.Data.result;
        }

        /// <summary>
        /// 获取指定版本之后到最新版本的所有更新信息
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public async Task<ClientUpdateInfo[]> GetUpdateDeltaByVersionAsync(string version)
        {
            RestRequest request = new RestRequest("/api/Update/" + version + "/delta", Method.GET);
            var response = await client.ExecuteAsync<ExecuteResult<ClientUpdateInfo[]>>(request);

            errorHandler(response, response.Data, request);
            return response.Data.result;
        }

        #endregion

        #region EULA
        /// <summary>
        /// 获取用户许可协议的HTML
        /// </summary>
        /// <returns></returns>
        public string GetEULA()
        {
            RestRequest request = new RestRequest("/api/EULA/EULA", Method.GET);
            var response = client.Execute(request);
            errorHandler(response, request);

            return response.Content;
        }
        /// <summary>
        /// 获取用户许可协议的HTML
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetEULAAsync()
        {
            RestRequest request = new RestRequest("/api/EULA/EULA", Method.GET);
            var response = await client.ExecuteAsync(request);
            errorHandler(response, request);

            return response.Content;
        }

        /// <summary>
        /// 获取用户许可协议的HTML
        /// </summary>
        /// <returns></returns>
        public string GetPrivacyPolicy()
        {
            RestRequest request = new RestRequest("/api/EULA/Privacy", Method.GET);
            var response = client.Execute(request);
            errorHandler(response, request);

            return response.Content;
        }
        /// <summary>
        /// 获取用户许可协议的HTML
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetPrivacyPolicyAsync()
        {
            RestRequest request = new RestRequest("/api/EULA/Privacy", Method.GET);
            var response = await client.ExecuteAsync(request);
            errorHandler(response, request);

            return response.Content;
        }
        #endregion

        #region 玩家卡组
        /// <summary>
        /// 设置用户卡组
        /// </summary>
        /// <param name="decks"></param>
        public void SetUserDecks(DeckDataItem[] decks)
        {
            RestRequest request = new RestRequest("/api/Deck", Method.POST);
            request.AddParameter("decks", decks.ToJson());

            var response = client.Execute<ExecuteResult>(request);

            errorHandler(response, response.Data, request);
        }

        /// <summary>
        /// 设置用户卡组
        /// </summary>
        /// <param name="decks"></param>
        public async Task SetUserDecksAsync(DeckDataItem[] decks)
        {
            RestRequest request = new RestRequest("/api/Deck", Method.POST);
            request.AddParameter("decks", decks.ToJson());

            var response = await client.ExecuteAsync<ExecuteResult>(request);

            errorHandler(response, response.Data, request);
        }

        /// <summary>
        /// 获取用户卡组
        /// </summary>
        /// <returns></returns>
        public DeckDataItem[] GetUserDecks()
        {
            RestRequest request = new RestRequest("/api/Deck", Method.GET);
            var response = client.Execute<ExecuteResult<DeckDataItem[]>>(request);

            errorHandler(response, response.Data, request);

            return response.Data.result;
        }

        /// <summary>
        /// 获取用户卡组
        /// </summary>
        /// <returns></returns>
        public async Task<DeckDataItem[]> GetUserDecksAsync()
        {
            RestRequest request = new RestRequest("/api/Deck", Method.GET);
            var response = await client.ExecuteAsync<ExecuteResult<DeckDataItem[]>>(request);

            errorHandler(response, response.Data, request);

            return response.Data.result;
        }

        #endregion
    }

    [System.Serializable]
    public class NetClientException : System.Exception
    {
        public string Url { get; }
        public NetClientException() { }
        public NetClientException(string message, string url = "") : base(message)
        {
            Url = url;
        }
        public NetClientException(HttpStatusCode code, string url = "") : base($"HTTP {(int)code}: {code}")
        {
            Url = url;
        }
        public NetClientException(Exception inner, string url = "") : base(inner.Message)
        {
            Url = url;
        }
        protected NetClientException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    public class MongoDBJsonSerializer : IRestSerializer
    {
        public string Serialize(object obj) => obj.ToJson();

        public string Serialize(Parameter bodyParameter) => Serialize(bodyParameter.Value);

        public T Deserialize<T>(IRestResponse response)
        {
            // UnityEngine.Debug.Log(response.Content);
            return BsonSerializer.Deserialize<T>(response.Content);
        }

        public string[] SupportedContentTypes { get; } =
        {
            "application/json", "text/json", "text/x-json", "text/javascript", "*+json", "text/plain"
        };

        public string ContentType { get; set; } = "application/json";

        public DataFormat DataFormat { get; } = DataFormat.Json;
    }

    static class CookieContainerExtension
    {
        public static void WriteTo(this CookieContainer container, string file)
        {
            using (Stream stream = File.Create(file))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(stream, container);
            }
        }

        public static CookieContainer ReadFrom(string file)
        {
            try
            {
                using (Stream stream = File.Open(file, FileMode.Open))
                {
                    BinaryFormatter formatter = new BinaryFormatter();
                    return (CookieContainer)formatter.Deserialize(stream);
                }
            }
            catch (Exception)
            {
                return new CookieContainer();
            }
        }
    }
}
