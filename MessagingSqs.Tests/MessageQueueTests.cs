using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text.Json;
using TBUtil.MessagingSqs;

namespace MessagingSqs.Tests;


[TestClass]
public class MessageQueueTests
{
    private const string AccessKey = "xxx";
    private const string SecretKey = "xxx";
    private const string Region = "eu-central-1";

    [TestMethod]
    public void Broadcast_Message_And_Consume()
    {
        using MessageQueue queue = new(AccessKey, SecretKey, Region);

        Models.Dummy dummyModel = new();
        string topicName = "dev-unit-broadcast";
        string queueName = "dev-unit-broadcast-consume";
        string routingKey = "dev-test";

        queue.EnsureQueueIsSubscribedToTopic(topicName, queueName, routingKey).GetAwaiter().GetResult();

        Assert.IsTrue(queue.BroadcastAsync(snsTopic: topicName, key: routingKey, broadcastObject: dummyModel).GetAwaiter().GetResult());

        string test = queue.DequeueAsync<Models.Dummy>(queueName: queueName, topicName: topicName, routingKey, (message, key, customAttributes) => {
            string origional = JsonSerializer.Serialize(message);
            string messageVersion = JsonSerializer.Serialize(dummyModel);
            Assert.IsTrue(origional == messageVersion);
            return message.Property1 == dummyModel.Property1;
        }).GetAwaiter().GetResult();

        Assert.IsTrue(!string.IsNullOrEmpty(test));

        Assert.IsTrue(queue.DeleteQueueWithDeadLetterAsync(queueName, topicName, false).GetAwaiter().GetResult());

        Assert.IsTrue(queue.DeleteTopicAsync(topicName).GetAwaiter().GetResult());

    }

    [TestMethod]
    public void Send_And_Process_Message_To_SQS_Async()
    {
        using MessageQueue queue = new(AccessKey, SecretKey, Region);

        Models.Dummy dummyModel = new();
        string queueName = "dev-unit-send";

        Assert.IsTrue(queue.EnqueueAsync(queueName: queueName, enqueueObject: dummyModel).GetAwaiter().GetResult());

        string test = queue.DequeueAsync<Models.Dummy>(queueName: queueName, (message, key, customAttributes) => {
            string origional = JsonSerializer.Serialize(message);
            string messageVersion = JsonSerializer.Serialize(dummyModel);
            Assert.IsTrue(origional == messageVersion);
            return message.Property1 == dummyModel.Property1;
        }).GetAwaiter().GetResult();
        Assert.IsTrue(!string.IsNullOrEmpty(test));

        bool isDeleted = queue.DeleteQueueWithDeadLetterAsync(queueName, isFifo: false).GetAwaiter().GetResult();
        Assert.IsTrue(isDeleted);
    }

    [TestMethod]
    public void Send_And_Process_Message_To_SQS_fifo()
    {
        using MessageQueue queue = new(AccessKey, SecretKey, Region);
        Models.Dummy dummyModel = new();
        string queueName = "dev-unit-send-f";
        string groupId = "fifo-group";

        Assert.IsTrue(queue.EnqueueFifo(queueName: queueName, groupId: groupId, MessageDeduplicationId: Guid.NewGuid().ToString(), enqueueObject: dummyModel));

        string test = queue.DequeueFifo<Models.Dummy>(queueName: queueName, (message, key, customAttributes) => {
            string origional = JsonSerializer.Serialize(message);
            string messageVersion = JsonSerializer.Serialize(dummyModel);
            Assert.IsTrue(origional == messageVersion);
            return message.Property1 == dummyModel.Property1;
        });
        Assert.IsTrue(!string.IsNullOrEmpty(test));

        bool isDeleted = queue.DeleteQueueWithDeadLetterAsync(queueName, isFifo: true).GetAwaiter().GetResult();
        Assert.IsTrue(isDeleted);
    }

    [TestMethod]
    public void Create_And_Delete_Queue_Standard()
    {
        using MessageQueue queue = new(AccessKey, SecretKey, Region);
        string queueName = $"dev-unit-queuecrud{DateTime.UtcNow.Ticks}";
        string queueUrl = queue.CreateQueueWithDeadLetterAsync(queueName, false).GetAwaiter().GetResult();
        Assert.IsTrue(!string.IsNullOrWhiteSpace(queueUrl));
        bool isDeleted = queue.DeleteQueueWithDeadLetterAsync(queueName, isFifo: false).GetAwaiter().GetResult();
        Assert.IsTrue(isDeleted);
    }

    [TestMethod]
    public void Create_And_Delete_Queue_Fifo()
    {
        using MessageQueue queue = new(AccessKey, SecretKey, Region);
        string queueName = $"dev-unit-queuecrud-f{DateTime.UtcNow.Ticks}";
        string queueUrl = queue.CreateQueueWithDeadLetterAsync(queueName, true).GetAwaiter().GetResult();
        Assert.IsTrue(!string.IsNullOrWhiteSpace(queueUrl));
        bool isDeleted = queue.DeleteQueueWithDeadLetterAsync(queueName, isFifo: true).GetAwaiter().GetResult();
        Assert.IsTrue(isDeleted);
    }

}