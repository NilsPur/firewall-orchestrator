﻿using FWO.ApiClient;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace FWO.Middleware.Server.Requests
{
    class GetInternalGroupsRequestHandler : RequestHandler
    {
        private APIConnection ApiConn;
        
        /// <summary>
        /// Connected Ldaps to handle requests
        /// </summary>
        private List<Ldap> Ldaps;

        public GetInternalGroupsRequestHandler(List<Ldap> Ldaps, APIConnection ApiConn)
        {
            this.Ldaps = Ldaps;
            this.ApiConn = ApiConn;
        }

        protected override async Task<(HttpStatusCode status, string wrappedResult)> HandleRequestInternalAsync(HttpListenerRequest request)
        {
            // No parameters

            List<KeyValuePair<string, List<string>>> allGroups = new List<KeyValuePair<string, List<string>>>();
            List<Task> ldapGroupRequests = new List<Task>();

            foreach (Ldap currentLdap in Ldaps)
            {
                if (currentLdap.IsInternal() && currentLdap.HasGroupHandling())
                {
                    ldapGroupRequests.Add(Task.Run(() =>
                    {
                        // Get all groups from internal Ldap
                        List<KeyValuePair<string, List<string>>> currentGroups = currentLdap.GetAllInternalGroups();
                        allGroups.AddRange(currentGroups);
                    }));
                }
            }

            await Task.WhenAll(ldapGroupRequests);

            // Return status and result
            return WrapResult(HttpStatusCode.OK, ("allGroups", allGroups));
        }
    }
}
