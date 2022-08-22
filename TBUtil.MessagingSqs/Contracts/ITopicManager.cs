namespace TBUtil.MessagingSqs.Contracts;

/// <summary>
/// Represents a class to manage SNS topics
/// </summary>
public interface ITopicManager
{
    /// <summary>
    /// Delete a SNS topic
    /// </summary>
    /// <param name="topicName"></param>
    /// <returns></returns>
    Task<bool> DeleteTopicAsync(string topicName);
}
