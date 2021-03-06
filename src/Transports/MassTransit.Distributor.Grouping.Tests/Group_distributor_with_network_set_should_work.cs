// Copyright 2007-2012 The Apache Software Foundation.
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
	using System.Diagnostics;
	using Magnum.Extensions;
	using Magnum.TestFramework;
	using MassTransit.Tests;
	using MassTransit.Tests.Messages;
	using NUnit.Framework;
	using TestFramework;
	using MassTransit.BusConfigurators;
	using MassTransit.Transports.Msmq.Tests.TestFixtures;

	[TestFixture, Integration]
	public class Group_distributor_with_network_set_should_work :
		MulticastMsmqEndpointTestFixture
	{
		private PingMessage _ping;
		private FutureMessage<PingMessage, Guid> _future;
		UnsubscribeAction _unsubscribe;

		protected override void EstablishContext()
		{
			base.EstablishContext();

			_ping = new PingMessage();
			_future = new FutureMessage<PingMessage, Guid>(_ping.CorrelationId);

			_unsubscribe = RemoteBus.SubscribeInstance(_future);

			LocalBus.ShouldHaveRemoteSubscriptionFor<PingMessage>();

			Trace.WriteLine("LocalBus");

			LocalBus.OutboundPipeline.Trace();

			Trace.WriteLine("RemoteBus");

			RemoteBus.OutboundPipeline.Trace();

			LocalBus.Publish(_ping);
		}

		protected override void TeardownContext()
		{
			_unsubscribe();

			LocalBus.ShouldNotHaveSubscriptionFor<PingMessage>();

			base.TeardownContext();
		}

		protected override void ConfigureLocalBus(BusConfigurators.ServiceBusConfigurator configurator)
		{
			base.ConfigureLocalBus(configurator);

			configurator.SetNetwork("ONE");
		}

		protected override void ConfigureRemoteBus(ServiceBusConfigurator configurator)
		{
			base.ConfigureRemoteBus(configurator);

			configurator.SetNetwork("ONE");
		}

		[Test]
		public void The_message_should_arrive()
		{
			_future.WaitUntilAvailable(10.Seconds());
		}
	}
}