// Copyright 2007-20012 The Apache Software Foundation.
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
namespace MassTransit.Distributor.Grouping.Tests
{
    using System;
    using System.Threading;
    using MassTransit.BusConfigurators;
    using MassTransit.Distributor.Grouping.Messages;
    using MassTransit.Tests.Messages;
    using MassTransit.Transports.Msmq.Tests.TestFixtures;

	public class GroupingTestFixtureWith2GroupsOf3 :
        MulticastMsmqEndpointTestFixture
    {
        int workerCount = 0;
        protected int group_one_response_count = 0;
        protected int group_two_response_count = 0;
        protected int total_response_count = 0;

        private readonly ManualResetEvent _workersReady = new ManualResetEvent(false);
        
        protected override void ConfigureLocalBus(ServiceBusConfigurator configurator)
        {
            base.ConfigureLocalBus(configurator);

            configurator.UseGroupDistributorFor<PingMessage>();
        }

        protected override void ConfigureRemoteBus(ServiceBusConfigurator configurator)
        {
            base.ConfigureRemoteBus(configurator);

            configurator.UseGroupDistributorFor<PingMessage>();
        }

		protected override void EstablishContext()
		{
            base.EstablishContext();

            LocalBus.SubscribeContextHandler<GroupWorkerAvailable<PingMessage>>(context =>
            {
                lock (this)
                {
                    ++workerCount;
                    if (workerCount >= 6)
                        _workersReady.Set();
                }
            });

            Action<PingMessage> group_one_action = m =>
            {
                Interlocked.Increment(ref group_one_response_count);
                Interlocked.Increment(ref total_response_count);
            };

            AddSubscriber("group_one", "msmq://localhost/worker_a", group_one_action);
            AddSubscriber("group_one", "msmq://localhost/worker_b", group_one_action);
            AddSubscriber("group_one", "msmq://localhost/worker_c", group_one_action);


            Action<PingMessage> group_two_action = m =>
            {
                Interlocked.Increment(ref group_two_response_count);
                Interlocked.Increment(ref total_response_count);
            };

            AddSubscriber("group_two", "msmq://localhost/worker_d", group_two_action);
            AddSubscriber("group_two", "msmq://localhost/worker_e", group_two_action);
            AddSubscriber("group_two", "msmq://localhost/worker_f", group_two_action);

            _workersReady.WaitOne();
		}

        void AddSubscriber(string group, string queueName, Action<PingMessage> messageAction)
        {
            var bus = ServiceBusFactory.New(configurator =>
            {
                configurator.ReceiveFrom(queueName);
                configurator.UseMulticastSubscriptionClient();
                configurator.UseGroupWorker<PingMessage>(group);
            });

            bus.SubscribeHandler<PingMessage>(command =>
            {
                messageAction(command);
            });
        }
	}
}