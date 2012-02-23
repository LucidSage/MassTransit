using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MassTransit;
using MassTransit.Distributor.Grouping;
using Messages;

namespace SubscriberOne
{
	class SubscriberOne
	{
		static void Main(string[] args)
		{
			using (var bus = ServiceBusFactory.New(sbc =>
			{
				sbc.UseMsmq();
				sbc.VerifyMsmqConfiguration();
				sbc.SetNetwork("mt_group_demo");
                sbc.UseMulticastSubscriptionClient(config =>
                    {
                        config.SetGroup("one");
                    });
				//sbc.UseGroupWorker<YourMessage>("one");

				sbc.ReceiveFrom("msmq://localhost/mt_group_events_sub_one");

			}))
			{
				bus.SubscribeHandler<YourMessage>(msg =>
					{
						ConsoleOut(msg.Text);
					});

				ConsoleOut("SubscriberOne Ready.");

				// Sit and wait
				Console.ReadLine();
			}
		}

		static Object _lock = new Object();

		private static void ConsoleOut(string inLine)
		{
			lock (_lock)
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine(inLine);
				Console.ResetColor();
			}
		}
	}
}
