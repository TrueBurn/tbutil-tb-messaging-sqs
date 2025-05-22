using Amazon.SimpleNotificationService.Model;
using Amazon.SimpleNotificationService;
using Amazon.SQS.Model;
using Amazon.SQS;
using System.Text.Json;
using TBUtil.MessagingSqs.Contracts;
using SnsMessage = Amazon.SimpleNotificationService.Util.Message;

namespace TBUtil.MessagingSqs;

/// <summary>
/// Default implementation of message queuing specific to the UNi4 Online environment.
/// This implementation include dead-letter queues, topic broadcasting, single message handling.
/// </summary>
public sealed class MessageQueue : IMessageSender, IMessageReceiver, IQueueManager, ITopicManager, IDisposable
{

    private readonly int WaitTimeSeconds = 10;

    private readonly IAmazonSQS _amazonSQSClient; 
    private readonly IAmazonSimpleNotificationService _amazonSNSClient; 

    private readonly Dictionary<string, bool> queueSubscriptions = new();

    /// <summary>
    /// Default constructor.
    /// </summary>
    public MessageQueue() : this(new AmazonSQSClient(), new AmazonSimpleNotificationServiceClient()) 
    {
    }

    /// <summary>
    /// Message queue setup
    /// </summary>
    /// <param name="accessKey">AWS access key</param>
    /// <param name="secretKey">AWS secret key</param>
    /// <param name="region">AWS region</param>
    /// <param name="waitTimeinSeconds"></param>
    public MessageQueue(string accessKey, string secretKey, string region, int waitTimeinSeconds = 10)
        : this(new AmazonSQSClient(accessKey, secretKey, Amazon.RegionEndpoint.GetBySystemName(region)), 
               new AmazonSimpleNotificationServiceClient(accessKey, secretKey, Amazon.RegionEndpoint.GetBySystemName(region)), 
               waitTimeinSeconds)
    {
    }

    /// <summary>
    /// Message queue setup with injected AWS clients
    /// </summary>
    /// <param name="amazonSQSClient">SQS client</param>
    /// <param name="amazonSNSClient">SNS client</param>
    /// <param name="waitTimeinSeconds"></param>
    public MessageQueue(IAmazonSQS amazonSQSClient, IAmazonSimpleNotificationService amazonSNSClient, int waitTimeinSeconds = 10) 
    {
        _amazonSQSClient = amazonSQSClient ?? throw new ArgumentNullException(nameof(amazonSQSClient));
        _amazonSNSClient = amazonSNSClient ?? throw new ArgumentNullException(nameof(amazonSNSClient));
        WaitTimeSeconds = waitTimeinSeconds;
    }

