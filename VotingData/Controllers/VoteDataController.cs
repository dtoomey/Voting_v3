// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace VotingData.Controllers
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.ServiceFabric.Data;
    using Microsoft.ServiceFabric.Data.Collections;

    [Route("api/[controller]")]
    public class VoteDataController : Controller
    {
        private readonly IReliableStateManager stateManager;
        public const string BALLOTS_CAST_KEY = "TotalBallotsCast";


        public VoteDataController(IReliableStateManager stateManager)
        {
            this.stateManager = stateManager;
        }

        // GET api/VoteData
        [HttpGet("")]
        public async Task<IActionResult> Get()
        {
            ServiceEventSource.Current.Message($"VotingData.Get start. ");
            CancellationToken ct = new CancellationToken();

            IReliableDictionary<string, int> votesDictionary = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, int>>("votes");
            IReliableDictionary<string, long> ballotDictionary = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, long>>("ballots");

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                IAsyncEnumerable<KeyValuePair<string, int>> list = await votesDictionary.CreateEnumerableAsync(tx);
                IAsyncEnumerator<KeyValuePair<string, int>> enumerator = list.GetAsyncEnumerator();
                List<KeyValuePair<string, int>> result = new List<KeyValuePair<string, int>>();

                while (await enumerator.MoveNextAsync(ct))
                {
                    result.Add(enumerator.Current);
                }

                ServiceEventSource.Current.Message($"VotingData.Get end.");
                return this.Json(result);
            }
        }

        // PUT api/VoteData/name
        [HttpPut("{name}")]
        public async Task<IActionResult> Put(string name)
        {
            ServiceEventSource.Current.Message($"VotingData.Put start. name='{name}'");
            int result = 0;

            IReliableDictionary<string, int> votesDictionary = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, int>>("votes");

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                result = await votesDictionary.AddOrUpdateAsync(tx, name, 1, (key, oldvalue) => ++oldvalue);
                await tx.CommitAsync();
            }

            ServiceEventSource.Current.Message($"VotingData.Put end. Total votes: {result.ToString()}");
            return new OkResult();
        }

        // POST api/VoteData
        [HttpPost("")]
        public async Task<IActionResult> Post()
        {
            ServiceEventSource.Current.Message($"VotingData.Post start.");
            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                await this.AuditBallot(1);               
                await tx.CommitAsync();
            }

            ServiceEventSource.Current.Message($"VotingData.Post end.");
            return new OkResult();
        }

        // DELETE api/VoteData/name
        [HttpDelete("{name}")]
        public async Task<IActionResult> Delete(string name)
        {
            ServiceEventSource.Current.Message($"VotingData.Delete start. name='{name}'");
            ConditionalValue<int> result = new ConditionalValue<int>(false, -1);

            IReliableDictionary<string, int> votesDictionary = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, int>>("votes");
            IReliableDictionary<string, long> ballotDictionary = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, long>>("ballots");

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                if (await votesDictionary.ContainsKeyAsync(tx, name))
                {
                    ConditionalValue<int> deleteVotes = await votesDictionary.TryGetValueAsync(tx, name);
                    result = await votesDictionary.TryRemoveAsync(tx, name);
                    long ballots = await GetTotalBallotsCast();
                    await AuditBallot(-1 * (ballots >= deleteVotes.Value ? deleteVotes.Value : ballots));
                    await tx.CommitAsync();
                    ServiceEventSource.Current.Message($"VotingData.Delete end. '{name}' deleted.");
                    return new OkResult();
                }
                else
                {
                    ServiceEventSource.Current.Message($"VotingData.Delete end. '{name}' not found.");
                    return new NotFoundResult();
                }
            }
        }

        public async Task<IActionResult> AuditBallot(long count)
        {
            ServiceEventSource.Current.Message($"VotingData.AuditBallot start.");
            IReliableDictionary<string, long> ballotDictionary = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, long>>("ballots");

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                long result = await ballotDictionary.AddOrUpdateAsync(tx, BALLOTS_CAST_KEY, count, (key, value) => value + count);
                await tx.CommitAsync();
                ServiceEventSource.Current.Message($"VotingData.AuditBallot end. Adjustment: '{result.ToString()}'");
                return new OkResult();
            }
        }

        public async Task<int> GetNumberOfVotes(string voteItem)
        {
            ServiceEventSource.Current.Message("VotingData.GetNumberOfVotes start.");
            ConditionalValue<int> result = new ConditionalValue<int>(true, 0);

            IReliableDictionary<string, int> votesDictionary = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, int>>("votes");

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                if (votesDictionary != null)
                {
                    result = await votesDictionary.TryGetValueAsync(tx, voteItem);
                    await tx.CommitAsync();
                }
            }

            ServiceEventSource.Current.Message("VotingData.GetNumberOfVotes end.");
            return result.HasValue ? result.Value : 0;
        }

        // DELETE api/VoteData/name
        [HttpGet("ballots")]
        public async Task<long> GetTotalBallotsCast()
        {
            ServiceEventSource.Current.Message("VotingData.GetTotalBallotsCast start.");
            ConditionalValue<long> result = new ConditionalValue<long>(true, 0);

            IReliableDictionary<string, long> ballotDictionary = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, long>>("ballots");

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                if (ballotDictionary != null)
                {
                    result = await ballotDictionary.TryGetValueAsync(tx, BALLOTS_CAST_KEY);
                    await tx.CommitAsync();
                }
            }

            ServiceEventSource.Current.Message("VotingData.GetTotalBallotsCast end.");
            return result.HasValue ? result.Value : 0;
        }

        public async Task<List<KeyValuePair<string, int>>> GetAllVoteCounts()
        {
            ServiceEventSource.Current.Message("VotingData.GetAllVoteCounts start.");
            List<KeyValuePair<string, int>> kvps = new List<KeyValuePair<string, int>>();

            IReliableDictionary<string, int> votesDictionary = await this.stateManager.GetOrAddAsync<IReliableDictionary<string, int>>("votes");

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                if (votesDictionary != null)
                {
                    IAsyncEnumerable<KeyValuePair<string, int>> e = await votesDictionary.CreateEnumerableAsync(tx);
                    IAsyncEnumerator<KeyValuePair<string, int>> items = e.GetAsyncEnumerator();

                    while (await items.MoveNextAsync(new CancellationToken()))
                    {
                        kvps.Add(new KeyValuePair<string, int>(items.Current.Key, items.Current.Value));
                    }

                    //kvps.Sort((x, y) => x.Value.CompareTo(y.Value) * -1);  // intentionally commented out!
                }
                await tx.CommitAsync();
            }

            ServiceEventSource.Current.Message($"VotingData.GetAllVoteCounts end. Number of keys: {kvps.Count.ToString()}");
            return kvps;
        }

        public async Task<string> CheckVotesIntegrity()
        {
            long totalVotesAcrossItems = 0;
            long totalBallotsCast = 0;
            string result = null;

            using (ITransaction tx = this.stateManager.CreateTransaction())
            {
                totalBallotsCast = await GetTotalBallotsCast();
                var voteItems = await GetAllVoteCounts();

                foreach (var item in voteItems)
                {
                    totalVotesAcrossItems += item.Value;
                }

                if (totalBallotsCast != totalVotesAcrossItems)
                {
                    result = $"Total votes across items [{totalVotesAcrossItems.ToString()}] does not equal total ballots cast [{totalBallotsCast.ToString()}].";
                }

                await tx.CommitAsync();
            }

            return result;
        }



    }
}