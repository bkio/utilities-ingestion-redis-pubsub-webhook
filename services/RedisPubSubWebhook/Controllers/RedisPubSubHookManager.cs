/// Copyright 2022- Burak Kara, All rights reserved.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Authentication;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CommonUtilities;
using CloudServiceUtilities;

namespace RedisPubSubWebhook
{
    class RedisPubSubHookManager
    {
        public static RedisPubSubHookManager Get()
        {
            if (Instance == null)
            {
                Instance = new RedisPubSubHookManager();
            }
            return Instance;
        }
        private RedisPubSubHookManager() { }
        private static RedisPubSubHookManager Instance = null;

        private IMemoryServiceInterface MemoryService;
        private IPubSubServiceInterface PubSubService;

        private string RedisKeyTopicMapParameters_ScopeKey;
        private string RedisKeyTopicMapParameters_FieldKey;
        private string RedisKeyAckQueryParameters_Domain;
        private string RedisKeyAckQueryParameters_Subdomain;

        public bool StartOperation(
            IMemoryServiceInterface _MemoryService, 
            IPubSubServiceInterface _PubSubService, 
            string _TopicEndpointMapFetchMethod,
            string _RedisKeyTopicMap, 
            string _TopicEndpointMapJsonAsString,
            string _RedisKeyPrefixAck, 
            Action<string> _ErrorMessageAction = null)
        {
            if (bOperationStarted)
            {
                _ErrorMessageAction?.Invoke("RedisPubSubHookManager->StartOperation: Operation has already been started.");
                return false;
            }

            MemoryService = _MemoryService;
            PubSubService = _PubSubService;

            if (_TopicEndpointMapFetchMethod == "REDIS")
            {
                var Splitted1 = _RedisKeyTopicMap.Split(':');
                if (Splitted1 == null || Splitted1.Length != 4)
                {
                    _ErrorMessageAction?.Invoke("RedisPubSubHookManager->StartOperation: REDIS_KEY_TOPICS_TO_BE_LISTENED_MAP must contains FOUR elements delimited by :, for example: PUBSUB:WEBHOOK:CONFIG:TOPICS");
                    return false;
                }
                RedisKeyTopicMapParameters_ScopeKey = $"{Splitted1[0]}_{Splitted1[1]}_{Splitted1[2]}";
                RedisKeyTopicMapParameters_FieldKey = Splitted1[3];

                if (!FetchTopicsToBeListened(_ErrorMessageAction))
                {
                    return false;
                }
            }
            else //ENV_VAR
            {
                if (!ParseTopicsToBeListened(_TopicEndpointMapJsonAsString, _ErrorMessageAction))
                {
                    return false;
                }
            }
            
            var Splitted2 = _RedisKeyPrefixAck.Split(':');
            if (Splitted2 == null || Splitted2.Length != 2)
            {
                _ErrorMessageAction?.Invoke("RedisPubSubHookManager->StartOperation: REDIS_KEY_PREFIX_ACK must contains TWO elements delimited by :, for example: PUBSUB:WEBHOOK_ACK");
                return false;
            }
            RedisKeyAckQueryParameters_Domain = Splitted2[0];
            RedisKeyAckQueryParameters_Subdomain = Splitted2[1];

            if (!StartListeningTopics(_ErrorMessageAction))
            {
                return false;
            }

            bOperationStarted = true;
            return true;
        }
        private bool bOperationStarted = false;

