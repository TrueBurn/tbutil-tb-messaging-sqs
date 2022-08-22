using Amazon.SimpleNotificationService.Util;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TBUtil.MessagingSqs;


internal static class Extensions
{

    public static void EnsureSuccessHttpStatusCode(this HttpStatusCode statusCode)
    {
        if (statusCode.ToString().StartsWith("4") || statusCode.ToString().StartsWith("5"))
        {
            throw new System.Exception($"Invalid status code received: {statusCode}");
        }
    }

    public static string Base64Encode(this string plainText)
    {
        byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }

    public static string Base64Decode(this string base64EncodedData)
    {
        return base64EncodedData.IsBase64String()
            ? Encoding.UTF8.GetString(Convert.FromBase64String(base64EncodedData))
            : base64EncodedData;
    }

    public static bool IsBase64String(this string s)
    {
        s = s.Trim();
        return (s.Length % 4 == 0) && Regex.IsMatch(s, @"^[a-zA-Z0-9\+/]*={0,3}$", RegexOptions.None);

    }

    /// <summary>
    /// Parses SQS message body and produces a concise with message attributes. 
    /// </summary>
    /// <param name="messageBody"></param>
    /// <param name="withSnsMessage"></param>
    public static (Message snsMessage, MessageAttributesWrapper attributesWrapper) ParseMessage(this string messageBody, bool withSnsMessage = false)
    {
        Message snsMessage = default;

        if (withSnsMessage)
        {
            snsMessage = Message.ParseMessage(messageBody);
        }

        MessageAttributesWrapper attributesWrapper = JsonSerializer.Deserialize<MessageAttributesWrapper>(messageBody);

        return (snsMessage, attributesWrapper);
    }

    public static string SanatiseAttributeString(this string input)
    {
        try
        {
            string value = input;
            byte[] bytes = Encoding.Default.GetBytes(value);
            value = Encoding.UTF8.GetString(bytes);

            Regex rgx = new("[^a-zA-Z]");
            value = rgx.Replace(value, "");

            return value;
        }
        catch (Exception)
        {
            throw;
        }
    }

}
