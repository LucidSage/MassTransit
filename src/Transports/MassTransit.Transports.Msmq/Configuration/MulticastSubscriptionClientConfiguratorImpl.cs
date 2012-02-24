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
namespace MassTransit.Transports.Msmq.Configuration
{
	using System;
	using System.Net;
	using Builders;
	using BusConfigurators;
	using Exceptions;
	using Magnum.Extensions;
	using Subscriptions.Coordinator;
	using Util;
    using MassTransit.Transports.Msmq.Group;

	public class MulticastSubscriptionClientConfiguratorImpl :
		MulticastSubscriptionClientConfigurator
	{
		IPEndPoint _multicastAddress;
        string _group;
        IControlBus _subscriptionBus;

        public IControlBus SubscriptionBus { get { return _subscriptionBus; } }

        public IGroupSelectionStrategy GroupSelectionStrategy { get; private set; }

		public MulticastSubscriptionClientConfiguratorImpl()
		{
			_multicastAddress = new IPEndPoint(IPAddress.Parse("235.109.116.115"), 7784);
            GroupSelectionStrategy = new RoundRobinSelectionStrategy();
		}

		public void SetMulticastAddress(string uriString)
		{
			try
			{
				var uri = new Uri(uriString.ToLowerInvariant());

				var ipEndPoint = new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port);

				_multicastAddress = ipEndPoint;
			}
			catch (UriFormatException ex)
			{
				throw new ConfigurationException("The multicast address must match the format: msmq-pgm://x.x.x.x:port/ ", ex);
			}
		}

        public void SetGroup(string group)
        {
            _group = group;
        }

        public void SetGroupSelectionStrategy(IGroupSelectionStrategy selectionStrategy)
        {
            GroupSelectionStrategy = selectionStrategy;
        }

		public SubscriptionObserver Create(IServiceBus bus, SubscriptionRouter router)
		{
			string path = bus.ControlBus.Endpoint.Address.Uri.AbsolutePath;

			Uri uri = new UriBuilder("msmq-pgm", _multicastAddress.Address.ToString(), _multicastAddress.Port, path).Uri;
			Uri clientUri = uri.AppendToPath("_subscriptions");

			var builder = new ControlBusBuilderImpl(new ServiceBusSettings
				{
					ConcurrentConsumerLimit = 1,
					ConcurrentReceiverLimit = 1,
					EndpointCache = bus.EndpointCache,
					InputAddress = clientUri,
					ReceiveTimeout = 3.Seconds(),
					Network = router.Network,
					AutoStart = true,
				});

            _subscriptionBus = builder.Build();

            var service = new MulticastSubscriptionClient(_subscriptionBus, router, _group ?? router.PeerId.ToString());

			return service;
		}
	}
}