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
        Consumes<AddPeer>.Context,
        Consumes<RemovePeer>.Context
	{
        readonly IDictionary<Uri, Peer> _workers = new Dictionary<Uri, Peer>();

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
                List<Peer> recipients;
                lock (_workers)
                {
                    // Use ToList to force Linq to evaluate the expression while _workers is locked
                    recipients = _workers
                        .Values
                        .GroupBy(wd => wd.Group)
                        .Select(workers => _selectionStrategy.Select(workers))
                        .ToList();
                }
                
                recipients.Each(wd => this.FastInvoke<GroupMessageRouter>(new[]{ Type.GetType(context.MessageType) }, "SendMessageToWorker", context, wd));
			};
		}

        [UsedImplicitly]
        void SendMessageToWorker<TMessage>(ISendContext context, Peer worker) where TMessage : class
        {
            if (worker == null)
            {
                _bus.MessageContext<TMessage>().RetryLater();
                return;
            }

            IBusPublishContext<TMessage> msgContext;
            if (!context.TryGetContext<TMessage>(out msgContext))
                throw new Exception("Failed to resolve context.");
            
            IEndpoint endpoint = _bus.GetEndpoint(worker.PeerUri);

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

        public void Consume(IConsumeContext<AddPeer> message)
        {
            lock (_workers)
            {
                _workers.Retrieve(message.SourceAddress, () => message.Message);
            }
        }

        public void Consume(IConsumeContext<RemovePeer> message)
        {
            lock (_workers)
            {
                _workers.Remove(message.SourceAddress);
            }
        }

		/*
		void Consume(GroupWorkerAvailable<TMessage> message)
		{
			GroupWorkerDetails worker;
			lock (_workers)
			{
				worker = _workers.Retrieve(message.ControlUri, () =>
				{
					return new GroupWorkerDetails
					{
						ControlUri = message.ControlUri,
						DataUri = message.DataUri,
						InProgress = message.InProgress,
						InProgressLimit = message.InProgressLimit,
						Pending = message.Pending,
						PendingLimit = message.PendingLimit,
						LastUpdate = message.Updated,
						Group = message.Group,
					};
				});
			}

			worker.UpdateInProgress(message.InProgress, message.InProgressLimit, message.Pending, message.PendingLimit,
				message.Updated);

			if (_log.IsDebugEnabled)
				_log.DebugFormat("Worker {0}: {1} in progress, {2} pending", message.DataUri, message.InProgress, message.Pending);
		}
        */
		//static readonly ILog _log = LogManager.GetLogger(typeof(Distributor<TMessage>));
		
		public void Dispose()
		{
		}
    }

}
