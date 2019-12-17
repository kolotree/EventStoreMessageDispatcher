﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto;
using ProtoActorAdapter.Actors.Messages;

namespace ProtoActorAdapter.Actors
{
    internal sealed class RootActor : IActor
    {
        private readonly Uri _destinationUri;
        
        private readonly Dictionary<string, PID> _appliersByAggregateId = new Dictionary<string, PID>();
        private long _lastRoutedEvent = -1;

        public RootActor(Uri destinationUri)
        {
            _destinationUri = destinationUri;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    break;
                case ReadLastRoutedEvent _:
                    context.Respond(new LastRoutedDomainEvent(_lastRoutedEvent));
                    break;
                case RouteDomainEvent message:
                    var applierActor = LocateChildActorForDomainEvent(context, message);
                    context.Send(applierActor, message.ToEnqueueDomainEvent());
                    break;
            }

            return Task.CompletedTask;
        }

        private PID LocateChildActorForDomainEvent(IContext context, RouteDomainEvent domainEvent)
        {
            if (!_appliersByAggregateId.TryGetValue(domainEvent.ChildActorId(), out var applierActor))
            {
                applierActor = CreateAggregateEventApplierActorOf(context, domainEvent.ChildActorId());
            }

            return applierActor;
        }

        private PID CreateAggregateEventApplierActorOf(IContext context, string aggregateId)
        {
            var props = Props.FromProducer(() => new AggregateEventApplierActor(_destinationUri));
            var applierActor = context.Spawn(props);
            _appliersByAggregateId.Add(aggregateId, applierActor);
            return applierActor;
        }
    }
}