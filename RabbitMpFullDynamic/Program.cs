using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace RabbitMpFullDynamic
{
    public class Program
    {
        private static Dictionary<string, RabbitMqConsumer> ServiceConsumerMap = new Dictionary<string, RabbitMqConsumer>();

        public static void Main(string[] args)
        {
            var listOfThread = new List<Thread>();

            //run publiser
            var publisher = new RabbitMqPublisher();
            Console.WriteLine("Input message => service name:message");
            while(true) 
            {
                var inputMsg = Console.ReadLine();
                if (inputMsg.Equals("exit", StringComparison.CurrentCultureIgnoreCase)) break;
                var inputArgs = inputMsg.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (inputArgs.Length != 2 || inputArgs.Any(input => string.IsNullOrWhiteSpace(input)))
                {
                    Console.WriteLine("Input message is not in correct format!");
                    continue;
                }

                var serviceName = inputArgs[0].Trim();
                var message = inputArgs[1].Trim();
                MessagePublishEventArgs eventArgs = publisher.SentMessage(serviceName, message);
                if (!ServiceConsumerMap.ContainsKey(serviceName))
                {
                    var thread = new Thread(() => StartConsumert(eventArgs));
                    listOfThread.Add(thread);
                    thread.Start();
                }
            }

            foreach (var keyValuePair in ServiceConsumerMap)
            {
                if (keyValuePair.Value != null) keyValuePair.Value.KeepConsumer = false;
            }

            listOfThread.ForEach(t => { if (t.IsAlive) t.Abort(); });

            Console.ReadLine();
        }

        private static void StartConsumert(MessagePublishEventArgs eventArgs)
        {
            var consumer = new RabbitMqConsumer() { KeepConsumer = true };
            ServiceConsumerMap.Add(eventArgs.ServiceName, consumer);
            var thread = new Thread(() => consumer.Subscribe(eventArgs.ServiceName, eventArgs.ConsumerVirtualHostName, eventArgs.ConsumerQueueName));
            thread.Start();
        }
    }
}
