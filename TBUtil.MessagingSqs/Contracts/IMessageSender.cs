namespace TBUtil.MessagingSqs.Contracts;

/// <summary>
/// Represents a class to send messages
/// </summary>
public interface IMessageSender : IDisposable
{
    /// <summary>
    /// Broadcast a message to a SNS topic
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="snsTopic"></param>
    /// <param name="key"></param>
    /// <param name="message"></param>
    /// <param name="customAttributes"></param>
    /// <param name="metaKey"></param>
    /// <returns></returns>
    Task<bool> BroadcastAsync<T>(string snsTopic, string key, T message, Dictionary<string, string> customAttributes = null, string metaKey = null) where T : new();
    /// <summary>
    /// Broadcast a message to a SNS topic
    /// </summary>
    /// <param name="snsTopic"></param>
    /// <param name="key"></param>
    /// <param name="broadcastString"></param>
    /// <param name="customAttributes"></param>
    /// <param name="metaKey"></param>
    /// <returns></returns>
    Task<bool> BroadcastStringAsync(string snsTopic, string key, string broadcastString, Dictionary<string, string> customAttributes = null, string metaKey = null);

    /// <summary>
    /// Add a message directly onto a queue
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="queueName"></param>
    /// <param name="message"></param>
    /// <param name="customAttributes"></param>
    /// <returns></returns>
    Task<bool> EnqueueAsync<T>(string queueName, T message, Dictionary<string, string> customAttributes = null) where T : new();
    /// <summary>
    /// Add a message directly onto a queue
    /// </summary>
    /// <param name="queueName"></param>
    /// <param name="enqueueString"></param>
    /// <param name="customAttributes"></param>
    /// <returns></returns>
    Task<bool> EnqueueStringAsync(string queueName, string enqueueString, Dictionary<string, string> customAttributes = null);

    /// <summary>
    /// Add a message directly onto a FIFO queue
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="queueName"></param>
    /// <param name="groupId"></param>
    /// <param name="MessageDeduplicationId"></param>
    /// <param name="message"></param>
    /// <param name="customAttributes"></param>
    /// <returns></returns>
    bool EnqueueFifo<T>(string queueName, string groupId, string MessageDeduplicationId, T message, Dictionary<string, string> customAttributes = null) where T : new();
    /// <summary>
    /// Add a message directly onto a FIFO queue
    /// </summary>
    /// <param name="queueName"></param>
    /// <param name="groupId"></param>
    /// <param name="MessageDeduplicationId"></param>
    /// <param name="enqueueString"></param>
    /// <param name="customAttributes"></param>
    /// <returns></returns>
    bool EnqueueStringFifo(string queueName, string groupId, string MessageDeduplicationId, string enqueueString, Dictionary<string, string> customAttributes = null);
}
