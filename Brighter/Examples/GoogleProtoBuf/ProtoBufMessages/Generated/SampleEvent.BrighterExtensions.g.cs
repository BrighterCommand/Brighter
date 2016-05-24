// Generated code.  Do not modify. 
// Brigher extensions to ProtoBuf messages produced by the protoc-gen-brighter.exe Protoc.exe plug-in module.

using System;
using paramore.brighter.commandprocessor;
using Google.Protobuf;


namespace Brighter.Example.Messages
{
   /// <summary>
   /// Brigher IEvent interface implementation
   /// The interface to derive from is chosen when the .proto file
   /// is parsed based on the message class name.  Message classes 
   /// ending in Request, Response, Command and Event are derived
   /// from ICommand, IResponse, ICommand and IEvent respectively.
   /// Any class not ending in one of these keywords is not
   /// given the Brighter extensions.
   /// </summary>
   public sealed partial class SampleEvent : IEvent
   {
      public Guid Id {get; set;}
      // The protobuf generated code cleverly gives us a partial method that is called by the
      // generated constructor, which we need to set the required Id field to a new Guid value.
      // Note that in .net 6.0 an auto property with initializer is allowed, which would remove
      // the need for using this partial method.
      partial void OnConstruction()
      {
         Id = Guid.NewGuid();
      }
   }


   /// <summary>
   /// Auto-generated brighter mapping class for SampleEvent to use
   /// Brighter's serialization and deserialization logic.
   /// </summary>
   public partial class SampleEventBrighterMapper : IAmAMessageMapper<SampleEvent>
   {
      // _header defined at class scope to allow alteration through partial method AlterMessageHeader()
      private MessageHeader _header = null;

      public Message MapToMessage(SampleEvent request)
      {
         _header = new MessageHeader(messageId: request.Id, topic: SampleEvent.Descriptor.Name, messageType: MessageType.MT_EVENT);
         _header.ContentType = "application/x-protobuf";

         // Partial method allows making client specific adjustments to the header before it is included in the Message object
         AlterMessageHeader(request);

         Message msg = new Message(
            header: _header,
            body: new MessageBody(request.ToByteArray(), SampleEvent.Descriptor.Name));
         return msg;
      }


      public SampleEvent MapToRequest(Message message)
      {
         return SampleEvent.Parser.ParseFrom(message.Body.Bytes);
      }

      /// <summary>
      /// Allows user code to alter _header as part of the mapping process and before it is published.
      /// <param name="request">The request that has been serialized for sending.  May contain information needed to make custom alterations to the header</param>
      /// </summary>
      partial void AlterMessageHeader(SampleEvent request);
   }
}

