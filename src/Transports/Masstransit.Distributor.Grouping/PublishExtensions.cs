using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MassTransit.Distributor.Grouping
{
	using System;
	using Magnum.Reflection;
using MassTransit.Context;
using MassTransit.Transports;

	public static class PublishExtensions
	{
		/// <summary>
		/// Publishes a message to all subscribed consumers for the message type
		/// </summary>
		/// <typeparam name="T">The type of the message</typeparam>
		/// <param name="message">The messages to be published</param>
		/// <param name="contextCallback">The callback to perform operations on the context</param>
		public static void GroupPublish<T>(this IServiceBus bus, T message)
			where T : class
		{
			var context = ContextStorage.CreatePublishContext(message);
			context.SetSourceAddress(bus.Endpoint.Address.Uri);

			//contextCallback(context);

			int publishedCount = 0;
			foreach (var consumer in bus.OutboundPipeline.Enumerate(context))
			{
				try
				{
					consumer(context);
					publishedCount++;
				}
				catch (Exception ex)
				{
					//_log.Error(string.Format("'{0}' threw an exception publishing message '{1}'",
					//    consumer.GetType().FullName, message.GetType().FullName), ex);
				}
			}

			context.Complete();

			if (publishedCount == 0)
			{
				context.NotifyNoSubscribers();
			}

			//_eventChannel.Send(new MessagePublished
			//    {
			//        MessageType = typeof (T),
			//        ConsumerCount = publishedCount,
			//        Duration = context.Duration,
			//    });
		}
	}
}
