using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TBUtil.MessagingSqs;

/// <summary>
/// Constants to be used with SQS messaging
/// </summary>
internal static class Constants
{
    public const string DEAD_LETTER_SUFFIX = "-dl";
    public const string FIFO_QUEUE_SUFFIX = ".fifo";
    public const string ROUTING_KEY_NAME = "routingKey";
    public const string META_KEY_NAME = "metaKey";
}