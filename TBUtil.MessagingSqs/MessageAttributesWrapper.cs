namespace TBUtil.MessagingSqs;

/// <summary>
/// Used to wrap the <see cref="CustomMessageAttribute"/> dictionary to fascilitate serrialization.
/// </summary>
public class MessageAttributesWrapper
{
    /// <summary>
    /// A Collection of <see cref="CustomMessageAttribute"/>
    /// </summary>
    public Dictionary<string, CustomMessageAttribute> MessageAttributes { get; set; }
}