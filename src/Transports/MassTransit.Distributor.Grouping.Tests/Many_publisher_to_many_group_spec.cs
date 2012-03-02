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
    using Magnum;
    using Magnum.Extensions;
    using MassTransit.Tests.Messages;
    using NUnit.Framework;

    [TestFixture, Category("Integration")]
	public class Many_publisher_to_many_group_spec :
        GroupingTestFixtureWith2GroupsOf3
    {        
        [Test]
        public void Many_publishers_should_not_duplicate_messages()
        {
            const int messageCount = 10;

            for (int i = 0; i < messageCount; ++i)
            {
                LocalBus.Publish(new PingMessage());
                RemoteBus.Publish(new PingMessage());
            }

            ThreadUtil.Sleep(2.Seconds());

            Assert.AreEqual(messageCount * 2, group_one_response_count);
            Assert.AreEqual(messageCount * 2, group_two_response_count);
            Assert.AreEqual(messageCount * 2 * 2, total_response_count);
        }
	}
}