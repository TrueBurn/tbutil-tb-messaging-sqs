namespace TBUtil.MessagingSqs;

/// <summary>
/// Custom Implementation of the SNS and SQS MessageAttribute models used for mapping.
/// </summary>
public class CustomMessageAttribute
{
    /// <summary>
    /// The datatype of the attribute.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// The value of the attribute.
    /// </summary>
    public string Value { get; set; }
}
