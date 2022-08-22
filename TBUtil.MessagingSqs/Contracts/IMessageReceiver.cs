namespace TBUtil.MessagingSqs.Contracts;

/// <summary>
/// Represents a class to receive messages
/// </summary>
public interface IMessageReceiver : IDisposable
{
    /// <summary>
    /// Process a message from a queue that was broadcast to a SNS topic
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="queueName"></param>
    /// <param name="topicName">SQS topic the message was sent to</param>
    /// <param name="key"></param>
    /// <param name="handler"></param>
    /// <param name="metaKey"></param>
    /// <returns></returns>
    /// 
    Task<string> DequeueAsync<T>(string queueName, string topicName, string key, ProcessMessageDelegate<T> handler, string metaKey = null);
    /// <summary>
    /// Process a message from a queue that was broadcast to a SNS topic
    /// </summary>
    /// <param name="queueName"></param>
    /// <param name="topicName">SQS topic the message was sent to</param>
    /// <param name="key"></param>
    /// <param name="handler"></param>
    /// <param name="metaKey"></param>
    /// <returns></returns>
    /// 
    Task<string> DequeueStringAsync(string queueName, string topicName, string key, ProcessMessageStringDelegate handler, string metaKey = null);
    /// <summary>
    /// Process a message from a queue that was broadcast to a SNS topic
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="queueName"></param>
    /// <param name="topicName">SQS topic the message was sent to</param>
    /// <param name="keyList"></param>
    /// <param name="handler"></param>
    /// <param name="metaKey"></param>
    /// <returns></returns>
    /// 
    Task<string> DequeueAsync<T>(string queueName, string topicName, List<string> keyList, ProcessMessageDelegateMultipleKeys<T> handler, string metaKey = null);
    /// <summary>
    /// Process a message from a queue that was broadcast to a SNS topic
    /// </summary>
    /// <param name="queueName"></param>
    /// <param name="topicName">SQS topic the message was sent to</param>
    /// <param name="keyList"></param>
    /// <param name="handler"></param>
    /// <param name="metaKey"></param>
    /// <returns></returns>
    /// 
    Task<string> DequeueStringAsync(string queueName, string topicName, List<string> keyList, ProcessMessageStringDelegateMultipleKeys handler, string metaKey = null);

    /// <summary>
    /// Process a message from s queue
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="queueName"></param>
    /// <param name="handler"></param>
    /// <returns></returns>
    Task<string> DequeueAsync<T>(string queueName, ProcessMessageDelegate<T> handler);
    /// <summary>
    /// Process a message from s queue
    /// </summary>
    /// <param name="queueName"></param>
    /// <param name="handler"></param>
    /// <returns></returns>
    Task<string> DequeueStringAsync(string queueName, ProcessMessageStringDelegate handler);

    /// <summary>
    /// Process a message from a FIFO queue
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="queueName"></param>
    /// <param name="handler"></param>
    /// <returns></returns>
    string DequeueFifo<T>(string queueName, ProcessMessageDelegate<T> handler);
    /// <summary>
    /// Process a message from a FIFO queue
    /// </summary>
    /// <param name="queueName"></param>
    /// <param name="handler"></param>
    /// <returns></returns>
    string DequeueStringFifo(string queueName, ProcessMessageStringDelegate handler);

}