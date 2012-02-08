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
	using System.Text;
	using MassTransit.BusConfigurators;
	using MassTransit;
	using MassTransit.Advanced;
	using Magnum.Extensions;

	/// <summary>
	/// Extensions to the ServiceBusConfigurator class for setting up group routing
	/// </summary>
	public static class ExtensionsForGroupDistributor
	{
		/// <summary>
		/// Implements the publisher in a distributor-to-groups-of-workers pattern for the given message type. 
		/// </summary>
		/// <typeparam name="TMessage">The type of message to publish</typeparam>
		/// <param name="configurator">Service bus to implement the distributor</param>
		public static void UseGroupDistributorFor<TMessage>(this ServiceBusConfigurator configurator)
			   where TMessage : class
		{
			UseGroupDistributorFor<TMessage>(configurator, new GroupWorkerSelectionStrategy<TMessage>());
		}

		/// <summary>
		/// Implements the publisher in a distributor-to-groups-of-workers pattern for the given message type. 
		/// </summary>
		/// <typeparam name="TMessage">The type of message to publish</typeparam>
		/// <typeparam name="TSelectionStrategy">The <code>IWorkerSelectionStrategy</code> used to pick 
		/// which worker node to send a message</typeparam>
		/// <param name="configurator">Service bus to implement the distributor</param>
		public static void UseGroupDistributorFor<TMessage, TSelectionStrategy>(this ServiceBusConfigurator configurator)
			where TMessage : class
			where TSelectionStrategy : class, IWorkerSelectionStrategy<TMessage>, new()
		{
			UseGroupDistributorFor<TMessage>(configurator, new TSelectionStrategy());
		}

		/// <summary>
		/// Implements the publisher in a distributor-to-groups-of-workers pattern for the given message type. 
		/// </summary>
		/// <typeparam name="TMessage">The type of message to publish</typeparam>
		/// <param name="configurator">Service bus to implement the distributor</param>
		/// <param name="workerSelectionStrategy">The <code>IWorkerSelectionStrategy</code> 
		/// used to pick which worker node to send a message</param>
		public static void UseGroupDistributorFor<TMessage>(this ServiceBusConfigurator configurator,
												IWorkerSelectionStrategy<TMessage> workerSelectionStrategy)
			where TMessage : class
		{
			configurator.AddService(BusServiceLayer.Presentation, () => new GroupDistributor<TMessage>(workerSelectionStrategy));

			configurator.SetReceiveTimeout(50.Milliseconds());
		}

		/// <summary>
		/// Implements the subscriber in a distributor-to-groups-of-workers pattern for the given message type. 
		/// The group is defaulted to the name of the queue.
		/// </summary>
		/// <typeparam name="TMessage">The type of message to publish</typeparam>
		/// <param name="configurator">Service bus to implement the distributor</param>
		public static void UseGroupWorker<TMessage>(this ServiceBusConfigurator configurator)
			where TMessage : class
		{
			configurator.AddService(BusServiceLayer.Presentation, () => new GroupWorker<TMessage>(null));
		}

		/// <summary>
		/// Implements the subscriber in a distributor-to-groups-of-workers pattern for the given message type. 
		/// </summary>
		/// <typeparam name="TMessage">The type of message to publish</typeparam>
		/// <param name="configurator">Service bus to implement the distributor</param>
		/// <param name="group">The group in which this worker will act as a competing consumer</param>
		public static void UseGroupWorker<TMessage>(this ServiceBusConfigurator configurator, string group)
			where TMessage : class
		{
			configurator.AddService(BusServiceLayer.Presentation, () => new GroupWorker<TMessage>(group));
		}

		/// <summary>
		/// Implements the subscriber in a distributor-to-groups-of-workers pattern for the given message type. 
		/// </summary>
		/// <typeparam name="TMessage">The type of message to publish</typeparam>
		/// <param name="configurator">Service bus to implement the distributor</param>
		/// <param name="group">The group in which this worker will act as a competing consumer</param>
		/// <param name="inProgressLimit"></param>
		/// <param name="pendingLimit"></param>
		public static void UseGroupWorker<TMessage>(this ServiceBusConfigurator configurator, string group, int inProgressLimit, int pendingLimit)
			where TMessage : class
		{
			var settings = new WorkerSettings
			{
				InProgressLimit = inProgressLimit,
				PendingLimit = pendingLimit
			};

			configurator.AddService(BusServiceLayer.Presentation, () => new GroupWorker<TMessage>(settings, group));
		}

	}
}
