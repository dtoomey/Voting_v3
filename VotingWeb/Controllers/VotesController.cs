// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace VotingWeb.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Fabric;
    using System.Fabric.Query;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json;

    [Produces("application/json")]
    [Route("api/[controller]")]
    public class VotesController : Controller
    {
        private readonly HttpClient httpClient;
        private readonly FabricClient fabricClient;
        private readonly string reverseProxyBaseUri;
        private readonly StatelessServiceContext serviceContext;

        public VotesController(HttpClient httpClient, StatelessServiceContext context, FabricClient fabricClient)
        {
            this.fabricClient = fabricClient;
            this.httpClient = httpClient;
            this.serviceContext = context;
            this.reverseProxyBaseUri = Environment.GetEnvironmentVariable("ReverseProxyBaseUri");
        }

        // GET: api/Votes
        [HttpGet("")]
        public async Task<IActionResult> Get()
        {
            ServiceEventSource.Current.ServiceRequestStart("VotesController.Get");

            Uri serviceName = VotingWeb.GetVotingDataServiceName(this.serviceContext);
            Uri proxyAddress = this.GetProxyAddress(serviceName);
            long totalBallots = 0;

            ServicePartitionList partitions = await this.fabricClient.QueryManager.GetPartitionListAsync(serviceName);
            List<KeyValuePair<string, int>> votes = new List<KeyValuePair<string, int>>();

            foreach (Partition partition in partitions)
            {
                string proxyUrl =
                    $"{proxyAddress}/api/VoteData?PartitionKey={((Int64RangePartitionInformation)partition.PartitionInformation).LowKey}&PartitionKind=Int64Range";

                using (HttpResponseMessage response = await this.httpClient.GetAsync(proxyUrl))
                {
                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        continue;
                    }

                    votes.AddRange(JsonConvert.DeserializeObject<List<KeyValuePair<string, int>>>(await response.Content.ReadAsStringAsync()));
                }

                string proxyUrl2 =
                   $"{proxyAddress}/api/VoteData/ballots?PartitionKey={((Int64RangePartitionInformation)partition.PartitionInformation).LowKey}&PartitionKind=Int64Range";

                using (HttpResponseMessage response = await this.httpClient.GetAsync(proxyUrl2))
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        Int64.TryParse(await response.Content.ReadAsStringAsync(), out totalBallots);
                    }
                }
            }

            Models.VoteTally result = new Models.VoteTally() { Votes = votes, TotalBallots = totalBallots };

            return this.Json(result);
        }

        // PUT: api/Votes/name
        [HttpPut("{name}")]
        public async Task<IActionResult> Put(string name)
        {
            ServiceEventSource.Current.ServiceRequestStart("VotesController.Put");
            ContentResult result = null;

            Uri serviceName = VotingWeb.GetVotingDataServiceName(this.serviceContext);
            Uri proxyAddress = this.GetProxyAddress(serviceName);
            long partitionKey = this.GetPartitionKey(name);
            string proxyUrl = $"{proxyAddress}/api/VoteData/{name}?PartitionKey={partitionKey}&PartitionKind=Int64Range";

            StringContent putContent = new StringContent($"{{ 'name' : '{name}' }}", Encoding.UTF8, "application/json");
            putContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            // Insert "trump" snippet here to introduce a nasty bug.
            
            // Record the new vote
            using (HttpResponseMessage response = await this.httpClient.PutAsync(proxyUrl, putContent))
            {
                result = new ContentResult()
                {
                    StatusCode = (int)response.StatusCode,
                    Content = await response.Content.ReadAsStringAsync()
                };
            }

            // Separately audit the ballot
            if (result.StatusCode == 200)
            {
                proxyUrl = $"{proxyAddress}/api/VoteData/?PartitionKey={partitionKey}&PartitionKind=Int64Range";
                using (HttpResponseMessage response = await this.httpClient.PostAsync(proxyUrl, null))
                {
                    result = new ContentResult()
                    {
                        StatusCode = (int)response.StatusCode,
                        Content = await response.Content.ReadAsStringAsync()
                    };
                }
            }

            return result;
        }

        // DELETE: api/Votes/name
        [HttpDelete("{name}")]
        public async Task<IActionResult> Delete(string name)
        {
            ServiceEventSource.Current.ServiceRequestStart("VotesController.Delete");

            Uri serviceName = VotingWeb.GetVotingDataServiceName(this.serviceContext);
            Uri proxyAddress = this.GetProxyAddress(serviceName);
            long partitionKey = this.GetPartitionKey(name);
            string proxyUrl = $"{proxyAddress}/api/VoteData/{name}?PartitionKey={partitionKey}&PartitionKind=Int64Range";

            using (HttpResponseMessage response = await this.httpClient.DeleteAsync(proxyUrl))
            {
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    return this.StatusCode((int)response.StatusCode);
                }
            }

            return new OkResult();
        }


        // GET api/appVersion 
        [HttpGet("appVersion")]
        public async Task<string> GetAppVersion()
        {
            ServiceEventSource.Current.ServiceRequestStart("VotesController.GetAppVersion");

            try
            {
                var applicationName = new Uri("fabric:/Voting");
                using (var client = new FabricClient())
                {
                    var applications = await client.QueryManager.GetApplicationListAsync(applicationName).ConfigureAwait(false);
                    return applications[0].ApplicationTypeVersion;
                }
            }
            catch (Exception ex)
            {
                ServiceEventSource.Current.Message("Error in VotesController.GetAppVersion method: {0}", ex.Message);
                return "An error occurred: " + ex.Message;
            }

        }

        // GET api/appVersion 
        [HttpGet("currentNode")]
        public string GetCurrentNode()
        {
            ServiceEventSource.Current.ServiceRequestStart("VotesController.GetCurrentNode");
            return this.serviceContext.NodeContext.NodeName;
        }

        /// <summary>
        /// Constructs a reverse proxy URL for a given service.
        /// Example: http://localhost:19081/VotingApplication/VotingData/
        /// </summary>
        /// <param name="serviceName"></param>
        /// <returns></returns>
        private Uri GetProxyAddress(Uri serviceName)
        {
            return new Uri($"{this.reverseProxyBaseUri}{serviceName.AbsolutePath}");
        }

        /// <summary>
        /// Creates a partition key from the given name.
        /// Uses the zero-based numeric position in the alphabet of the first letter of the name (0-25).
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private long GetPartitionKey(string name)
        {
            return Char.ToUpper(name.First()) - 'A';
        }
    }
}
