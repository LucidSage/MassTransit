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
	using System.Linq;
	using MassTransit.Tests.Load;
	using MassTransit.Tests.Load.Messages;
	using NUnit.Framework;

	[TestFixture, Category("Integration")]
	public class Group_distributor_spec :
		GroupingTestFixture
	{
		protected override void EstablishContext()
		{
			base.EstablishContext();

			AddSubscriber("group_one", "msmq://localhost/worker_a");
			AddSubscriber("group_one", "msmq://localhost/worker_b");
			AddSubscriber("group_one", "msmq://localhost/worker_c");
		}

		[Test]
		public void Using_the_load_generator_should_share_the_load()
		{
			var generator = new LoadGenerator<FirstCommand, FirstResponse>();

			generator.Run(RemoteBus, LocalBus.Endpoint, Subscribers, 10, x => new FirstCommand(x));
		}
	}
}