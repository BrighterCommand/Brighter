using System.Text.Json;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;

public class MyParameterizedTransform : IAmAMessageTransform
{
    public static readonly string HEADER_KEY = "MyParameterizedTransformTest";
    private string _template;
    private string _displayFormat;
    
    /// <summary>
    /// Gets or sets the context. Usually the context is given to you by the pipeline and you do not need to set this
    /// </summary>
    /// <value>The context.</value>
    public IRequestContext Context { get; set; }


    public void InitializeWrapFromAttributeParams(params object[] initializerList)
    {
        _template = (string)initializerList[0];
    }

    public void InitializeUnwrapFromAttributeParams(params object[] initializerList)
    {
        _displayFormat = (string)initializerList[0];
    }

    public Message Wrap(Message message, Publication publication)
    {
        message.Header.Bag.Add(HEADER_KEY, _template);
        return message;
    }

    public Message Unwrap(Message message)
    {
        var oldCommand = JsonSerializer.Deserialize<MyTransformableCommand>(message.Body.Value);
        oldCommand.Value = string.Format(_displayFormat, oldCommand.Value);
        message.Body = new MessageBody(JsonSerializer.Serialize(oldCommand, new JsonSerializerOptions(JsonSerializerDefaults.General)));
        return message;
    }

    public void Dispose() { }

}