        private bool FetchTopicsToBeListened(Action<string> _ErrorMessageAction = null)
        {
            var Result = MemoryService.GetKeyValue(RedisKeyTopicMapParameters_ScopeKey, RedisKeyTopicMapParameters_FieldKey, _ErrorMessageAction);
            if (Result == null)
            {
                _ErrorMessageAction?.Invoke("RedisPubSubHookManager->FetchTopicsToBeListened: GetKeyValue returned null.");
                return false;
            }
            return ParseTopicsToBeListened(Result.AsString, _ErrorMessageAction);
        }
        private bool ParseTopicsToBeListened(string _JsonString, Action<string> _ErrorMessageAction = null)
        {
            try
            {
                var TopicsParsed = new Dictionary<string, List<PubSubTopicToListenRequestModel>>();

                var ParsedJObject = JObject.Parse(_JsonString);

                foreach (var Pair in ParsedJObject)
                {
                    var EndpointList = new List<PubSubTopicToListenRequestModel>();

                    if (Pair.Value.Type != JTokenType.String)
                    {
                        _ErrorMessageAction?.Invoke("RedisPubSubHookManager->ParseTopicsToBeListened: Unexpected json structure-1: " + _JsonString);
                        return false;
                    }

                    var ValueAsString = (string)Pair.Value;
                    var EndpointListAsJArray = JArray.Parse(ValueAsString);
                    foreach (JObject EndpointJObject in EndpointListAsJArray)
                    {
                        var Deserialized = JsonConvert.DeserializeObject<PubSubTopicToListenRequestModel>(EndpointJObject.ToString());
                        if (Deserialized == null || !Deserialized.Validate())
                        {
                            _ErrorMessageAction?.Invoke("RedisPubSubHookManager->ParseTopicsToBeListened: Unexpected json structure-2: " + _JsonString);
                            return false;
                        }
                        EndpointList.Add(Deserialized);
                    }

                    if (EndpointList.Count == 0)
                    {
                        _ErrorMessageAction?.Invoke("RedisPubSubHookManager->ParseTopicsToBeListened: Unexpected json structure-3: " + _JsonString);
                        return false;
                    }

                    TopicsParsed.Add(Pair.Key, EndpointList);
                }

                if (TopicsParsed.Count == 0)
                {
                    _ErrorMessageAction?.Invoke("RedisPubSubHookManager->ParseTopicsToBeListened: There is no topic to be listened defined in memory store: " + _JsonString);
                    return false;
                }

                PubSubTopicsToListen = TopicsParsed;
            }
            catch (Exception e)
            {
                _ErrorMessageAction?.Invoke("RedisPubSubHookManager->ParseTopicsToBeListened: Json parse error occured. Error: " + e.Message + ", String: " + _JsonString);
                return false;
            }
            return true;
        }
        private Dictionary<string, List<PubSubTopicToListenRequestModel>> PubSubTopicsToListen = null;

        private bool StartListeningTopics(Action<string> _ErrorMessageAction = null)
        {
            var WaitUntil = new ManualResetEvent(false);
            var ErrorState = new Atomicable<bool>(false, EProducerStatus.MultipleProducer);

            TaskWrapper.Run(() =>
            {
                foreach (var Topic in PubSubTopicsToListen)
                {
                    if (!SubscribeToTopic(Topic.Key, Topic.Value, _ErrorMessageAction))
                    {
                        ErrorState.Set(true);
                        break;
                    }
                }
                WaitUntil.Set();
            });

            try
            {
                WaitUntil.WaitOne();
            }
            catch (Exception e)
            {
                _ErrorMessageAction?.Invoke("RedisPubSubHookManager->StartListeningTopics: Error: " + e.Message + ", trace: " + e.StackTrace);
                return false;
            }
            return !ErrorState.Get();
        }

        private bool SubscribeToTopic(string _TopicName, List<PubSubTopicToListenRequestModel> _EndpointCalls, Action<string> _ErrorMessageAction = null)
        {
            return PubSubService.CustomSubscribe(_TopicName, (string _Topic, string _Message) =>
            {
                TryObtainingRightForTheMessage(out bool _bAlreadyExecuted, _Topic, _Message, _ErrorMessageAction);

                if (_bAlreadyExecuted)
                    return; //Success

                foreach (var CC in _EndpointCalls)
                {
                    var CurrentEndpointCall = CC;

                    TaskWrapper.Run(() =>
                    {
                        int RetryCount = -1;

                        while (++RetryCount < 10)
                        {
                            if (TryPerformingHttpRequest(CurrentEndpointCall, _Message, _ErrorMessageAction))
                                return; //Success

                            Thread.Sleep(1000); //Retry

                            _ErrorMessageAction?.Invoke("RedisPubSubHookManager->SubscribeToTopic: Retrying http request perform to: " + CurrentEndpointCall.Endpoint);
                        }

                        _ErrorMessageAction?.Invoke("RedisPubSubHookManager->SubscribeToTopic: Failed to perform http request for topic fatally: " + _TopicName);
                    });
                }

            }, _ErrorMessageAction);
        }

