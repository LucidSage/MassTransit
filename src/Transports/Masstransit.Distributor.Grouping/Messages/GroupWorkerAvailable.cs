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
namespace MassTransit.Distributor.Grouping.Messages
{
	using System;
	using Magnum;
	using MassTransit.Distributor.Messages;

	/// <summary>
	/// Published by workers that can process a message of type T
	/// </summary>
	/// <typeparam name="T">The type of message that can be processed.</typeparam>
	public class GroupWorkerAvailable<T> :
		WorkerAvailable<T>
	{
		public GroupWorkerAvailable(Uri controlUri, Uri dataUri, int inProgress, int inProgressLimit, int pending, int pendingLimit, string group)
			: base(controlUri, dataUri, inProgress, inProgressLimit, pending, pendingLimit)
		{
			Group = group;
		}

		protected GroupWorkerAvailable()
		{
		}

		public string Group { get; set; }

	}
}