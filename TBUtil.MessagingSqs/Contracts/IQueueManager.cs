namespace TBUtil.MessagingSqs.Contracts;

/// <summary>
/// Represents a class to manage queues
/// </summary>
public interface IQueueManager
{
    /// <summary>
    /// Creates a SQS queue as well as a corrisponding deadletter queue
    /// </summary>
    /// <param name="queueName"></param>
    /// <param name="isFifo"></param>
    /// <returns></returns>
    Task<string> CreateQueueWithDeadLetterAsync(string queueName, bool isFifo = false);

    /// <summary>
    /// Deletes a SQS queue as well as a corrisponding deadletter queue
    /// </summary>
    /// <param name="queueName"></param>
    /// <param name="topicName"></param>
    /// <param name="isFifo"></param>
    /// <returns></returns>
    Task<bool> DeleteQueueWithDeadLetterAsync(string queueName, string topicName, bool isFifo = false);

    /// <summary>
    /// Ensures that a SQS queue is subscribed to a SNS topic using a fitler jey
    /// </summary>
    /// <param name="topicName"></param>
    /// <param name="queueName"></param>
    /// <param name="filterKey"></param>
    /// <returns></returns>
    Task EnsureQueueIsSubscribedToTopic(string topicName, string queueName, string filterKey);
}
