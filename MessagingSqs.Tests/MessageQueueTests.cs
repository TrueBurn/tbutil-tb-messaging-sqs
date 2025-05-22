using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text.Json;
using TBUtil.MessagingSqs;
using Moq;
using Amazon.SQS;
using Amazon.SimpleNotificationService;
using Amazon.SQS.Model; // Ensures Message is SQS.Model.Message
using Amazon.SimpleNotificationService.Model; // Ensures other models are SNS
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
using System.Linq; 
// Fully qualify Constants to avoid ambiguity
using Constants = TBUtil.MessagingSqs.Constants; 

namespace MessagingSqs.Tests;

[TestClass]
public class MessageQueueTests
{
    private Mock<IAmazonSQS> _mockSqs;
    private Mock<IAmazonSimpleNotificationService> _mockSns;
    private MessageQueue _messageQueue;

    [TestInitialize]
    public void TestInitialize()
    {
        _mockSqs = new Mock<IAmazonSQS>();
        _mockSns = new Mock<IAmazonSimpleNotificationService>();
        _messageQueue = new MessageQueue(_mockSqs.Object, _mockSns.Object);
    }

    [TestMethod]
    public async Task Broadcast_Message_And_Consume()
    {
        Models.Dummy dummyModel = new();
        string topicName = "dev-unit-broadcast";
        string queueName = "dev-unit-broadcast-consume";
        string routingKey = "dev-test";
        string accountId = "123456789012"; 
        string region = "eu-central-1"; 
        string queueUrl = $"https://sqs.{region}.amazonaws.com/{accountId}/{queueName}";
        string queueArn = $"arn:aws:sqs:{region}:{accountId}:{queueName}";
        string topicArn = $"arn:aws:sns:{region}:{accountId}:{topicName}";
        string subscriptionArn = $"{topicArn}:some-subscription-id";

        // 1. Fix FindTopicAsync and FindTopicResponse
        _mockSns.Setup(s => s.FindTopicAsync(It.Is<FindTopicRequest>(req => req.Name == topicName), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new Amazon.SimpleNotificationService.Model.FindTopicResponse { TopicArn = topicArn, HttpStatusCode = HttpStatusCode.OK });

        _mockSqs.Setup(s => s.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == queueName), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = queueUrl, HttpStatusCode = HttpStatusCode.OK });
        _mockSqs.Setup(s => s.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == queueName + Constants.DEAD_LETTER_SUFFIX), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = queueUrl + Constants.DEAD_LETTER_SUFFIX, HttpStatusCode = HttpStatusCode.OK });

        // 2. Fix QueueARN assignment
        _mockSqs.Setup(s => s.GetQueueAttributesAsync(It.Is<GetQueueAttributesRequest>(r => r.QueueUrl == queueUrl && r.AttributeNames.Contains("QueueArn")), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new GetQueueAttributesResponse { Attributes = new Dictionary<string, string> { { "QueueArn", queueArn } }, HttpStatusCode = HttpStatusCode.OK });

        _mockSns.Setup(s => s.ListSubscriptionsByTopicAsync(It.Is<ListSubscriptionsByTopicRequest>(req => req.TopicArn == topicArn), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new ListSubscriptionsByTopicResponse { Subscriptions = new List<Subscription>(), HttpStatusCode = HttpStatusCode.OK });
        
        // 3. Fix SubscribeQueueAsync (now SubscribeAsync)
        _mockSns.Setup(s => s.SubscribeAsync(It.Is<SubscribeRequest>(req => req.TopicArn == topicArn && req.Protocol == "sqs" && req.Endpoint == queueArn), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new SubscribeResponse { SubscriptionArn = subscriptionArn, HttpStatusCode = HttpStatusCode.OK });
        
        _mockSns.Setup(s => s.SetSubscriptionAttributesAsync(It.Is<SetSubscriptionAttributesRequest>(req => req.SubscriptionArn == subscriptionArn && req.AttributeName == "FilterPolicy"), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new SetSubscriptionAttributesResponse { HttpStatusCode = HttpStatusCode.OK });

        _mockSns.Setup(s => s.PublishAsync(It.Is<PublishRequest>(r => r.TopicArn == topicArn), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new PublishResponse { MessageId = "test-message-id", HttpStatusCode = HttpStatusCode.OK });

        // 4 & 5. Fix SQS.Model.Message instantiation and SNS message simulation
        // Construct the SNS message JSON string
        var snsMessageUtil = new Amazon.SimpleNotificationService.Util.Message
        {
            MessageText = JsonSerializer.Serialize(dummyModel),
            TopicArn = topicArn, // Typically included
            Timestamp = DateTime.UtcNow, // Typically included
            // SNS MessageAttributes are part of the JSON body
            MessageAttributes = new Dictionary<string, Amazon.SimpleNotificationService.Util.MessageAttribute> 
            {
                { Constants.ROUTING_KEY_NAME, new Amazon.SimpleNotificationService.Util.MessageAttribute { Type = "String", Value = routingKey } }
            }
        };
        string snsMessageJson = JsonSerializer.Serialize(snsMessageUtil);

        var sqsReceivedMessage = new Amazon.SQS.Model.Message // SQS Message
        {
            MessageId = "sqs-message-id",
            ReceiptHandle = "receipt-handle",
            Body = snsMessageJson, // The Body is the JSON string of the SNS message
            // SQS MessageAttributes are distinct from SNS MessageAttributes embedded in the body
            MessageAttributes = new Dictionary<string, MessageAttributeValue>() 
        };
        _mockSqs.Setup(s => s.ReceiveMessageAsync(It.Is<ReceiveMessageRequest>(r => r.QueueUrl == queueUrl), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse { Messages = new List<Amazon.SQS.Model.Message> { sqsReceivedMessage }, HttpStatusCode = HttpStatusCode.OK });

        _mockSqs.Setup(s => s.DeleteMessageAsync(It.Is<DeleteMessageRequest>(r => r.QueueUrl == queueUrl && r.ReceiptHandle == "receipt-handle"), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new DeleteMessageResponse { HttpStatusCode = HttpStatusCode.OK });
        
        _mockSqs.Setup(s => s.DeleteQueueAsync(It.IsAny<DeleteQueueRequest>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new DeleteQueueResponse { HttpStatusCode = HttpStatusCode.OK });

        _mockSns.Setup(s => s.DeleteTopicAsync(It.Is<DeleteTopicRequest>(req => req.TopicArn == topicArn), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new DeleteTopicResponse { HttpStatusCode = HttpStatusCode.OK });
        
        _mockSns.Setup(s => s.UnsubscribeAsync(It.Is<UnsubscribeRequest>(req => req.SubscriptionArn == subscriptionArn), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new UnsubscribeResponse { HttpStatusCode = HttpStatusCode.OK });

        await _messageQueue.EnsureQueueIsSubscribedToTopic(topicName, queueName, routingKey);
        Assert.IsTrue(await _messageQueue.BroadcastAsync(snsTopic: topicName, key: routingKey, broadcastObject: dummyModel));

        bool messageProcessedSuccessfully = false;
        string resultMessageId = await _messageQueue.DequeueAsync<Models.Dummy>(queueName: queueName, topicName: topicName, routingKey, (message, key, customAttributes) => {
            string origional = JsonSerializer.Serialize(message);
            string messageVersion = JsonSerializer.Serialize(dummyModel);
            Assert.IsTrue(origional == messageVersion);
            messageProcessedSuccessfully = (message.Property1 == dummyModel.Property1);
            return messageProcessedSuccessfully;
        });

        Assert.IsTrue(messageProcessedSuccessfully);
        Assert.IsTrue(!string.IsNullOrEmpty(resultMessageId));
        
        Assert.IsTrue(await _messageQueue.DeleteQueueWithDeadLetterAsync(queueName, topicName, false));
        Assert.IsTrue(await _messageQueue.DeleteTopicAsync(topicName));

        _mockSns.Verify(s => s.FindTopicAsync(It.Is<FindTopicRequest>(req => req.Name == topicName), It.IsAny<System.Threading.CancellationToken>()), Times.AtLeastOnce);
        _mockSqs.Verify(s => s.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == queueName), It.IsAny<System.Threading.CancellationToken>()), Times.AtLeastOnce);
        _mockSns.Verify(s => s.PublishAsync(It.Is<PublishRequest>(r => r.TopicArn == topicArn), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _mockSqs.Verify(s => s.ReceiveMessageAsync(It.Is<ReceiveMessageRequest>(r => r.QueueUrl == queueUrl), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _mockSqs.Verify(s => s.DeleteMessageAsync(It.Is<DeleteMessageRequest>(r => r.QueueUrl == queueUrl && r.ReceiptHandle == "receipt-handle"), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _mockSqs.Verify(s => s.DeleteQueueAsync(It.Is<DeleteQueueRequest>(r => r.QueueUrl.Contains(queueName)), It.IsAny<System.Threading.CancellationToken>()), Times.Exactly(2)); 
        _mockSns.Verify(s => s.DeleteTopicAsync(It.Is<DeleteTopicRequest>(req => req.TopicArn == topicArn), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Send_And_Process_Message_To_SQS_Async()
    {
        Models.Dummy dummyModel = new();
        string queueName = "dev-unit-send";
        string queueUrl = $"https://sqs.eu-central-1.amazonaws.com/123456789012/{queueName}";

        _mockSqs.Setup(s => s.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == queueName), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = queueUrl, HttpStatusCode = HttpStatusCode.OK });
        _mockSqs.Setup(s => s.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == queueName + Constants.DEAD_LETTER_SUFFIX), It.IsAny<System.Threading.CancellationToken>()))
           .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = queueUrl + Constants.DEAD_LETTER_SUFFIX, HttpStatusCode = HttpStatusCode.OK });

        _mockSqs.Setup(s => s.SendMessageAsync(It.Is<SendMessageRequest>(r => r.QueueUrl == queueUrl), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new SendMessageResponse { MessageId = "test-sqs-message-id", HttpStatusCode = HttpStatusCode.OK });

        var sqsMessage = new Amazon.SQS.Model.Message 
        {
            MessageId = "sqs-message-id",
            ReceiptHandle = "receipt-handle",
            Body = JsonSerializer.Serialize(dummyModel) 
        };
        _mockSqs.Setup(s => s.ReceiveMessageAsync(It.Is<ReceiveMessageRequest>(r => r.QueueUrl == queueUrl), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse { Messages = new List<Amazon.SQS.Model.Message> { sqsMessage }, HttpStatusCode = HttpStatusCode.OK });

        _mockSqs.Setup(s => s.DeleteMessageAsync(It.Is<DeleteMessageRequest>(r => r.QueueUrl == queueUrl && r.ReceiptHandle == "receipt-handle"), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new DeleteMessageResponse { HttpStatusCode = HttpStatusCode.OK });
        
        _mockSqs.Setup(s => s.DeleteQueueAsync(It.IsAny<DeleteQueueRequest>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new DeleteQueueResponse { HttpStatusCode = HttpStatusCode.OK });
         _mockSqs.Setup(s => s.GetQueueAttributesAsync(It.Is<GetQueueAttributesRequest>(r => r.QueueUrl == queueUrl + Constants.DEAD_LETTER_SUFFIX && r.AttributeNames.Contains("QueueArn")), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new GetQueueAttributesResponse { Attributes = new Dictionary<string, string>{{"QueueArn", "arn_dlq"}}, HttpStatusCode = HttpStatusCode.OK });


        Assert.IsTrue(await _messageQueue.EnqueueAsync(queueName: queueName, enqueueObject: dummyModel));

        bool messageProcessedSuccessfully = false;
        string resultMessageId = await _messageQueue.DequeueAsync<Models.Dummy>(queueName: queueName, (message, key, customAttributes) => {
            string origional = JsonSerializer.Serialize(message);
            string messageVersion = JsonSerializer.Serialize(dummyModel);
            Assert.IsTrue(origional == messageVersion);
            messageProcessedSuccessfully = message.Property1 == dummyModel.Property1;
            return messageProcessedSuccessfully;
        });
        Assert.IsTrue(messageProcessedSuccessfully);
        Assert.IsTrue(!string.IsNullOrEmpty(resultMessageId));

        bool isDeleted = await _messageQueue.DeleteQueueWithDeadLetterAsync(queueName, isFifo: false);
        Assert.IsTrue(isDeleted);

        _mockSqs.Verify(s => s.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == queueName), It.IsAny<System.Threading.CancellationToken>()), Times.AtLeastOnce);
        _mockSqs.Verify(s => s.SendMessageAsync(It.Is<SendMessageRequest>(r => r.QueueUrl == queueUrl), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _mockSqs.Verify(s => s.ReceiveMessageAsync(It.Is<ReceiveMessageRequest>(r => r.QueueUrl == queueUrl), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _mockSqs.Verify(s => s.DeleteMessageAsync(It.Is<DeleteMessageRequest>(r => r.QueueUrl == queueUrl && r.ReceiptHandle == "receipt-handle"), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _mockSqs.Verify(s => s.DeleteQueueAsync(It.Is<DeleteQueueRequest>(r => r.QueueUrl.Contains(queueName)), It.IsAny<System.Threading.CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task Send_And_Process_Message_To_SQS_fifo()
    {
        Models.Dummy dummyModel = new();
        string queueName = "dev-unit-send-f";
        string fifoQueueName = $"{queueName}{Constants.FIFO_QUEUE_SUFFIX}";
        string queueUrl = $"https://sqs.eu-central-1.amazonaws.com/123456789012/{fifoQueueName}";
        string groupId = "fifo-group";
        string deduplicationId = Guid.NewGuid().ToString();

        _mockSqs.Setup(s => s.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == fifoQueueName), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = queueUrl, HttpStatusCode = HttpStatusCode.OK });
         _mockSqs.Setup(s => s.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == fifoQueueName.Replace(Constants.FIFO_QUEUE_SUFFIX, Constants.DEAD_LETTER_SUFFIX + Constants.FIFO_QUEUE_SUFFIX)), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = queueUrl.Replace(Constants.FIFO_QUEUE_SUFFIX, Constants.DEAD_LETTER_SUFFIX + Constants.FIFO_QUEUE_SUFFIX), HttpStatusCode = HttpStatusCode.OK });


        _mockSqs.Setup(s => s.SendMessageAsync(It.Is<SendMessageRequest>(r => r.QueueUrl == queueUrl && r.MessageGroupId == groupId && r.MessageDeduplicationId == deduplicationId), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new SendMessageResponse { MessageId = "test-sqs-fifo-message-id", HttpStatusCode = HttpStatusCode.OK });

        var sqsMessage = new Amazon.SQS.Model.Message 
        {
            MessageId = "sqs-fifo-message-id",
            ReceiptHandle = "receipt-handle-fifo",
            Body = JsonSerializer.Serialize(dummyModel)
        };
        _mockSqs.Setup(s => s.ReceiveMessageAsync(It.Is<ReceiveMessageRequest>(r => r.QueueUrl == queueUrl), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse { Messages = new List<Amazon.SQS.Model.Message> { sqsMessage }, HttpStatusCode = HttpStatusCode.OK });

        _mockSqs.Setup(s => s.DeleteMessageAsync(It.Is<DeleteMessageRequest>(r => r.QueueUrl == queueUrl && r.ReceiptHandle == "receipt-handle-fifo"), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new DeleteMessageResponse { HttpStatusCode = HttpStatusCode.OK });

        _mockSqs.Setup(s => s.DeleteQueueAsync(It.IsAny<DeleteQueueRequest>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new DeleteQueueResponse { HttpStatusCode = HttpStatusCode.OK });
        
        _mockSqs.Setup(s => s.GetQueueAttributesAsync(It.Is<GetQueueAttributesRequest>(r => r.QueueUrl == queueUrl.Replace(Constants.FIFO_QUEUE_SUFFIX, Constants.DEAD_LETTER_SUFFIX + Constants.FIFO_QUEUE_SUFFIX) && r.AttributeNames.Contains("QueueArn")), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new GetQueueAttributesResponse { Attributes = new Dictionary<string, string>{{"QueueArn", "arn_dlq_fifo"}}, HttpStatusCode = HttpStatusCode.OK });


        Assert.IsTrue(_messageQueue.EnqueueFifo(queueName: queueName, groupId: groupId, MessageDeduplicationId: deduplicationId, enqueueObject: dummyModel));
        
        bool messageProcessedSuccessfully = false;
        string resultMessageId = _messageQueue.DequeueFifo<Models.Dummy>(queueName: queueName, (message, key, customAttributes) => {
            string origional = JsonSerializer.Serialize(message);
            string messageVersion = JsonSerializer.Serialize(dummyModel);
            Assert.IsTrue(origional == messageVersion);
            messageProcessedSuccessfully = message.Property1 == dummyModel.Property1;
            return messageProcessedSuccessfully;
        });
        Assert.IsTrue(messageProcessedSuccessfully);
        Assert.IsTrue(!string.IsNullOrEmpty(resultMessageId));

        bool isDeleted = await _messageQueue.DeleteQueueWithDeadLetterAsync(queueName, isFifo: true);
        Assert.IsTrue(isDeleted);

        _mockSqs.Verify(s => s.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == fifoQueueName), It.IsAny<System.Threading.CancellationToken>()), Times.AtLeastOnce);
        _mockSqs.Verify(s => s.SendMessageAsync(It.Is<SendMessageRequest>(r => r.QueueUrl == queueUrl && r.MessageGroupId == groupId && r.MessageDeduplicationId == deduplicationId), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _mockSqs.Verify(s => s.ReceiveMessageAsync(It.Is<ReceiveMessageRequest>(r => r.QueueUrl == queueUrl), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _mockSqs.Verify(s => s.DeleteMessageAsync(It.Is<DeleteMessageRequest>(r => r.QueueUrl == queueUrl && r.ReceiptHandle == "receipt-handle-fifo"), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _mockSqs.Verify(s => s.DeleteQueueAsync(It.Is<DeleteQueueRequest>(r => r.QueueUrl.Contains(fifoQueueName.Replace(Constants.FIFO_QUEUE_SUFFIX, ""))), It.IsAny<System.Threading.CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task Create_And_Delete_Queue_Standard()
    {
        string queueName = $"dev-unit-queuecrud{DateTime.UtcNow.Ticks}";
        string queueUrl = $"https://sqs.eu-central-1.amazonaws.com/123456789012/{queueName}";
        string dlqUrl = $"https://sqs.eu-central-1.amazonaws.com/123456789012/{queueName}{Constants.DEAD_LETTER_SUFFIX}";
        string dlqArn = $"arn:aws:sqs:eu-central-1:123456789012:{queueName}{Constants.DEAD_LETTER_SUFFIX}";

        _mockSqs.Setup(s => s.CreateQueueAsync(It.Is<CreateQueueRequest>(r => r.QueueName == queueName), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new CreateQueueResponse { QueueUrl = queueUrl, HttpStatusCode = HttpStatusCode.OK });
        _mockSqs.Setup(s => s.CreateQueueAsync(It.Is<CreateQueueRequest>(r => r.QueueName == queueName + Constants.DEAD_LETTER_SUFFIX), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new CreateQueueResponse { QueueUrl = dlqUrl, HttpStatusCode = HttpStatusCode.OK });
        
        _mockSqs.Setup(s => s.GetQueueAttributesAsync(It.Is<GetQueueAttributesRequest>(r => r.QueueUrl == dlqUrl && r.AttributeNames.Contains("QueueArn")), It.IsAny<System.Threading.CancellationToken>())) 
            .ReturnsAsync(new GetQueueAttributesResponse { Attributes = new Dictionary<string, string> { { "QueueArn", dlqArn } }, HttpStatusCode = HttpStatusCode.OK });
        
        _mockSqs.Setup(s => s.SetQueueAttributesAsync(It.Is<SetQueueAttributesRequest>(r => r.QueueUrl == queueUrl), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new SetQueueAttributesResponse { HttpStatusCode = HttpStatusCode.OK });

        _mockSqs.Setup(s => s.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == queueName), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = queueUrl, HttpStatusCode = HttpStatusCode.OK });
        _mockSqs.Setup(s => s.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == queueName + Constants.DEAD_LETTER_SUFFIX), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = dlqUrl, HttpStatusCode = HttpStatusCode.OK });

        _mockSqs.Setup(s => s.DeleteQueueAsync(It.Is<DeleteQueueRequest>(r => r.QueueUrl == queueUrl), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new DeleteQueueResponse { HttpStatusCode = HttpStatusCode.OK });
        _mockSqs.Setup(s => s.DeleteQueueAsync(It.Is<DeleteQueueRequest>(r => r.QueueUrl == dlqUrl), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new DeleteQueueResponse { HttpStatusCode = HttpStatusCode.OK });

        string createdQueueUrl = await _messageQueue.CreateQueueWithDeadLetterAsync(queueName, false);
        Assert.AreEqual(queueUrl, createdQueueUrl);

        bool isDeleted = await _messageQueue.DeleteQueueWithDeadLetterAsync(queueName, isFifo: false);
        Assert.IsTrue(isDeleted);

        _mockSqs.Verify(s => s.CreateQueueAsync(It.Is<CreateQueueRequest>(r => r.QueueName == queueName), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _mockSqs.Verify(s => s.CreateQueueAsync(It.Is<CreateQueueRequest>(r => r.QueueName == queueName + Constants.DEAD_LETTER_SUFFIX), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _mockSqs.Verify(s => s.DeleteQueueAsync(It.Is<DeleteQueueRequest>(r => r.QueueUrl == queueUrl), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _mockSqs.Verify(s => s.DeleteQueueAsync(It.Is<DeleteQueueRequest>(r => r.QueueUrl == dlqUrl), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Create_And_Delete_Queue_Fifo()
    {
        string queueName = $"dev-unit-queuecrud-f{DateTime.UtcNow.Ticks}";
        string fifoQueueName = $"{queueName}{Constants.FIFO_QUEUE_SUFFIX}";
        string fifoDlqName = $"{queueName}{Constants.DEAD_LETTER_SUFFIX}{Constants.FIFO_QUEUE_SUFFIX}";
        string queueUrl = $"https://sqs.eu-central-1.amazonaws.com/123456789012/{fifoQueueName}";
        string dlqUrl = $"https://sqs.eu-central-1.amazonaws.com/123456789012/{fifoDlqName}";
        string dlqArn = $"arn:aws:sqs:eu-central-1:123456789012:{fifoDlqName}";

        _mockSqs.Setup(s => s.CreateQueueAsync(It.Is<CreateQueueRequest>(r => r.QueueName == fifoQueueName), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new CreateQueueResponse { QueueUrl = queueUrl, HttpStatusCode = HttpStatusCode.OK });
        _mockSqs.Setup(s => s.CreateQueueAsync(It.Is<CreateQueueRequest>(r => r.QueueName == fifoDlqName), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new CreateQueueResponse { QueueUrl = dlqUrl, HttpStatusCode = HttpStatusCode.OK });

        _mockSqs.Setup(s => s.GetQueueAttributesAsync(It.Is<GetQueueAttributesRequest>(r => r.QueueUrl == dlqUrl && r.AttributeNames.Contains("QueueArn")), It.IsAny<System.Threading.CancellationToken>())) 
            .ReturnsAsync(new GetQueueAttributesResponse { Attributes = new Dictionary<string, string> { { "QueueArn", dlqArn } }, HttpStatusCode = HttpStatusCode.OK });
        
        _mockSqs.Setup(s => s.SetQueueAttributesAsync(It.Is<SetQueueAttributesRequest>(r => r.QueueUrl == queueUrl), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new SetQueueAttributesResponse { HttpStatusCode = HttpStatusCode.OK });
        
        _mockSqs.Setup(s => s.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == fifoQueueName), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = queueUrl, HttpStatusCode = HttpStatusCode.OK });
        _mockSqs.Setup(s => s.GetQueueUrlAsync(It.Is<GetQueueUrlRequest>(r => r.QueueName == fifoDlqName), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new GetQueueUrlResponse { QueueUrl = dlqUrl, HttpStatusCode = HttpStatusCode.OK });


        _mockSqs.Setup(s => s.DeleteQueueAsync(It.Is<DeleteQueueRequest>(r => r.QueueUrl == queueUrl), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new DeleteQueueResponse { HttpStatusCode = HttpStatusCode.OK });
        _mockSqs.Setup(s => s.DeleteQueueAsync(It.Is<DeleteQueueRequest>(r => r.QueueUrl == dlqUrl), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new DeleteQueueResponse { HttpStatusCode = HttpStatusCode.OK });


        string createdQueueUrl = await _messageQueue.CreateQueueWithDeadLetterAsync(queueName, true);
        Assert.AreEqual(queueUrl, createdQueueUrl);

        bool isDeleted = await _messageQueue.DeleteQueueWithDeadLetterAsync(queueName, isFifo: true);
        Assert.IsTrue(isDeleted);

        _mockSqs.Verify(s => s.CreateQueueAsync(It.Is<CreateQueueRequest>(r => r.QueueName == fifoQueueName), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _mockSqs.Verify(s => s.CreateQueueAsync(It.Is<CreateQueueRequest>(r => r.QueueName == fifoDlqName), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _mockSqs.Verify(s => s.DeleteQueueAsync(It.Is<DeleteQueueRequest>(r => r.QueueUrl == queueUrl), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        _mockSqs.Verify(s => s.DeleteQueueAsync(It.Is<DeleteQueueRequest>(r => r.QueueUrl == dlqUrl), It.IsAny<System.Threading.CancellationToken>()), Times.Once);
    }
}