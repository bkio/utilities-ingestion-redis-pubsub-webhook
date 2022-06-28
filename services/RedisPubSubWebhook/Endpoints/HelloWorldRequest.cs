/// Copyright 2022- Burak Kara, All rights reserved.

using System;
using System.Net;
using WebServiceUtilities;
using WebResponse = WebServiceUtilities.WebResponse;

namespace RedisPubSubWebhook
{
    internal class HelloWorldRequest : WebServiceBase
    {
        protected override WebServiceResponse OnRequest(HttpListenerContext _Context, Action<string> _ErrorMessageAction = null)
        {
            return WebResponse.StatusOK("Hello world!");
        }
    }
}