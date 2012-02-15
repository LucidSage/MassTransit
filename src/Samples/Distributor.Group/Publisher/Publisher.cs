using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Messages;
using MassTransit;
using MassTransit.Distributor.Grouping;

namespace Publisher
{
	class Publisher
	{
		static void Main(string[] args)
		{
			using (var bus = ServiceBusFactory.New(sbc =>
			{
				sbc.UseMsmq();
				sbc.VerifyMsmqConfiguration();
				sbc.SetNetwork("mt_group_demo");
				sbc.UseMulticastSubscriptionClient();
				sbc.UseGroupDistributorFor<YourMessage>();

				sbc.ReceiveFrom("msmq://localhost/mt_group_events_pub");
			}))
			{

				ConsoleOut("Publisher Ready.");

				while (Console.ReadLine() != "x")
				{
					ConsoleOut("Message Sent.");

					bus.Publish(new YourMessage { Text = "Hi" });
				}
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
