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
    using Magnum.Extensions;
    using MassTransit.Subscriptions.Messages;
    using MassTransit.Util;
    using Stact;
    using Stact.Internal;

    public class QueueLengthSelectionStrategy :
        IGroupSelectionStrategy,
        Consumes<QueueLengthMessage>.All
    {
        Scheduler _scheduler;
        ScheduledOperation _scheduled;
        readonly Fiber _fiber = new PoolFiber();
        IServiceBus _queue_length_bus;
        IServiceBus _target_bus;
        Dictionary<Uri, int> _peer_queue_lengths = new Dictionary<Uri, int>();

        public void Configure(IGroupMessageRouter router)
        {
            _target_bus = router.Bus;

            _queue_length_bus = ServiceBusFactory.New(sbc =>
                    {
                        sbc.UseMsmq();
                        sbc.SetNetwork("mt_group_queue_length");
                        sbc.UseMulticastSubscriptionClient();
                        sbc.ReceiveFrom(router.Bus.Endpoint.Address.Uri.AppendToPath("_queue_length"));
                    });

            _queue_length_bus.SubscribeInstance(this);

            _scheduler = new TimerScheduler(new PoolFiber());

            IDisposable unsubscribe = null;
            unsubscribe = router.LocalSubscriptionAdded.Subscribe(sub =>
                {
                    _scheduled = _scheduler.Schedule(3.Seconds(), 3.Seconds(), _fiber, PublishQueueLength);
                    unsubscribe.Dispose();
                });

            router.RemoteSubscriptionAdded.Subscribe(sub =>
                {
                    lock (_peer_queue_lengths)
                    {
                        _peer_queue_lengths[sub.EndpointUri] = 0;
                    }
                });

            router.RemoteSubscriptionRemoved.Subscribe(sub =>
                {
                    lock (_peer_queue_lengths)
                    {
                        _peer_queue_lengths.Remove(sub.EndpointUri);
                    }
                });
        }

        public PeerSubscription Select(IGrouping<string, PeerSubscription> potentials)
        {
            lock (_peer_queue_lengths)
            {
                return potentials.Join(_peer_queue_lengths, sub => sub.EndpointUri, kv => kv.Key, (sub, kv) => new { Sub = sub, Pending = kv.Value })
                    .OrderBy(sub => sub.Pending)
                    .First()
                    .Do(sub => _peer_queue_lengths[sub.Sub.EndpointUri] = _peer_queue_lengths[sub.Sub.EndpointUri] + 1)
                    .Sub;
            }
        }

        void PublishQueueLength()
        {
            try
            {
                int pendingMessages = 0;
                MsmqEndpointManagement.Manage(_target_bus.Endpoint.Address, msmq =>
                {
                    pendingMessages = (int)msmq.Count();
                });

                _queue_length_bus.Publish(new QueueLengthMessage()
                    {
                        SourceUri = _target_bus.Endpoint.Address.Uri,
                        Pending = pendingMessages
                    });
            }
            catch
            {
            }
        }

        public void Consume(QueueLengthMessage message)
        {
            lock (_peer_queue_lengths)
            {
                _peer_queue_lengths[message.SourceUri] = message.Pending;
            }
        }
    }
}
