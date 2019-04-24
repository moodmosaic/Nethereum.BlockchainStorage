﻿using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.Processing.Logs.Handling
{
    public interface ISubscriberQueueFactory
    {
        Task<IQueue> GetSubscriberQueueAsync(long subscriberId, long subscriberQueueId);
    }
}
