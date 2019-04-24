﻿using Nethereum.BlockchainProcessing.Processing.Logs;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.Processing.Logs.Configuration
{
    public interface IEventSubscriptionRepository
    {
        Task<IEventSubscriptionDto[]> GetEventSubscriptionsAsync(long subscriberId);

        Task<IEventSubscriptionDto> UpsertAsync(IEventSubscriptionDto subscription);
    }
}
