// Copyright 2007-2011 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
	using BusConfigurators;
	using MassTransit.Tests;
	using MassTransit.Tests.Load.Messages;
	using Magnum;
	using Magnum.Extensions;
	using MassTransit.Distributor;
	using MassTransit.Transports;
	using MassTransit.Tests.TextFixtures;
	using MassTransit.Transports.Msmq.Tests.TestFixtures;
	using System.Collections.Generic;
	using MassTransit.Tests.Distributor;

	public class GroupingTestFixture :
		MulticastMsmqEndpointTestFixture
	{
		List<IServiceBus> _subs = new List<IServiceBus>();

		protected IEnumerable<IServiceBus> Subscribers { get { return _subs; } }

		protected override void ConfigureLocalBus(ServiceBusConfigurator configurator)
		{
			base.ConfigureLocalBus(configurator);

			//configurator.UseGroupDistributorFor<FirstCommand>();
		}

		protected override void ConfigureRemoteBus(ServiceBusConfigurator configurator)
		{
			base.ConfigureRemoteBus(configurator);

			//configurator.UseGroupWorker<FirstCommand>("ONE");
		}

		protected void AddSubscriber(string group, string queueName)
		{
			var bus = ServiceBusFactory.New(configurator =>
			{
				configurator.ReceiveFrom(queueName);
				configurator.UseMulticastSubscriptionClient();
				configurator.UseGroupWorker<FirstCommand>(group);
			});

			bus.SubscribeHandler<FirstCommand>(command =>
				{
					ThreadUtil.Sleep(10.Milliseconds());

					var response = new FirstResponse(command.CorrelationId);

					bus.Context().Respond(response);
				});

			_subs.Add(bus);
		}
	}
}