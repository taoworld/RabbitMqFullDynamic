using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace RabbitMpFullDynamic
{
    public class RabbitMqPublisher
    {
        public const string Hostname = "localhost";

        private const string Login = "tao";
        private const string Password = "tao";
        private const int Port = 5673;
        private const string PublisherVhost = "publisherVhost";
        private const string ConsumerVhost = "consumerVhost";
        private static object LockInstance = new object();

        private static ConnectionFactory PubliherFactoryInstance, ConsumerFactoryInstance;
        

        public static ConnectionFactory GetFactoryInstance(bool IsForPublisher)
        {
            var targetInstance = IsForPublisher ? PubliherFactoryInstance : ConsumerFactoryInstance;

            if (targetInstance == null)
            {
                lock (LockInstance)
                {
                    if (targetInstance == null)
                    {
                        targetInstance = new ConnectionFactory()
                        {
                            HostName = Hostname,
                            UserName = Login,
                            Password = Password,
                            Port = Port,
                            VirtualHost = IsForPublisher ? PublisherVhost : ConsumerVhost
                        };
                    }
                }
            }

            return targetInstance;
        }


        public 
    }
}
