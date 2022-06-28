/// Copyright 2022- Burak Kara, All rights reserved.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RedisPubSubWebhook
{
    public class PubSubTopicToListenRequestModel
    {
        public const string ENDPOINT_PROPERTY = "endpoint";
        public const string HEADERS_PROPERTY = "headers";
        public const string VERB_PROPERTY = "verb";

        [JsonProperty(ENDPOINT_PROPERTY)]
        public string Endpoint = "";

        [JsonProperty(HEADERS_PROPERTY)]
        public List<PubSubTopicToListenRequestHeaderModel> Headers = new List<PubSubTopicToListenRequestHeaderModel>();

        [JsonProperty(VERB_PROPERTY)]
        public string Verb = "";

        public bool Validate()
        {
            if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out Uri UriResult)
                || (UriResult.Scheme != Uri.UriSchemeHttp && UriResult.Scheme != Uri.UriSchemeHttps)
                || (Verb != "GET" && Verb != "POST" && Verb != "PUT" && Verb != "DELETE")) return false;

            foreach (var Header in Headers)
            {
                if (Header == null || Header.Key.Length == 0 || Header.Values == null || Header.Values.Count == 0) return false;

                foreach (var HeaderValue in Header.Values)
                {
                    if (HeaderValue == null || HeaderValue.Length == 0) return false;
                }
            }

            return true;
        }
    }

    public class PubSubTopicToListenRequestHeaderModel
    {
        public const string KEY_PROPERTY = "key";
        public const string VALUES_PROPERTY = "values";

        [JsonProperty(KEY_PROPERTY)]
        public string Key = "";

        [JsonProperty(VALUES_PROPERTY)]
        public List<string> Values = new List<string>();
    }
}