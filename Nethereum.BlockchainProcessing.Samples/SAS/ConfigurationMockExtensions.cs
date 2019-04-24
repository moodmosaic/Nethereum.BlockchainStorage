﻿using Moq;
using Nethereum.BlockchainProcessing.Processing.Logs;
using Nethereum.BlockchainProcessing.Processing.Logs.Configuration;
using Nethereum.BlockchainProcessing.Processing.Logs.Handling.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.BlockchainProcessing.Samples.SAS
{
    public class IdGenerator
    {
        Dictionary<Type, long> _counters = new Dictionary<Type, long>();
        public long Next<T>()
        {
            long id = 1;
            if (_counters.TryGetValue(typeof(T), out long val))
            {
                id = val + 1;
            }
            _counters[typeof(T)] = id;
            return id;
        }
    }

    public static class ConfigurationMockExtensions
    {
        public static IEventProcessingConfigurationRepository CreateMockRepository(this EventProcessingConfigContext repo, IdGenerator id)
        {
            Mock<IEventProcessingConfigurationRepository> configDb = new Mock<IEventProcessingConfigurationRepository>();

            configDb.Setup(h => h.AddAsync(It.IsAny<IEventHandlerHistoryDto>()))
                .Returns<IEventHandlerHistoryDto>((history) =>
                {
                    repo.Add(history);
                    return Task.CompletedTask;
                });

            configDb.Setup(h => h.ContainsEventHandlerHistoryAsync(It.IsAny<long>(), It.IsAny<string>()))
                .Returns<long, string>((eventHandlerId, eventKey) =>
                {
                    var exists = repo.EventHandlerHistories.Any(h => 
                    h.EventHandlerId == eventHandlerId && 
                    h.EventKey == eventKey);

                    return Task.FromResult(exists);
                });

            configDb
                .Setup(d => d.GetSubscribersAsync(It.IsAny<long>()))
                .Returns<long>((partitionId) => Task.FromResult(repo.Subscribers.Where(s => s.PartitionId == partitionId).Cast<ISubscriberDto>().ToArray()));

            configDb
                .Setup(d => d.GetEventSubscriptionsAsync(It.IsAny<long>()))
                .Returns<long>((subscriberId) => Task.FromResult(repo.EventSubscriptions.Where(s => s.SubscriberId == subscriberId).Cast<IEventSubscriptionDto>().ToArray()));

            configDb
                .Setup(d => d.GetSubscriberContractAsync(It.IsAny<long>(), It.IsAny<long>()))
                .Returns<long, long>((subscriberId, contractId) => Task.FromResult(repo.Contracts.Where(s => s.SubscriberId == subscriberId && s.Id == contractId).Cast<ISubscriberContractDto>().FirstOrDefault()));

            configDb
                .Setup(d => d.GetEventSubscriptionAddressesAsync(It.IsAny<long>()))
                .Returns<long>((eventSubscriptionId) => Task.FromResult(repo.EventSubscriptionAddresses.Where(s => s.EventSubscriptionId == eventSubscriptionId).Cast<IEventSubscriptionAddressDto>().ToArray()));

            configDb
                .Setup(d => d.GetParameterConditionsAsync(It.IsAny<long>()))
                .Returns<long>((eventSubscriptionId) => Task.FromResult(repo.ParameterConditions.Where(s => s.EventSubscriptionId == eventSubscriptionId).Cast<IParameterConditionDto>().ToArray()));

            configDb
                .Setup(d => d.GetEventHandlersAsync(It.IsAny<long>()))
                .Returns<long>((eventSubscriptionId) => Task.FromResult(repo.DecodedEventHandlers.Where(s => s.EventSubscriptionId == eventSubscriptionId).Cast<IEventHandlerDto>().ToArray()));

            configDb
                .Setup(d => d.GetOrCreateEventSubscriptionStateAsync(It.IsAny<long>()))
                .Returns<long>((eventSubscriptionId) =>
                {
                    var state = repo.GetEventSubscriptionState(eventSubscriptionId);
                    if(state == null)
                    {
                        state = repo.Add(new EventSubscriptionStateDto(eventSubscriptionId));
                    }
                    return Task.FromResult(state as IEventSubscriptionStateDto);
                });

            configDb
                .Setup(d => d.UpsertAsync(It.IsAny<IEnumerable<IEventSubscriptionStateDto>>()))
                .Callback<IEnumerable<IEventSubscriptionStateDto>>((states) =>
                { 
                    foreach(var state in states)
                    { 
                        //simulate an update
                        //this is in memory so not really representative
                        var existing = repo.EventSubscriptionStates.FirstOrDefault(s => s.EventSubscriptionId == state.EventSubscriptionId);
                        var index = repo.EventSubscriptionStates.IndexOf(existing);
                        repo.EventSubscriptionStates[index] = state;
                    }
                 })
                .Returns(Task.CompletedTask);

            configDb
                .Setup(d => d.GetContractQueryConfigurationAsync(It.IsAny<long>(), It.IsAny<long>()))
                .Returns<long, long>((subscriberId, eventHandlerId) =>
                {
                    var contractQuery = repo.ContractQueries.FirstOrDefault(c => c.EventHandlerId == eventHandlerId);
                    if (contractQuery == null) throw new ArgumentException($"Could not find Contract Query Configuration for Event Handler Id: {eventHandlerId}");
                    var contract = repo.Contracts.FirstOrDefault(c => c.SubscriberId == subscriberId && c.Id == contractQuery.ContractId);
                    if (contract == null) throw new ArgumentException($"Could not find Contract Query Id: {contractQuery.Id}, Contract Id: {contractQuery.ContractId}");
                    var parameters = repo.ContractQueryParameters.Where(p => p.ContractQueryId == contractQuery.Id);

                    ContractQueryConfiguration config = Map(contractQuery, contract, parameters);

                    return Task.FromResult(config);
                });

            configDb
                .Setup(d => d.GetEventAggregationConfigurationAsync(It.IsAny<long>()))
                .Returns<long>((eventHandlerId) =>
                {
                    var dto = repo.EventAggregators.FirstOrDefault(c => c.EventHandlerId == eventHandlerId);
                    if (dto == null) throw new ArgumentException($"Could not find Event Aggregator Configuration for Event Handler Id: {eventHandlerId}");

                    EventAggregatorConfiguration config = Map(dto);

                    return Task.FromResult(config);
                });

            configDb
                .Setup(d => d.GetSubscriberQueueAsync(It.IsAny<long>(), It.IsAny<long>()))
                .Returns<long, long>((subscriberId, subscriberQueueId) =>
                {
                    var dto = repo.SubscriberQueues.FirstOrDefault(q => q.SubscriberId == subscriberId && q.Id == subscriberQueueId);
                    if (dto == null) throw new ArgumentException($"Could not find Subscriber Queue Id: {subscriberQueueId}");
                    return Task.FromResult(dto as ISubscriberQueueDto);
                });

            configDb
                .Setup(d => d.GetSubscriberSearchIndexAsync(It.IsAny<long>(), It.IsAny<long>()))
                .Returns<long, long>((subscriberId, subscriberSearchIndexId) => {
                    var dto = repo.SubscriberSearchIndexes.FirstOrDefault(q => q.SubscriberId == subscriberId && q.Id == subscriberSearchIndexId);
                    if (dto == null) throw new ArgumentException($"Could not find Subscriber Search Index Id: {subscriberSearchIndexId}");
                    return Task.FromResult(dto as ISubscriberSearchIndexDto);
                });

            configDb
                .Setup(d => d.GetSubscriberStorageAsync(It.IsAny<long>(), It.IsAny<long>()))
                .Returns<long, long>((subscriberId, subscriberRepositoryId) => {
                    var dto = repo.SubscriberRepositories.FirstOrDefault(q => q.SubscriberId == subscriberId && q.Id == subscriberRepositoryId);
                    if (dto == null) throw new ArgumentException($"Could not find Subscriber Repository Id: {subscriberRepositoryId}");
                    return Task.FromResult(dto as ISubscriberStorageDto);
                });

            configDb
                .Setup(d => d.GetEventRuleConfigurationAsync(It.IsAny<long>()))
                .Returns<long>((eventHandlerId) =>
                {
                    var dto = repo.EventRuleConfigurations.FirstOrDefault(c => c.EventHandlerId == eventHandlerId);
                    if (dto == null) throw new ArgumentException($"Could not find EventRuleConfiguration for Event Handler Id: {eventHandlerId}");
                    return Task.FromResult(Map(dto));
                });
                

            return configDb.Object;
        }

        private static IEventRuleDto Map(EventRuleConfigurationDto dto)
        {
            return new EventRuleDto
            {
                EventParameterNumber = dto.EventParameterNumber,
                InputName = dto.SourceKey,
                Source = dto.Source,
                Type = dto.Type,
                Value = dto.Value
            };
        }

        private static EventAggregatorConfiguration Map(EventAggregatorDto dto)
        {
            return new EventAggregatorConfiguration
            {
                Destination = dto.Destination,
                EventParameterNumber = dto.EventParameterNumber,
                SourceKey = dto.SourceKey,
                Operation = dto.Operation,
                OutputKey = dto.OutputKey,
                Source = dto.Source
            };
        }

        private static ContractQueryConfiguration Map(ContractQueryDto contractQuery, SubscriberContractDto contract, IEnumerable<ContractQueryParameterDto> parameters)
        {
            return new ContractQueryConfiguration
            {
                ContractABI = contract.Abi,
                ContractAddress = contractQuery.ContractAddress,
                ContractAddressParameterNumber = contractQuery.ContractAddressParameterNumber,
                ContractAddressSource = contractQuery.ContractAddressSource,
                ContractAddressStateVariableName = contractQuery.ContractAddressStateVariableName,
                EventStateOutputName = contractQuery.EventStateOutputName,
                FunctionSignature = contractQuery.FunctionSignature,
                SubscriptionStateOutputName = contractQuery.SubscriptionStateOutputName,
                Parameters = parameters.Select(p => new ContractQueryParameter
                {
                    Order = p.Order,
                    EventParameterNumber = p.EventParameterNumber,
                    EventStateName = p.EventStateName,
                    Source = p.Source,
                    Value = p.Value
                }).ToArray()
            };
        }
    }


}
