﻿// 
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using DotNetNuke.Instrumentation;

namespace DotNetNuke.Web.Api.Auth
{
    public abstract class AuthMessageHandlerBase : DelegatingHandler
    {
        private static readonly ILog Logger = LoggerSource.Instance.GetLogger(typeof(AuthMessageHandlerBase));

        public abstract string AuthScheme { get; }
        public virtual bool BypassAntiForgeryToken => false;
        public bool DefaultInclude { get; }
        public bool ForceSsl { get; }

        protected AuthMessageHandlerBase(bool includeByDefault, bool forceSsl)
        {
            DefaultInclude = includeByDefault;
            ForceSsl = forceSsl;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = OnInboundRequest(request, cancellationToken);
            if(response != null)
            {
                response.RequestMessage = response.RequestMessage ?? request; //if someone returns new HttpResponseMessage(), fill in the requestMessage for other handlers in the chain
                return Task<HttpResponseMessage>.Factory.StartNew(() => response, cancellationToken);
            }

            return base.SendAsync(request, cancellationToken).ContinueWith(x => OnOutboundResponse(x.Result, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// A chance to process inbound requests
        /// </summary>
        /// <param name="request">the request message</param>
        /// <param name="cancellationToken">a cancellationtoken</param>
        /// <returns>null normally, if a response is returned all inbound processing is terminated and the resposne is returned</returns>
        public virtual HttpResponseMessage OnInboundRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return null;
        }

        /// <summary>
        /// A change to process outbound responses
        /// </summary>
        /// <param name="response">The response message</param>
        /// <param name="cancellationToken">a cancellationtoken</param>
        /// <returns>the responsemessage</returns>
        public virtual HttpResponseMessage OnOutboundResponse(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            return response;
        }

        protected bool NeedsAuthentication(HttpRequestMessage request)
        {
            if (MustEnforceSslInRequest(request))
            {
                return !Thread.CurrentPrincipal.Identity.IsAuthenticated;
            }

            if (Logger.IsTraceEnabled)
            {
                Logger.Trace($"{AuthScheme}: Validating request vs. SSL mode ({ForceSsl}) failed. ");
            }

            // will let callers to return without authenticating the user
            return false;
        }

        protected static bool IsXmlHttpRequest(HttpRequestMessage request)
        {
            string value = null;
            IEnumerable<string> values;
            if (request != null && request.Headers.TryGetValues("X-REQUESTED-WITH", out values))
            {
                value = values.FirstOrDefault();
            }
            return !string.IsNullOrEmpty(value) &&
                   value.Equals("XmlHttpRequest", StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Validated the <see cref="ForceSsl"/> setting of the instane against the HTTP(S) request.
        /// </summary>
        /// <returns>True if <see cref="ForceSsl"/> matcher the request scheme; false otherwise.</returns>
        private bool MustEnforceSslInRequest(HttpRequestMessage request)
        {
            return !ForceSsl || request.RequestUri.Scheme.Equals("HTTPS", StringComparison.InvariantCultureIgnoreCase);
        }

        protected static void SetCurrentPrincipal(IPrincipal principal, HttpRequestMessage request)
        {
            Thread.CurrentPrincipal = principal;
            request.GetHttpContext().User = principal;
        }
    }
}
