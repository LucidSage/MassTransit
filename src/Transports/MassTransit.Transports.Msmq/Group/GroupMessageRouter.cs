// Copyright 2007-2012 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.Transports.Msmq.Group
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using log4net;
    using Magnum.Extensions;
    using Magnum.Reflection;
	using MassTransit.Pipeline;
	using MassTransit.Pipeline.Sinks;
    using MassTransit.Subscriptions.Messages;
    using MassTransit.Util;
    using MassTransit.Transports.Msmq.Configuration;

	class GroupMessageRouter :
		IBusService,
        IDisposable,
        IPipelineSink<ISendContext>,
        Consumes<AddPeerSubscription>.Context,
        Consumes<RemovePeerSubscription>.Context
	{
        readonly IDictionary<Uri, PeerSubscription> _workers = new Dictionary<Uri, PeerSubscription>();

		UnsubscribeAction _unsubscribeAction = () => false;
		IServiceBus _bus;
        IPipelineSink<ISendContext> _defaultSink;
        IControlBus _subscriptionBus;
        IGroupSelectionStrategy _selectionStrategy;

        public GroupMessageRouter(
            MulticastSubscriptionClientConfiguratorImpl configurator,
            IGroupSelectionStrategy selectionStrategy)
        {
            _subscriptionBus = configurator.SubscriptionBus;
            _selectionStrategy = selectionStrategy;
		}

		public IEnumerable<Action<ISendContext>> Enumerate(ISendContext context)
		{
			yield return ctx =>
            {
                List<PeerSubscription> recipients;
                lock (_workers)
                {
                    // Use ToList to force Linq to evaluate the expression while _workers is locked
                    recipients = _workers
                        .Values
                        .Where( peer => peer.MessageName == context.MessageType )
                        .GroupBy(peer => peer.Group)
                        .Select(workers => _selectionStrategy.Select(workers))
                        .ToList();
                }
                
                recipients.Each(wd => this.FastInvoke<GroupMessageRouter>(new[]{ Type.GetType(context.MessageType) }, "SendMessageToWorker", context, wd));
			};
		}

        [UsedImplicitly]
        void SendMessageToWorker<TMessage>(ISendContext context, PeerSubscription worker) where TMessage : class
        {
            if (worker == null)
            {
                _bus.MessageContext<TMessage>().RetryLater();
                return;
            }

            IBusPublishContext<TMessage> msgContext;
            if (!context.TryGetContext<TMessage>(out msgContext))
                throw new Exception("Failed to resolve context.");
            
            IEndpoint endpoint = _bus.GetEndpoint(worker.EndpointUri);

            endpoint.Send(msgContext.Message, sendContext =>
            {
                sendContext.SetNetwork(context.Network);
                sendContext.SetRequestId(context.RequestId);
                sendContext.SendResponseTo(context.ResponseAddress);
            });
        }

		public bool Inspect(IPipelineInspector inspector)
		{
			return inspector.Inspect(_defaultSink);
		}

		public void Start(IServiceBus bus)
		{
			_bus = bus;

			//_unsubscribeAction = bus.SubscribeHandler<GroupWorkerAvailable<TMessage>>(Consume);
            _unsubscribeAction = _subscriptionBus.SubscribeInstance(this);
			
			// HACK: I don't like relying on this cast, but it is the only way to accomplish
			// the replacement of the existing MessageRouter sink. Hopefully the API add a way
			// to do this 'officially' in the future.
			_defaultSink = ((OutboundMessagePipeline)bus.OutboundPipeline).ReplaceOutputSink(this);
		}
		
		public void Stop()
		{
            lock (_workers)
                _workers.Clear();

			_unsubscribeAction();
		}

        public void Consume(IConsumeContext<AddPeerSubscription> message)
        {
            // Ignore ourselves
            if (message.SourceAddress == _bus.Endpoint.Address.Uri)
                return;

            lock (_workers)
            {
                _workers.Retrieve(message.SourceAddress, () => message.Message);
            }
        }

        public void Consume(IConsumeContext<RemovePeerSubscription> message)
        {
            lock (_workers)
            {
                _workers.Remove(message.SourceAddress);
            }
        }
        		
		public void Dispose()
		{
		}
    }

}
