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
	using Exceptions;
	using Magnum.Extensions;
	using Magnum.TestFramework;
	using NUnit.Framework;
	using MassTransit.Tests.TextFixtures;
	using MassTransit.Tests;
	using MassTransit.BusConfigurators;
	using MassTransit.Distributor.Grouping.Messages;

	[TestFixture]
	public class Publishing_a_simple_request :
		GroupingTestFixture
	{
		class PingMessage
		{
			public Guid TransactionId { get; set; }
		}

		class PongMessage
		{
			public Guid TransactionId { get; set; }
		}

		protected override void ConfigureLocalBus(ServiceBusConfigurator configurator)
		{
			base.ConfigureLocalBus(configurator);

			configurator.UseGroupDistributorFor<PingMessage>();
		}

		protected override void ConfigureRemoteBus(ServiceBusConfigurator configurator)
		{
			base.ConfigureRemoteBus(configurator);

			configurator.UseGroupWorker<PingMessage>("ONE");
		}

		[Test]
		public void Should_support_SendRequest()
		{
			var pongReceived = new FutureMessage<PongMessage>();
			var pingReceived = new FutureMessage<PingMessage>();

			RemoteBus.SubscribeContextHandler<PingMessage>(x =>
				{
					pingReceived.Set(x.Message);
					x.Respond(new PongMessage {TransactionId = x.Message.TransactionId});
				});
			//LocalBus.ShouldHaveSubscriptionFor<PingMessage>();

			var ping = new PingMessage();

			TimeSpan timeout = 8.Seconds();

			RemoteBus.Endpoint.SendRequest(ping, LocalBus, x =>
				{
					x.Handle<PongMessage>(message =>
						{
							message.TransactionId.ShouldEqual(ping.TransactionId,
								"The response correlationId did not match");
							pongReceived.Set(message);
						});

					x.SetTimeout(timeout);
				});

			pingReceived.IsAvailable(timeout).ShouldBeTrue("The ping was not received");
			pongReceived.IsAvailable(timeout).ShouldBeTrue("The pong was not received");
		}

		[Test]
		public void Should_support_PublishRequest()
		{
			var pongReceived = new FutureMessage<PongMessage>();
			var pingReceived = new FutureMessage<PingMessage>();
			var ping = new PingMessage();
			TimeSpan timeout = 8.Seconds();

			var workerAvailableReceived = new FutureMessage<GroupWorkerAvailable<PingMessage>>();

			LocalBus.SubscribeHandler<GroupWorkerAvailable<PingMessage>>(message =>
			{
				workerAvailableReceived.Set(message);
			});

			RemoteBus.SubscribeContextHandler<PingMessage>(x =>
			{
				pingReceived.Set(x.Message);
				x.Respond(new PongMessage { TransactionId = x.Message.TransactionId });
				//RemoteBus.MessageContext<PongMessage>().Respond(new PongMessage { TransactionId = x.Message.TransactionId });
			});
			//LocalBus.ShouldHaveSubscriptionFor<PingMessage>();
			
			workerAvailableReceived.IsAvailable(timeout).ShouldBeTrue("The worker did not join the group!");

			//LocalBus.SubscribeHandler<PongMessage>(response =>
			//{
			//    response.TransactionId.ShouldEqual(ping.TransactionId, "The response correlationId did not match");
			//    pongReceived.Set(response);
			//});

			//LocalBus.Publish(ping);

			LocalBus.PublishRequest(ping, callback =>
				{
					callback.Handle<PongMessage>(response =>
					{
						response.TransactionId.ShouldEqual(ping.TransactionId,
							"The response correlationId did not match");
						pongReceived.Set(response);
					});

					callback.SetTimeout(timeout);
				});
			
			pingReceived.IsAvailable(timeout).ShouldBeTrue("The ping was not received");
			pongReceived.IsAvailable(timeout).ShouldBeTrue("The pong was not received");
		}
	}
}