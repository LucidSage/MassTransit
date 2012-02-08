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

	/// <summary>
	/// The default selection stategy to pick a single worker from a group
	/// </summary>
	/// <typeparam name="TMessage"></typeparam>
	public class GroupWorkerSelectionStrategy<TMessage> :
		IWorkerSelectionStrategy<TMessage>
		where TMessage : class
	{
		/// <summary>
		/// Check to see if there are any workers ready for the given message
		/// </summary>
		/// <param name="candidates">The set of workers to check</param>
		/// <param name="message"></param>
		/// <returns></returns>
		public bool HasAvailableWorker(IEnumerable<WorkerDetails> candidates, TMessage message)
		{
			return candidates.Any();
		}

		/// <summary>
		/// Selects a single worker from the given list for the given message
		/// </summary>
		/// <param name="candidates">The set of workers to choose from</param>
		/// <param name="message"></param>
		/// <returns></returns>
		public WorkerDetails SelectWorker(IEnumerable<WorkerDetails> candidates, TMessage message)
		{
			return candidates
				.OrderBy(x => x.InProgress + x.Pending)
				.ThenByDescending(x => x.LastUpdate)
				.FirstOrDefault();
		}
	}
}