    /// <summary>
    /// Dequeue a message and pass it to a delegate
    /// </summary>
    public async Task<string> DequeueAsync<T>(string queueName, string topicName, string key, ProcessMessageDelegate<T> handler, string metaKey = null)
    {
        handler = handler ?? throw new ArgumentNullException(nameof(handler));
        List<string> messageIds = new();
        try
        {
            if (handler is not null)
            {
                await InternalEnsureQueueIsSubscribedToTopic(topicName, queueName, key, metaKey);
                string queueUrl = await GetQueueUrl(queueName, isFifo: false);
                if (string.IsNullOrWhiteSpace(queueName)) return null;
                ReceiveMessageRequest receiveMessageRequest = new() { QueueUrl = queueUrl, WaitTimeSeconds = WaitTimeSeconds, MessageAttributeNames = new List<string> { "All" } };
                ReceiveMessageResponse receiveMessageResponse = await _amazonSQSClient.ReceiveMessageAsync(receiveMessageRequest); 
                receiveMessageResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
                foreach (Message receivedMessage in receiveMessageResponse.Messages)
                {
                    messageIds.Add(receivedMessage.MessageId);
                    try
                    {
                        (SnsMessage snsMessage, MessageAttributesWrapper attributesWrapper) = receivedMessage.Body.ParseMessage(true);
                        if (InternalProcessMessage(JsonSerializer.Deserialize<T>(snsMessage.MessageText), topicName, key, MapToStringDictionary(attributesWrapper.MessageAttributes), handler))
                            await AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, false);
                        else
                            await AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, true);
                    }
                    catch (Exception) { await AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, true); throw; }
                }
            }
            return string.Join(",", messageIds);
        }
        catch (Exception) { throw; }
    }

    public async Task<string> DequeueStringAsync(string queueName, string topicName, string key, ProcessMessageStringDelegate handler, string metaKey = null)
    {
        handler = handler ?? throw new ArgumentNullException(nameof(handler));
        List<string> messageIds = new();
        try
        {
            if (handler is not null)
            {
                await InternalEnsureQueueIsSubscribedToTopic(topicName, queueName, key, metaKey);
                string queueUrl = await GetQueueUrl(queueName, isFifo: false);
                if (string.IsNullOrWhiteSpace(queueName)) return null;
                ReceiveMessageRequest receiveMessageRequest = new() { QueueUrl = queueUrl, WaitTimeSeconds = WaitTimeSeconds, MessageAttributeNames = new List<string> { "All" } };
                ReceiveMessageResponse receiveMessageResponse = await _amazonSQSClient.ReceiveMessageAsync(receiveMessageRequest); // ITEM 1 Corrected
                receiveMessageResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
                foreach (Message receivedMessage in receiveMessageResponse.Messages)
                {
                    messageIds.Add(receivedMessage.MessageId);
                    try
                    {
                        (SnsMessage snsMessage, MessageAttributesWrapper attributesWrapper) = receivedMessage.Body.ParseMessage(true);
                        if (InternalProcessMessage(snsMessage.MessageText.Base64Decode(), topicName, key, MapToStringDictionary(attributesWrapper.MessageAttributes), handler))
                            await AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, false);
                        else
                            await AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, true);
                    }
                    catch (Exception) { await AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, true); throw; }
                }
            }
            return string.Join(",", messageIds);
        }
        catch (Exception) { throw; }
    }

    public async Task<string> DequeueAsync<T>(string queueName, string topicName, List<string> keyList, ProcessMessageDelegateMultipleKeys<T> handler, string metaKey = null)
    {
        handler = handler ?? throw new ArgumentNullException(nameof(handler));
        List<string> messageIds = new();
        try
        {
            if (handler is not null)
            {
                await InternalEnsureQueueIsSubscribedToTopic(topicName, queueName, keyList, metaKey);
                string queueUrl = await GetQueueUrl(queueName, isFifo: false);
                if (string.IsNullOrWhiteSpace(queueName)) return null;
                ReceiveMessageRequest receiveMessageRequest = new() { QueueUrl = queueUrl, WaitTimeSeconds = WaitTimeSeconds, MessageAttributeNames = new List<string> { "All" } };
                ReceiveMessageResponse receiveMessageResponse = await _amazonSQSClient.ReceiveMessageAsync(receiveMessageRequest); // ITEM 2 Corrected
                receiveMessageResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
                foreach (Message receivedMessage in receiveMessageResponse.Messages)
                {
                    messageIds.Add(receivedMessage.MessageId);
                    try
                    {
                        (SnsMessage snsMessage, MessageAttributesWrapper attributesWrapper) = receivedMessage.Body.ParseMessage(true);
                        if (InternalProcessMessageMultipleKeys(JsonSerializer.Deserialize<T>(snsMessage.MessageText), topicName, keyList, MapToStringDictionary(attributesWrapper.MessageAttributes), handler))
                            await AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, false);
                        else
                            await AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, true);
                    }
                    catch (Exception) { await AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, true); throw; }
                }
            }
            return string.Join(",", messageIds);
        }
        catch (Exception) { throw; }
    }

    public async Task<string> DequeueStringAsync(string queueName, string topicName, List<string> keyList, ProcessMessageStringDelegateMultipleKeys handler, string metaKey = null)
    {
        handler = handler ?? throw new ArgumentNullException(nameof(handler));
        List<string> messageIds = new();
        try
        {
            if (handler is not null)
            {
                await InternalEnsureQueueIsSubscribedToTopic(topicName, queueName, keyList, metaKey);
                string queueUrl = await GetQueueUrl(queueName, isFifo: false);
                if (string.IsNullOrWhiteSpace(queueName)) return null;
                ReceiveMessageRequest receiveMessageRequest = new() { QueueUrl = queueUrl, WaitTimeSeconds = WaitTimeSeconds, MessageAttributeNames = new List<string> { "All" } };
                ReceiveMessageResponse receiveMessageResponse = await _amazonSQSClient.ReceiveMessageAsync(receiveMessageRequest); // ITEM 3 Corrected
                receiveMessageResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
                foreach (Message receivedMessage in receiveMessageResponse.Messages)
                {
                    messageIds.Add(receivedMessage.MessageId);
                    try
                    {
                        (SnsMessage snsMessage, MessageAttributesWrapper attributesWrapper) = receivedMessage.Body.ParseMessage(true);
                        if (InternalProcessMessageMultipleKeys(snsMessage.MessageText.Base64Decode(), topicName, keyList, MapToStringDictionary(attributesWrapper.MessageAttributes), handler))
                            await AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, false);
                        else
                            await AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, true);
                    }
                    catch (Exception) { await AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, true); throw; }
                }
            }
            return string.Join(",", messageIds);
        }
        catch (Exception) { throw; }
    }

    public async Task<string> DequeueAsync<T>(string queueName, ProcessMessageDelegate<T> handler)
    {
        handler = handler ?? throw new ArgumentNullException(nameof(handler));
        List<string> messageIds = new();
        try
        {
            if (handler is not null)
            {
                string queueUrl = await GetQueueUrl(queueName, isFifo: false);
                if (string.IsNullOrWhiteSpace(queueName)) return null;
                ReceiveMessageRequest receiveMessageRequest = new() { QueueUrl = queueUrl, WaitTimeSeconds = WaitTimeSeconds, MessageAttributeNames = new List<string> { "All" } };
                ReceiveMessageResponse receiveMessageResponse = await _amazonSQSClient.ReceiveMessageAsync(receiveMessageRequest); // ITEM 4 Corrected
                receiveMessageResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
                foreach (Message receivedMessage in receiveMessageResponse.Messages)
                {
                    (SnsMessage _, MessageAttributesWrapper attributesWrapper) = receivedMessage.Body.ParseMessage();
                    messageIds.Add(receivedMessage.MessageId);
                    try
                    {
                        if (InternalProcessMessage(JsonSerializer.Deserialize<T>(receivedMessage.Body), null, null, MapToStringDictionary(attributesWrapper.MessageAttributes), handler))
                            await AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, false);
                        else
                            await AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, true);
                    }
                    catch (Exception) { await AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, true); throw; }
                }
            }
            return string.Join(",", messageIds);
        }
        catch (Exception) { throw; }
    }

    public async Task<string> DequeueStringAsync(string queueName, ProcessMessageStringDelegate handler)
    {
        handler = handler ?? throw new ArgumentNullException(nameof(handler));
        List<string> messageIds = new();
        try
        {
            if (handler is not null)
            {
                string queueUrl = await GetQueueUrl(queueName, isFifo: false);
                if (string.IsNullOrWhiteSpace(queueName)) return null;
                ReceiveMessageRequest receiveMessageRequest = new() { QueueUrl = queueUrl, WaitTimeSeconds = WaitTimeSeconds, MessageAttributeNames = new List<string> { "All" } };
                ReceiveMessageResponse receiveMessageResponse = await _amazonSQSClient.ReceiveMessageAsync(receiveMessageRequest); // ITEM 5 Corrected
                receiveMessageResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
                foreach (Message receivedMessage in receiveMessageResponse.Messages)
                {
                    (SnsMessage _, MessageAttributesWrapper attributesWrapper) = receivedMessage.Body.ParseMessage();
                    messageIds.Add(receivedMessage.MessageId);
                    try
                    {
                        if (InternalProcessMessage(receivedMessage.Body.Base64Decode(), null, null, MapToStringDictionary(attributesWrapper.MessageAttributes), handler))
                            await AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, false);
                        else
                            await AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, true);
                    }
                    catch (Exception) { await AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, true); throw; }
                }
            }
            return string.Join(",", messageIds);
        }
        catch (Exception) { throw; }
    }

    public string DequeueFifo<T>(string queueName, ProcessMessageDelegate<T> handler)
    {
        handler = handler ?? throw new ArgumentNullException(nameof(handler));
        List<string> messageIds = new();
        try
        {
            if (handler is not null)
            {
                string queueUrl = GetQueueUrl(queueName, isFifo: true).GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(queueName)) return null;
                ReceiveMessageRequest receiveMessageRequest = new() { QueueUrl = queueUrl, MessageAttributeNames = new List<string> { "All" } };
                ReceiveMessageResponse receiveMessageResponse = _amazonSQSClient.ReceiveMessageAsync(receiveMessageRequest).GetAwaiter().GetResult(); 
                receiveMessageResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
                foreach (Message receivedMessage in receiveMessageResponse.Messages)
                {
                    (SnsMessage _, MessageAttributesWrapper attributesWrapper) = receivedMessage.Body.ParseMessage();
                    messageIds.Add(receivedMessage.MessageId);
                    try
                    {
                        if (InternalProcessMessage(JsonSerializer.Deserialize<T>(receivedMessage.Body), null, null, MapToStringDictionary(attributesWrapper.MessageAttributes), handler))
                            AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, false).GetAwaiter().GetResult();
                        else
                            AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, true).GetAwaiter().GetResult();
                    }
                    catch (Exception) { AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, true).GetAwaiter().GetResult(); throw; }
                }
            }
            return string.Join(",", messageIds);
        }
        catch (Exception) { throw; }
    }

    public string DequeueStringFifo(string queueName, ProcessMessageStringDelegate handler)
    {
        handler = handler ?? throw new ArgumentNullException(nameof(handler));
        List<string> messageIds = new();
        try
        {
            if (handler is not null)
            {
                string queueUrl = GetQueueUrl(queueName, isFifo: true).GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(queueName)) return null;
                ReceiveMessageRequest receiveMessageRequest = new() { QueueUrl = queueUrl, MessageAttributeNames = new List<string> { "All" } };
                ReceiveMessageResponse receiveMessageResponse = _amazonSQSClient.ReceiveMessageAsync(receiveMessageRequest).GetAwaiter().GetResult(); 
                receiveMessageResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
                foreach (Message receivedMessage in receiveMessageResponse.Messages)
                {
                    (SnsMessage _, MessageAttributesWrapper attributesWrapper) = receivedMessage.Body.ParseMessage();
                    messageIds.Add(receivedMessage.MessageId);
                    try
                    {
                        if (InternalProcessMessage(receivedMessage.Body.Base64Decode(), null, null, MapToStringDictionary(attributesWrapper.MessageAttributes), handler))
                            AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, false).GetAwaiter().GetResult();
                        else
                            AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, true).GetAwaiter().GetResult();
                    }
                    catch (Exception) { AcknowledgeMessage(queueUrl, receivedMessage.ReceiptHandle, true).GetAwaiter().GetResult(); throw; }
                }
            }
            return string.Join(",", messageIds);
        }
        catch (Exception) { throw; }
    }

    public async Task<bool> BroadcastAsync<T>(string snsTopic, string key, T broadcastObject, Dictionary<string, string> customAttributes = null, string metaKey = null) where T : new()
    {
        try
        {
            string topicArn = await GetTopicArn(snsTopic);
            PublishRequest publishRequest = new(topicArn, JsonSerializer.Serialize(broadcastObject));
            Dictionary<string, Amazon.SimpleNotificationService.Model.MessageAttributeValue> messageAttributes = new() { { Constants.ROUTING_KEY_NAME, new Amazon.SimpleNotificationService.Model.MessageAttributeValue() { DataType = "String", StringValue = key } } };
            if (!string.IsNullOrWhiteSpace(metaKey)) messageAttributes.Add(Constants.META_KEY_NAME, new Amazon.SimpleNotificationService.Model.MessageAttributeValue() { DataType = "String", StringValue = metaKey });
            if (customAttributes is not null && customAttributes.Any()) foreach (KeyValuePair<string, string> attribute in customAttributes) { if (string.IsNullOrWhiteSpace(attribute.Key) || string.IsNullOrWhiteSpace(attribute.Value)) continue; messageAttributes.Add(attribute.Key.SanatiseAttributeString(), new Amazon.SimpleNotificationService.Model.MessageAttributeValue { DataType = "String", StringValue = attribute.Value.SanatiseAttributeString() }); }
            if (messageAttributes.Any()) publishRequest.MessageAttributes = messageAttributes;
            PublishResponse publishResponse = await _amazonSNSClient.PublishAsync(publishRequest); 
            publishResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
            return true;
        }
        catch (Exception) { return false; }
    }

    public async Task<bool> BroadcastStringAsync(string snsTopic, string key, string broadcastString, Dictionary<string, string> customAttributes = null, string metaKey = null)
    {
        try
        {
            string topicArn = await GetTopicArn(snsTopic);
            PublishRequest publishRequest = new(topicArn, broadcastString.Base64Encode());
            Dictionary<string, Amazon.SimpleNotificationService.Model.MessageAttributeValue> messageAttributes = new() { { Constants.ROUTING_KEY_NAME, new Amazon.SimpleNotificationService.Model.MessageAttributeValue() { DataType = "String", StringValue = key } } };
            if (!string.IsNullOrWhiteSpace(metaKey)) messageAttributes.Add(Constants.META_KEY_NAME, new Amazon.SimpleNotificationService.Model.MessageAttributeValue() { DataType = "String", StringValue = metaKey });
            if (customAttributes is not null && customAttributes.Any()) foreach (KeyValuePair<string, string> attribute in customAttributes) { if (string.IsNullOrWhiteSpace(attribute.Key) || string.IsNullOrWhiteSpace(attribute.Value)) continue; messageAttributes.Add(attribute.Key.SanatiseAttributeString(), new Amazon.SimpleNotificationService.Model.MessageAttributeValue { DataType = "String", StringValue = attribute.Value.SanatiseAttributeString() }); }
            if (messageAttributes.Any()) publishRequest.MessageAttributes = messageAttributes;
            PublishResponse publishResponse = await _amazonSNSClient.PublishAsync(publishRequest); // ITEM 8 Corrected
            publishResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
            return true;
        }
        catch (Exception) { return false; }
    }

    public async Task<bool> EnqueueAsync<T>(string queueName, T enqueueObject, Dictionary<string, string> customAttributes = null) where T : new()
    {
        try
        {
            string queueUrl = await GetQueueUrl(queueName, isFifo: false);
            if (string.IsNullOrWhiteSpace(queueName)) return false;
            SendMessageRequest sqsRequest = new() { QueueUrl = queueUrl, MessageBody = JsonSerializer.Serialize(enqueueObject) };
            if (customAttributes is not null && customAttributes.Any())
            {
                Dictionary<string, Amazon.SQS.Model.MessageAttributeValue> messageAttributes = new();
                foreach (KeyValuePair<string, string> attribute in customAttributes) { if (string.IsNullOrWhiteSpace(attribute.Key) || string.IsNullOrWhiteSpace(attribute.Value)) continue; messageAttributes.Add(attribute.Key.SanatiseAttributeString(), new Amazon.SQS.Model.MessageAttributeValue { DataType = "String", StringValue = attribute.Value.SanatiseAttributeString() }); }
                if (messageAttributes.Any()) sqsRequest.MessageAttributes = messageAttributes;
            }
            SendMessageResponse sqsResponse = await _amazonSQSClient.SendMessageAsync(sqsRequest); 
            sqsResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
            return !string.IsNullOrWhiteSpace(sqsResponse.MessageId);
        }
        catch (Exception) { return false; }
    }

    public async Task<bool> EnqueueStringAsync(string queueName, string enqueueString, Dictionary<string, string> customAttributes = null)
    {
        try
        {
            string queueUrl = await GetQueueUrl(queueName, isFifo: false);
            if (string.IsNullOrWhiteSpace(queueName)) return false;
            SendMessageRequest sqsRequest = new() { QueueUrl = queueUrl, MessageBody = enqueueString.Base64Encode() };
            if (customAttributes is not null && customAttributes.Any())
            {
                Dictionary<string, Amazon.SQS.Model.MessageAttributeValue> messageAttributes = new();
                foreach (KeyValuePair<string, string> attribute in customAttributes) { if (string.IsNullOrWhiteSpace(attribute.Key) || string.IsNullOrWhiteSpace(attribute.Value)) continue; messageAttributes.Add(attribute.Key.SanatiseAttributeString(), new Amazon.SQS.Model.MessageAttributeValue { DataType = "String", StringValue = attribute.Value.SanatiseAttributeString() }); }
                if (messageAttributes.Any()) sqsRequest.MessageAttributes = messageAttributes;
            }
            SendMessageResponse sqsResponse = await _amazonSQSClient.SendMessageAsync(sqsRequest); // ITEM 6 Corrected
            sqsResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
            return !string.IsNullOrWhiteSpace(sqsResponse.MessageId);
        }
        catch (Exception) { return false; }
    }

    public bool EnqueueFifo<T>(string queueName, string groupId, string MessageDeduplicationId, T enqueueObject, Dictionary<string, string> customAttributes = null) where T : new()
    {
        try
        {
            string queueUrl = GetQueueUrl(queueName, isFifo: true).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(queueName)) return false;
            SendMessageRequest sqsRequest = new() { QueueUrl = queueUrl, MessageBody = JsonSerializer.Serialize(enqueueObject), MessageGroupId = groupId, MessageDeduplicationId = MessageDeduplicationId };
            if (customAttributes is not null && customAttributes.Any())
            {
                Dictionary<string, Amazon.SQS.Model.MessageAttributeValue> messageAttributes = new();
                foreach (KeyValuePair<string, string> attribute in customAttributes) { if (string.IsNullOrWhiteSpace(attribute.Key) || string.IsNullOrWhiteSpace(attribute.Value)) continue; messageAttributes.Add(attribute.Key.SanatiseAttributeString(), new Amazon.SQS.Model.MessageAttributeValue { DataType = "String", StringValue = attribute.Value.SanatiseAttributeString() }); }
                if (messageAttributes.Any()) sqsRequest.MessageAttributes = messageAttributes;
            }
            SendMessageResponse sqsResponse = _amazonSQSClient.SendMessageAsync(sqsRequest).GetAwaiter().GetResult(); 
            sqsResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
            return !string.IsNullOrWhiteSpace(sqsResponse.MessageId);
        }
        catch (Exception) { return false; }
    }

    public bool EnqueueStringFifo(string queueName, string groupId, string MessageDeduplicationId, string enqueueString, Dictionary<string, string> customAttributes = null)
    {
        try
        {
            string queueUrl = GetQueueUrl(queueName, isFifo: true).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(queueName)) return false;
            SendMessageRequest sqsRequest = new() { QueueUrl = queueUrl, MessageBody = enqueueString.Base64Encode(), MessageGroupId = groupId, MessageDeduplicationId = MessageDeduplicationId };
            if (customAttributes is not null && customAttributes.Any())
            {
                Dictionary<string, Amazon.SQS.Model.MessageAttributeValue> messageAttributes = new();
                foreach (KeyValuePair<string, string> attribute in customAttributes) { if (string.IsNullOrWhiteSpace(attribute.Key) || string.IsNullOrWhiteSpace(attribute.Value)) continue; messageAttributes.Add(attribute.Key.SanatiseAttributeString(), new Amazon.SQS.Model.MessageAttributeValue { DataType = "String", StringValue = attribute.Value.SanatiseAttributeString() }); }
                if (messageAttributes.Any()) sqsRequest.MessageAttributes = messageAttributes;
            }
            SendMessageResponse sqsResponse = _amazonSQSClient.SendMessageAsync(sqsRequest).GetAwaiter().GetResult(); // ITEM 7 Corrected
            sqsResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
            return !string.IsNullOrWhiteSpace(sqsResponse.MessageId);
        }
        catch (Exception) { return false; }
    }

    public Task<string> CreateQueueWithDeadLetterAsync(string queueName, bool isFifo)
    {
        return InternalCreateQueueWithDeadLetterAsync(queueName, isFifo);
    }

    public Task<bool> DeleteQueueWithDeadLetterAsync(string queueName, string topicName = null, bool isFifo = false)
    {
        return InternalDeleteQueueWithDeadLetterAsync(queueName, topicName, isFifo);
    }

    public Task EnsureQueueIsSubscribedToTopic(string topicName, string queueName, string filterKey)
    {
        return InternalEnsureQueueIsSubscribedToTopic(topicName, queueName, filterKey);
    }

    public Task<bool> DeleteTopicAsync(string topicName)
    {
        return InternalDeleteTopicAsync(topicName);
    }

    public void Dispose()
    {
        _amazonSQSClient?.Dispose(); 
        _amazonSNSClient?.Dispose(); 
    }

    #region Private

    private async Task<string> GetTopicArn(string topicName)
    {
        try
        {
            string topicArn = (await _amazonSNSClient.FindTopicAsync(topicName))?.TopicArn; 
            if (string.IsNullOrWhiteSpace(topicArn))
            {
                CreateTopicResponse createTopicResponse = await _amazonSNSClient.CreateTopicAsync(new CreateTopicRequest(topicName)); 
                createTopicResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
                topicArn = createTopicResponse.TopicArn;
            }
            return topicArn;
        }
        catch (Exception) { return string.Empty; }
    }

    private async Task<string> InternalCreateQueueWithDeadLetterAsync(string queueName, bool isFifo = false)
    {
        try
        {
            CreateQueueRequest createQueueRequest = new() { QueueName = queueName };
            if (isFifo) { createQueueRequest.QueueName = $"{createQueueRequest.QueueName}{Constants.FIFO_QUEUE_SUFFIX}"; createQueueRequest.Attributes.Add(QueueAttributeName.FifoQueue, "true"); }
            CreateQueueResponse createQueueResponse = await _amazonSQSClient.CreateQueueAsync(createQueueRequest); 
            createQueueResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
            if (!queueName.EndsWith(Constants.DEAD_LETTER_SUFFIX))
            {
                CreateQueueRequest createDeadLetterQueueRequest = new() { QueueName = queueName + Constants.DEAD_LETTER_SUFFIX };
                if (isFifo) { createDeadLetterQueueRequest.QueueName = $"{createDeadLetterQueueRequest.QueueName}{Constants.FIFO_QUEUE_SUFFIX}"; createDeadLetterQueueRequest.Attributes.Add(QueueAttributeName.FifoQueue, "true"); }
                CreateQueueResponse createDeadLetterQueueResponse = await _amazonSQSClient.CreateQueueAsync(createDeadLetterQueueRequest); 
                createDeadLetterQueueResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
                Dictionary<string, string> attributes = (await _amazonSQSClient.GetQueueAttributesAsync(new GetQueueAttributesRequest() { QueueUrl = createDeadLetterQueueResponse.QueueUrl, AttributeNames = new List<string>() { "QueueArn" } })).Attributes; 
                SetQueueAttributesResponse setQueueAttributesResponse = await _amazonSQSClient.SetQueueAttributesAsync(new SetQueueAttributesRequest { QueueUrl = createQueueResponse.QueueUrl, Attributes = new Dictionary<string, string> { { "RedrivePolicy", JsonSerializer.Serialize(new { maxReceiveCount = "3", deadLetterTargetArn = attributes["QueueArn"] }) } } }); 
                setQueueAttributesResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
            }
            return createQueueResponse.QueueUrl;
        }
        catch (Exception) { return string.Empty; }
    }

    private async Task<bool> InternalDeleteQueueWithDeadLetterAsync(string queueName, string topicName, bool isFifo = false)
    {
        try
        {
            DeleteQueueRequest deleteDeadletterQueueRequest = new() { QueueUrl = isFifo ? await GetQueueUrl($"{queueName}{Constants.DEAD_LETTER_SUFFIX}{Constants.FIFO_QUEUE_SUFFIX}") : await GetQueueUrl($"{queueName}{Constants.DEAD_LETTER_SUFFIX}") };
            DeleteQueueResponse deleteDeadletterQueueResponse = await _amazonSQSClient.DeleteQueueAsync(deleteDeadletterQueueRequest); 
            deleteDeadletterQueueResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
            if (!isFifo && !string.IsNullOrEmpty(topicName))
            {
                string topicArn = await GetTopicArn(topicName);
                ListSubscriptionsByTopicResponse currentSubscriptions = await _amazonSNSClient.ListSubscriptionsByTopicAsync(topicArn); 
                string mainQueueArn = await GetQueueArn(queueName);
                Subscription subscription = currentSubscriptions.Subscriptions.FirstOrDefault(x => x.Endpoint.Equals(mainQueueArn, StringComparison.InvariantCultureIgnoreCase) && x.Protocol.Equals("sqs", StringComparison.InvariantCultureIgnoreCase));
                if (subscription is not null) { UnsubscribeResponse unsubscribeResponse = await _amazonSNSClient.UnsubscribeAsync(subscription.SubscriptionArn); } 
            }
            DeleteQueueRequest deleteQueueRequest = new() { QueueUrl = isFifo ? await GetQueueUrl($"{queueName}{Constants.FIFO_QUEUE_SUFFIX}") : await GetQueueUrl(queueName) };
            DeleteQueueResponse deleteQueueResponse = await _amazonSQSClient.DeleteQueueAsync(deleteQueueRequest); 
            deleteQueueResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
            return true;
        }
        catch (Exception) { return false; }
    }

    private async Task<string> GetQueueUrl(string queueName, bool isFifo = false)
    {
        try
        {
            string queueUrl = string.Empty;
            try
            {
                GetQueueUrlResponse getQueueUrl = await _amazonSQSClient.GetQueueUrlAsync(new GetQueueUrlRequest(queueName: isFifo ? $"{queueName}{Constants.FIFO_QUEUE_SUFFIX}" : queueName)); 
                if (!getQueueUrl.HttpStatusCode.ToString().StartsWith("4") && !getQueueUrl.HttpStatusCode.ToString().StartsWith("5")) queueUrl = getQueueUrl.QueueUrl;
            }
            catch (Exception Ex) { if (Ex.Message.Equals("The specified queue does not exist for this wsdl version.", StringComparison.Ordinal)) queueUrl = string.Empty; else throw; }
            if (string.IsNullOrEmpty(queueUrl)) queueUrl = await CreateQueueWithDeadLetterAsync(queueName, isFifo: isFifo);
            return queueUrl;
        }
        catch (Exception) { return string.Empty; }
    }

    private async Task<string> GetQueueArn(string queueName, bool isFifo = false)
    {
        try
        {
            string queueArn = string.Empty;
            try
            {
                string queueUrl = await GetQueueUrl(queueName, isFifo);
                GetQueueAttributesResponse getQueueArn = await _amazonSQSClient.GetQueueAttributesAsync(new GetQueueAttributesRequest(queueUrl: queueUrl, attributeNames: new List<string>() { "QueueArn" })); 
                if (!getQueueArn.HttpStatusCode.ToString().StartsWith("4") && !getQueueArn.HttpStatusCode.ToString().StartsWith("5")) queueArn = getQueueArn.QueueARN;
            }
            catch (Exception Ex) { if (Ex.Message.Equals("The specified queue does not exist for this wsdl version.", StringComparison.Ordinal)) queueArn = string.Empty; else throw; }
            return queueArn;
        }
        catch (Exception) { return string.Empty; }
    }

    private static bool InternalProcessMessage<T>(T responseObject, string topic, string key, Dictionary<string, string> customAttributes, Delegate handler)
    {
        return handler switch
        {
            ProcessMessageDelegate<T> messageProcessor => messageProcessor(responseObject, key, customAttributes),
            ProcessDeadLetterMessageDelegate<T> deadLetterProcessor => deadLetterProcessor(responseObject, topic, key, customAttributes),
            _ => false,
        };
    }

    private static bool InternalProcessMessageMultipleKeys<T>(T responseObject, string topic, List<string> keyList, Dictionary<string, string> customAttributes, Delegate handler)
    {
        return handler switch
        {
            ProcessMessageDelegateMultipleKeys<T> messageProcessor => messageProcessor(responseObject, keyList, customAttributes),
            ProcessDeadLetterMessageDelegateMultipleKeys<T> deadLetterProcessor => deadLetterProcessor(responseObject, topic, keyList, customAttributes),
            _ => false,
        };
    }

    private static bool InternalProcessMessage(string responseString, string topic, string key, Dictionary<string, string> customAttributes, Delegate handler)
    {
        return handler switch
        {
            ProcessMessageStringDelegate messageProcessor => messageProcessor(responseString, key, customAttributes),
            ProcessDeadLetterMessageStringDelegate deadLetterProcessor => deadLetterProcessor(responseString, topic, key, customAttributes),
            _ => false,
        };
    }

    private static bool InternalProcessMessageMultipleKeys(string responseString, string topic, List<string> keyList, Dictionary<string, string> customAttributes, Delegate handler)
    {
        return handler switch
        {
            ProcessMessageStringDelegateMultipleKeys messageProcessor => messageProcessor(responseString, keyList, customAttributes),
            ProcessDeadLetterMessageStringDelegateMultipleKeys deadLetterProcessor => deadLetterProcessor(responseString, topic, keyList, customAttributes),
            _ => false,
        };
    }

    private async Task AcknowledgeMessage(string queueUrl, string receiptHandle, bool isFailed)
    {
        try
        {
            if (isFailed) { /* Allow visibility timeout */ }
            else
            {
                DeleteMessageRequest deleteMessageRequest = new() { QueueUrl = queueUrl, ReceiptHandle = receiptHandle };
                DeleteMessageResponse response = await _amazonSQSClient.DeleteMessageAsync(deleteMessageRequest); // Corrected
                response.HttpStatusCode.EnsureSuccessHttpStatusCode();
            }
        }
        catch (Exception) { throw; }
    }

    private async Task InternalEnsureQueueIsSubscribedToTopic(string topicName, string queueName, string filterKey, string metaKey = null)
    {
        try
        {
            if (queueSubscriptions.TryGetValue($"{topicName}-{queueName}", out bool isQueueSubscribed)) return;
            string topicArn = await GetTopicArn(topicName);
            string queueUrl = await GetQueueUrl(queueName, false);
            string queueArn = await GetQueueArn(queueName, false);
            if (string.IsNullOrWhiteSpace(topicArn)) throw new ArgumentNullException(nameof(topicArn), "Cannot find or create topic.");
            if (string.IsNullOrWhiteSpace(queueUrl)) throw new ArgumentNullException(nameof(queueUrl), "Cannot find or create queue.");
            if (string.IsNullOrWhiteSpace(filterKey)) throw new ArgumentNullException(nameof(filterKey), "Filter Key must be provided.");
            ListSubscriptionsByTopicResponse currentSubscriptions = await _amazonSNSClient.ListSubscriptionsByTopicAsync(topicArn); // Corrected
            if (!currentSubscriptions.Subscriptions.Any(x => x.Endpoint.Equals(queueArn, StringComparison.InvariantCultureIgnoreCase) && x.Protocol.Equals("sqs", StringComparison.InvariantCultureIgnoreCase)))
            {
                string subscriptionArn = await _amazonSNSClient.SubscribeQueueAsync(topicArn, _amazonSQSClient, queueUrl); // Corrected
                string filterPolicyValue = !string.IsNullOrWhiteSpace(metaKey) ? $@"{{ ""{Constants.ROUTING_KEY_NAME}"": [ ""{filterKey}"" ], ""{Constants.META_KEY_NAME}"": [ ""{metaKey}"" ] }}" : $@"{{ ""{Constants.ROUTING_KEY_NAME}"": [ ""{filterKey}"" ] }}";
                _ = await _amazonSNSClient.SetSubscriptionAttributesAsync(subscriptionArn: subscriptionArn, attributeName: "FilterPolicy", attributeValue: filterPolicyValue); // Corrected
            }
            queueSubscriptions.Add($"{topicName}-{queueName}", true);
        }
        catch (Exception) { throw; }
    }

    private async Task InternalEnsureQueueIsSubscribedToTopic(string topicName, string queueName, List<string> filterKeyList, string metaKey = null)
    {
        try
        {
            string topicArn = await GetTopicArn(topicName);
            string queueUrl = await GetQueueUrl(queueName, false);
            string queueArn = await GetQueueArn(queueName, false);
            if (string.IsNullOrWhiteSpace(topicArn)) throw new ArgumentNullException(nameof(topicArn), $"Cannot find or create topic. Name: {topicName}");
            if (string.IsNullOrWhiteSpace(queueUrl)) throw new ArgumentNullException(nameof(queueUrl), $"Cannot find or craate queue. Name: {queueName}");
            if (filterKeyList is null || !filterKeyList.Any()) throw new ArgumentNullException(nameof(filterKeyList), "At least one filter key must be provided.");
            ListSubscriptionsByTopicResponse currentSubscriptions = await _amazonSNSClient.ListSubscriptionsByTopicAsync(topicArn); // Corrected
            if (!currentSubscriptions.Subscriptions.Any(x => x.Endpoint.Equals(queueArn, StringComparison.InvariantCultureIgnoreCase) && x.Protocol.Equals("sqs", StringComparison.InvariantCultureIgnoreCase)))
            {
                string filter = $@"{{ ""{Constants.ROUTING_KEY_NAME}"": [ ";
                int iCounter = 1;
                foreach (string filterKey in filterKeyList) { if (iCounter == filterKeyList.Count) filter = $@"{filter}""{filterKey}"""; else filter = $@"{filter}""{filterKey}"", "; iCounter++; }
                filter += "] ";
                if (!string.IsNullOrEmpty(metaKey)) filter = $@"{filter}, ""{Constants.META_KEY_NAME}"" : [ ""{metaKey}"" ] ";
                filter += " }";
                string subscriptionArn = await _amazonSNSClient.SubscribeQueueAsync(topicArn, _amazonSQSClient, queueUrl); // Corrected
                await _amazonSNSClient.SetSubscriptionAttributesAsync(subscriptionArn: subscriptionArn, attributeName: "FilterPolicy", attributeValue: filter); // Corrected
            }
        }
        catch (Exception) { throw; }
    }

    private async Task<bool> InternalDeleteTopicAsync(string topicName)
    {
        try
        {
            DeleteTopicResponse deleteTopicResponse = await _amazonSNSClient.DeleteTopicAsync(await GetTopicArn(topicName)); // Corrected
            deleteTopicResponse.HttpStatusCode.EnsureSuccessHttpStatusCode();
            return true;
        }
        catch (Exception) { return false; }
    }

    private static Dictionary<string, string> MapToStringDictionary(Dictionary<string, CustomMessageAttribute> messageAttributes)
    {
        return messageAttributes?.Select(_ => new KeyValuePair<string, string>(_.Key, _.Value.Value))?.ToDictionary(_ => _.Key, _ => _.Value);
    }

    #endregion Private

}