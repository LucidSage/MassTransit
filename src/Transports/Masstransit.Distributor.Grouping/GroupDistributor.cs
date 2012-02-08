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
namespace MassTransit.Distributor.Grouping
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using log4net;
	using Magnum.Extensions;
	using MassTransit.Distributor.Grouping.Messages;
	using MassTransit.Pipeline;

	class GroupDistributor<TMessage> :
		IBusService, IDisposable, IPipelineSink<ISendContext>
		where TMessage : class
	{
		readonly IWorkerSelectionStrategy<TMessage> _selectionStrategy;

		readonly IDictionary<Uri, GroupWorkerDetails> _workers = new Dictionary<Uri, GroupWorkerDetails>();

		UnsubscribeAction _unsubscribeAction = () => false;
		IServiceBus _bus;

		public GroupDistributor(IWorkerSelectionStrategy<TMessage> workerSelectionStrategy)
		{
			_selectionStrategy = workerSelectionStrategy;
		}

		public IEnumerable<Action<ISendContext>> Enumerate(ISendContext context)
		{
			yield return ctx =>
			{
				IBusPublishContext<TMessage> msgContext;
				if (ctx.TryGetContext<TMessage>(out msgContext))
					Send(msgContext.Message);
			};
		}

		public bool Inspect(IPipelineInspector inspector)
		{
			return inspector.Inspect(this);
		}

		void Send(TMessage message)
		{
			List<GroupWorkerDetails> recipients;
			lock (_workers)
			{
				// Use ToList to force Linq to evaluate the expression while _workers is locked
				recipients = _workers.Values.GroupBy(wd => wd.Group).Select(workers => (GroupWorkerDetails)_selectionStrategy.SelectWorker(workers.Cast<WorkerDetails>(), message)).ToList();
			}

			recipients.Each(wd => SendMessageToWorker(message, wd));
		}

		void SendMessageToWorker(TMessage message, GroupWorkerDetails worker)
		{
			if (worker == null)
			{
				_bus.MessageContext<TMessage>().RetryLater();
				return;
			}

			worker.Add();

			IEndpoint endpoint = _bus.GetEndpoint(worker.DataUri);
			
			endpoint.Send(message);
		}

		public void Start(IServiceBus bus)
		{
			_bus = bus;

			_unsubscribeAction = bus.SubscribeHandler<GroupWorkerAvailable<TMessage>>(Consume);

			// HACK: I don't like relying on this cast, but it is the only way to accomplish
			// the replacement of the existing MessageRouter sink. Hopefully the API add a way
			// to do this 'officially' in the future.
			((OutboundMessagePipeline)bus.OutboundPipeline).ReplaceOutputSink(this);
		}
		
		public void Stop()
		{
			lock (_workers)
				_workers.Clear();

			_unsubscribeAction();
		}
		
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

		static readonly ILog _log = LogManager.GetLogger(typeof(Distributor<TMessage>));
		
		public void Dispose()
		{
		}
	}

}
