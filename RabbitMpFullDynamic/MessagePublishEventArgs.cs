using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMpFullDynamic
{
    public class MessagePublishEventArgs
    {
        public string ServiceName { get; set; }

        public string ConsumerVirtualHostName { get; set; }

        public string ConsumerExchangeName { get; set; }

        public string ConsumerQueueName { get; set; }
    }
}
