namespace TBUtil.MessagingSqs;


/// <summary>
/// Callback delegate used when subscribing and dequeuing messages.
/// </summary>
/// <typeparam name="T">Request message type.</typeparam>
/// <param name="responseObject">The message that was sent to the queue.</param>
/// <param name="key">The routing key that was used to identify the message.</param>
/// <param name="customAttributes">The headers (if any) that were added by the system and custom headers added when the message was sent..</param>
/// <returns>If processing the message was successful, <c>false</c> to place the message back on the queue, <c>true</c> to complete it.</returns>
public delegate bool ProcessMessageDelegate<T>(T responseObject, string key, Dictionary<string, string> customAttributes);

/// <summary>
/// Callback delegate used when subscribing and dequeuing messages.
/// </summary>
/// <typeparam name="T">Request message type.</typeparam>
/// <param name="responseObject">The message that was sent to the queue.</param>
/// <param name="keyList">The routing keys that were used to identify the message.</param>
/// <param name="customAttributes">The headers (if any) that were added by the system and custom headers added when the message was sent..</param>
/// <returns>If processing the message was successful, <c>false</c> to place the message back on the queue, <c>true</c> to complete it.</returns>
public delegate bool ProcessMessageDelegateMultipleKeys<T>(T responseObject, List<string> keyList, Dictionary<string, string> customAttributes);

/// <summary>
/// Callback delegate used when subscribing and dequeuing messages.
/// </summary>
/// <param name="responseString">The message that was sent to the queue.</param>
/// <param name="key">The routing key that was used to identify the message.</param>
/// <param name="customAttributes">The headers (if any) that were added by the system and custom headers added when the message was sent..</param>
/// <returns>If processing the message was successful, <c>false</c> to place the message back on the queue, <c>true</c> to complete it.</returns>
public delegate bool ProcessMessageStringDelegate(string responseString, string key, Dictionary<string, string> customAttributes);

/// <summary>
/// Callback delegate used when subscribing and dequeuing messages.
/// </summary>
/// <param name="responseString">The message that was sent to the queue.</param>
/// <param name="keyList">The routing key that was used to identify the message.</param>
/// <param name="customAttributes">The headers (if any) that were added by the system and custom headers added when the message was sent..</param>
/// <returns>If processing the message was successful, <c>false</c> to place the message back on the queue, <c>true</c> to complete it.</returns>
public delegate bool ProcessMessageStringDelegateMultipleKeys(string responseString, List<string> keyList, Dictionary<string, string> customAttributes);

/// <summary>
/// Callback delegate used when subscribing and dequeuing messages.
/// </summary>
/// <typeparam name="T">Request message type.</typeparam>
/// <param name="responseObject">The message that was sent to the queue.</param>
/// <param name="originalTopic">The topic was used, can be <c>null</c> if the message was enqueued directly.</param>
/// <param name="originalKey">The routing key that was used to identify the message.</param>
/// <param name="originalHeaders">The headers (if any) that were added by the system and custom headers added when the message was sent..</param>
/// <returns>If processing the message was successful, <c>false</c> to place the message back on the queue, <c>true</c> to complete it.</returns>
public delegate bool ProcessDeadLetterMessageDelegate<T>(T responseObject, string originalTopic, string originalKey, Dictionary<string, string> originalHeaders);

/// <summary>
/// Callback delegate used when subscribing and dequeuing messages.
/// </summary>
/// <typeparam name="T">Request message type.</typeparam>
/// <param name="responseObject">The message that was sent to the queue.</param>
/// <param name="originalTopic">The topic was used, can be <c>null</c> if the message was enqueued directly.</param>
/// <param name="originalKeys">The routing kesy were was used to identify the message.</param>
/// <param name="originalHeaders">The headers (if any) that were added by the system and custom headers added when the message was sent..</param>
/// <returns>If processing the message was successful, <c>false</c> to place the message back on the queue, <c>true</c> to complete it.</returns>
public delegate bool ProcessDeadLetterMessageDelegateMultipleKeys<T>(T responseObject, string originalTopic, List<string> originalKeys, Dictionary<string, string> originalHeaders);

/// <summary>
/// Callback delegate used when subscribing and dequeuing messages.
/// </summary>
/// <param name="responseString">The message that was sent to the queue.</param>
/// <param name="originalTopic">The topic was used, can be <c>null</c> if the message was enqueued directly.</param>
/// <param name="originalKey">The routing key that was used to identify the message.</param>
/// <param name="originalHeaders">The headers (if any) that were added by the system and custom headers added when the message was sent..</param>
/// <returns>If processing the message was successful, <c>false</c> to place the message back on the queue, <c>true</c> to complete it.</returns>
public delegate bool ProcessDeadLetterMessageStringDelegate(string responseString, string originalTopic, string originalKey, Dictionary<string, string> originalHeaders);

/// <summary>
/// Callback delegate used when subscribing and dequeuing messages.
/// </summary>
/// <param name="responseString">The message that was sent to the queue.</param>
/// <param name="originalTopic">The topic was used, can be <c>null</c> if the message was enqueued directly.</param>
/// <param name="originalKeys">The routing keys that were used to identify the message.</param>
/// <param name="originalHeaders">The headers (if any) that were added by the system and custom headers added when the message was sent..</param>
/// <returns>If processing the message was successful, <c>false</c> to place the message back on the queue, <c>true</c> to complete it.</returns>
public delegate bool ProcessDeadLetterMessageStringDelegateMultipleKeys(string responseString, string originalTopic, List<string> originalKeys, Dictionary<string, string> originalHeaders);
