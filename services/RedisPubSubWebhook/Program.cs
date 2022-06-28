/// Copyright 2022- Burak Kara, All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using CloudConnectors;
using CloudServiceUtilities;
using WebServiceUtilities;

namespace RedisPubSubWebhook
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("Initializing the service...");

            /*
            * Common initialization step
            */
            if (!CloudConnector.Initialize(out CloudConnector Connector,
                new string[][]
                {
                    new string[] { "DEPLOYMENT_BRANCH_NAME" },
                    new string[] { "DEPLOYMENT_BUILD_NUMBER" },

                    new string[] { "REDIS_ENDPOINT" },
                    new string[] { "REDIS_PORT" },
                    new string[] { "REDIS_PASSWORD" },
                    new string[] { "REDIS_SSL_ENABLED" },

                    new string[] { "TOPIC_ENDPOINT_MAP_FETCH_METHOD" }, //REDIS or ENV_VAR

                    //If TOPIC_ENDPOINT_MAP_FETCH_METHOD == REDIS
                    //Set REDIS_KEY_TOPICS_TO_BE_LISTENED_MAP. Example value: PUBSUB:WEBHOOK:CONFIG:TOPICS (There must be THREE delimiters, and delimiters must be :)
                    
                    //If TOPIC_ENDPOINT_MAP_FETCH_METHOD == ENV_VAR
                    //Set TOPIC_ENDPOINT_MAP. Example value: "{ \"topicName\":[{\"endpoint...."
                        
                    new string[] { "REDIS_KEY_TOPICS_TO_BE_LISTENED_MAP", "TOPIC_ENDPOINT_MAP" },

                    //Example value: PUBSUB:WEBHOOK_ACK (There must be ONE delimiter, and : must be the it.)
                    new string[] { "REDIS_KEY_PREFIX_ACK" }
                }))
                return;

            bool bInitSuccess = true;
            bInitSuccess &= Connector.WithPubSubService();
            bInitSuccess &= Connector.WithMemoryService();
            if (!bInitSuccess) return;

            var DeploymentBranchName = Connector.RequiredEnvironmentVariables["DEPLOYMENT_BRANCH_NAME"];
            Resources_DeploymentManager.Get().SetDeploymentBranchNameAndBuildNumber(DeploymentBranchName, Connector.RequiredEnvironmentVariables["DEPLOYMENT_BUILD_NUMBER"]);

            var TopicEndpointMapFetchMethod = Connector.RequiredEnvironmentVariables["TOPIC_ENDPOINT_MAP_FETCH_METHOD"].ToUpper();
            string RedisKeyTopicMap = null;
            string TopicEndpointMapJsonAsString = null;

            if (TopicEndpointMapFetchMethod == "REDIS")
            {
                if (!Connector.RequiredEnvironmentVariables.ContainsKey("REDIS_KEY_TOPICS_TO_BE_LISTENED_MAP"))
                {
                    Connector.LogService.WriteLogs(LogServiceMessageUtility.Single(ELogServiceLogType.Error, "If TOPIC_ENDPOINT_MAP_FETCH_METHOD is REDIS; REDIS_KEY_TOPICS_TO_BE_LISTENED_MAP must be set too."), Connector.ProgramID, "Initialization");
                    return;
                }
                RedisKeyTopicMap = Connector.RequiredEnvironmentVariables["REDIS_KEY_TOPICS_TO_BE_LISTENED_MAP"];
            }
            else if (TopicEndpointMapFetchMethod == "ENV_VAR")
            {
                if (!Connector.RequiredEnvironmentVariables.ContainsKey("TOPIC_ENDPOINT_MAP"))
                {
                    Connector.LogService.WriteLogs(LogServiceMessageUtility.Single(ELogServiceLogType.Error, "If TOPIC_ENDPOINT_MAP_FETCH_METHOD is ENV_VAR; TOPIC_ENDPOINT_MAP must be set too."), Connector.ProgramID, "Initialization");
                    return;
                }
                TopicEndpointMapJsonAsString = Connector.RequiredEnvironmentVariables["TOPIC_ENDPOINT_MAP"];
            }
            else
            {
                Connector.LogService.WriteLogs(LogServiceMessageUtility.Single(ELogServiceLogType.Error, "TOPIC_ENDPOINT_MAP_FETCH_METHOD could be either REDIS or ENV_VAR."), Connector.ProgramID, "Initialization");
                return;
            }

            var RedisKeyPrefixAck = Connector.RequiredEnvironmentVariables["REDIS_KEY_PREFIX_ACK"];

            if (!RedisPubSubHookManager.Get().StartOperation(
                    Connector.MemoryService, 
                    Connector.PubSubService,
                    TopicEndpointMapFetchMethod,
                    RedisKeyTopicMap,
                    TopicEndpointMapJsonAsString,
                    RedisKeyPrefixAck, 
            (string Message) =>
            {
                Connector.LogService.WriteLogs(LogServiceMessageUtility.Single(ELogServiceLogType.Info, Message), Connector.ProgramID, "RedisPubSubHookManager");
            }))
            {
                return;
            }

            string RootPath;
            if (DeploymentBranchName != "master" && DeploymentBranchName != "main" && DeploymentBranchName != "development")
            {
                RootPath = $"/{DeploymentBranchName}/";
            }
            else
            {
                RootPath = "/";
            }

            /*
            * Web-http service initialization (for health-check)
            */
            new WebService(new List<WebPrefixStructure>()
            {
                new WebPrefixStructure(new string[] { $"{RootPath}internal/hello_world" }, () => new HelloWorldRequest()),
            }
            .ToArray(), Connector.ServerPort).Run((string Message) =>
            {
                Connector.LogService.WriteLogs(LogServiceMessageUtility.Single(ELogServiceLogType.Info, Message), Connector.ProgramID, "WebService");
            });

            /*
            * Make main thread sleep forever
            */
            Thread.Sleep(Timeout.Infinite);
        }
    }
}