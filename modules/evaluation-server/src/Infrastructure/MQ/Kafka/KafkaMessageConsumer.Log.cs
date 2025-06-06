using Microsoft.Extensions.Logging;

namespace Infrastructure.MQ.Kafka;

partial class KafkaMessageConsumer
{
    public static partial class Log
    {
        [LoggerMessage(1, LogLevel.Warning, "No message handler for topic: {Topic}", EventName = "NoHandlerForTopic")]
        public static partial void NoHandlerForTopic(ILogger logger, string topic);

        [LoggerMessage(2, LogLevel.Error, "Exception occurred when consume message: {Message}.",
            EventName = "ErrorConsumeMessage")]
        public static partial void ErrorConsumeMessage(ILogger logger, string message, Exception exception);

        [LoggerMessage(3, LogLevel.Error, "Failed consume message: {Message}. Error: {Error}",
            EventName = "FailedConsumeMessage")]
        public static partial void FailedConsumeMessage(ILogger logger, string message, string error);

        [LoggerMessage(4, LogLevel.Error, "Exception occurred when store offset.", EventName = "ErrorStoreOffset")]
        public static partial void ErrorStoreOffset(ILogger logger, Exception ex);
    }
}