        private void TryObtainingRightForTheMessage(out bool _bAlreadyExecuted, string _Topic, string _Message, Action<string> _ErrorMessageAction)
        {
            _bAlreadyExecuted = false;

            Utility.CalculateStringMD5(_Topic + _Message, out string _Hash, _ErrorMessageAction);

            var MemoryScopeKey = $"{RedisKeyAckQueryParameters_Domain}_{RedisKeyAckQueryParameters_Subdomain}_{_Hash}";
            
            if (!MemoryService.SetKeyValueConditionally(
                MemoryScopeKey,
                new Tuple<string, PrimitiveType>(
                    "Handled",
                    new PrimitiveType("true")),
                _ErrorMessageAction,
                false))
            {
                _bAlreadyExecuted = true;
            }
            else
            {
                MemoryService.SetKeyExpireTime(MemoryScopeKey, TimeSpan.FromSeconds(5), _ErrorMessageAction);
            }
        }

        private bool TryPerformingHttpRequest(PubSubTopicToListenRequestModel _EndpointCall, string _Message, Action<string> _ErrorMessageAction)
        {
            using var Handler = new HttpClientHandler
            {
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls,
                ServerCertificateCustomValidationCallback = (a, b, c, d) => true
            };

            using var Client = new HttpClient(Handler);
            Client.Timeout = TimeSpan.FromMinutes(15);

            foreach (var Header in _EndpointCall.Headers)
            {
                Client.DefaultRequestHeaders.TryAddWithoutValidation(Header.Key, Header.Values);
            }

            try
            {
                HttpContent HttpContentIfPutPost = null;
                try
                {
                    Task<HttpResponseMessage> RequestTask;

                    switch (_EndpointCall.Verb)
                    {
                        case "GET":
                            RequestTask = Client.GetAsync(_EndpointCall.Endpoint);
                            break;
                        case "DELETE":
                            RequestTask = Client.DeleteAsync(_EndpointCall.Endpoint);
                            break;
                        case "PUT":
                            HttpContentIfPutPost = new StringContent(_Message);
                            RequestTask = Client.PutAsync(_EndpointCall.Endpoint, HttpContentIfPutPost);
                            break;
                        default: //POST (Pre-checked that it can only be POST)
                            HttpContentIfPutPost = new StringContent(_Message);
                            RequestTask = Client.PostAsync(_EndpointCall.Endpoint, HttpContentIfPutPost);
                            break;
                    }

                    using (RequestTask)
                    {
                        RequestTask.Wait();

                        using var Response = RequestTask.Result;
                        using var Content = Response.Content;
                        using var ReadResponseTask = Content.ReadAsStringAsync();

                        ReadResponseTask.Wait();

                        if ((int)Response.StatusCode >= 400)
                        {
                            _ErrorMessageAction?.Invoke("HTTP request to: " + _EndpointCall.Endpoint + ", returned code: " + (int)Response.StatusCode + ", with content: " + ReadResponseTask.Result);
                            return false;
                        }
                    }
                }
                finally
                {
                    try
                    {
                        HttpContentIfPutPost?.Dispose();
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception e)
            {
                _ErrorMessageAction?.Invoke("RedisPubSubHookManager->TryPerformingHttpRequest: Error occured during HTTP request to: " + _EndpointCall.Endpoint + ", with error: " + e.Message + ", trace: " + e.StackTrace);
                return false;
            }
            return true;
        }
    }
}