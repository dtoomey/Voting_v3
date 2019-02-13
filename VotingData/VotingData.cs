// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace VotingData
{
    using System;
    using System.Collections.Generic;
    using System.Fabric;
    using System.Fabric.Health;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using global::VotingData.Controllers;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.ServiceFabric.Data;
    using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
    using Microsoft.ServiceFabric.Services.Communication.Runtime;
    using Microsoft.ServiceFabric.Services.Runtime;

    /// <summary>
    /// The FabricRuntime creates an instance of this class for each service type instance. 
    /// </summary>
    internal sealed class VotingData : StatefulService
    {
        private const int HEALTH_CHECK_INTERVAL_IN_SECONDS = 5;

        public VotingData(StatefulServiceContext context)
            : base(context)
        {
        }

        /// <summary>
        /// Optional override to create listeners (like tcp, http) for this service instance.
        /// </summary>
        /// <returns>The collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new ServiceReplicaListener[]
            {
                new ServiceReplicaListener(
                    serviceContext =>
                        new KestrelCommunicationListener(
                            serviceContext,
                            (url, listener) =>
                            {
                                ServiceEventSource.Current.ServiceMessage(serviceContext, $"Starting Kestrel on {url}");

                                return new WebHostBuilder()
                                    .UseKestrel()
                                    .ConfigureServices(
                                        services => services
                                            .AddSingleton<StatefulServiceContext>(serviceContext)
                                            .AddSingleton<IReliableStateManager>(this.StateManager))
                                    .UseContentRoot(Directory.GetCurrentDirectory())
                                    .UseStartup<Startup>()
                                    .UseApplicationInsights()
                                    .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.UseUniqueServiceUrl)
                                    .UseUrls(url)
                                    .Build();
                            }))
            };
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            VoteDataController controller = new VoteDataController(this.StateManager);
            HealthReportSendOptions sendOptions = new HealthReportSendOptions() { Immediate = true };

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string result = await controller.CheckVotesIntegrity();
                if (!String.IsNullOrEmpty(result))
                {
                    HealthInformation healthInformation = new HealthInformation("ServiceCode", "StateDictionary", HealthState.Error);
                    healthInformation.Description = result;
                    this.Partition.ReportReplicaHealth(healthInformation, sendOptions);
                }
                else
                {
                    HealthInformation healthInformation = new HealthInformation("ServiceCode", "StateDictionary", HealthState.Ok);
                    this.Partition.ReportReplicaHealth(healthInformation, sendOptions);
                }

                await Task.Delay(TimeSpan.FromSeconds(HEALTH_CHECK_INTERVAL_IN_SECONDS), cancellationToken);
            }
        }

    }
}