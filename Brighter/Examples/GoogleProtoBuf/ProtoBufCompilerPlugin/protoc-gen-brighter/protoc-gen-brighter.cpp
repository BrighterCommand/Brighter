// protoc-gen-brighter.cpp : Generates partial classes for .proto files with the necessary additions for Brighter support.
// smclewin.  May 2016.  
#include "stdafx.h"

#include <iostream>
#include <fstream>
#include <list>


#include <google/protobuf/compiler/plugin.h>
#include <google/protobuf/compiler/code_generator.h>
#include <google/protobuf/descriptor.h>
#include <google/protobuf/io/zero_copy_stream.h>
#include <google/protobuf/io/printer.h>
#include <google/protobuf/io/coded_stream.h>
#include <google/protobuf/descriptor.pb.h>

namespace gp = google::protobuf;


class BrighterCodeGenerator : public gp::compiler::CodeGenerator
{
	/// Implements google::protobuf::compiler::CodeGenerator.
	bool Generate(
		const gp::FileDescriptor *fileDescriptor,
		const std::string &parameter,
		gp::compiler::GeneratorContext *context,
		std::string *error) const
	{
		// So far as I was able to tell I don't know enough to put this into the directory
		// path that we are compiling to, so to keep it out of the source assume bin/Release subdirectory
		// as per visual studio standards.  Having it in the Release sub-directory will get the file
		// to be included in the clean action from the IDE.
		std::string LogFileName = "bin/Release/protoc-gen-brighter.log";

		std::ofstream log;
		log.open(LogFileName, std::ios::app | std::ios::out);

		if (!log)
		{
			*error = "Unable to open the Brighter log file " + LogFileName;
			return false;
		}

		// We only support proto3
		if (fileDescriptor->syntax() != gp::FileDescriptor::SYNTAX_PROTO3) {
			*error = "Brigher Extensions for C# code generation only supports proto3 syntax";
			return false;
		}

		log << "=============================== Processing Proto File [ " << fileDescriptor->name() << " ]=======================" << std::endl;

		// Take off the .proto extension and add in the extension for the C# file being generated
		const std::string& GeneratingFileName = StripProto(fileDescriptor->name()) + ".BrighterExtensions.g.cs";

		log << "Output File Name: " << GeneratingFileName << std::endl;


		// Class name for generated code
		std::string MessageClassName = "";
		// The C# namespace to insert the code into
		std::string CSharpNamespace = "";
		// The Brighter interface from which to derive the class
		std::string BrighterInterface = "IRequest";
		// The Brighter MessageType to insert into the message when serialized
		std::string BrighterMessageTypeToken = "MT_NONE";


		if (fileDescriptor->options().has_csharp_namespace())
		{
			CSharpNamespace = fileDescriptor->options().csharp_namespace();

			log << "Using CSharp Namespace: " << CSharpNamespace << std::endl;
		}
		else
		{
			log << "csharp_namepsace is not set.  Not generating code for Brighter." << std::endl;
			return true;
		}


		// An assumption here that if a file contains one message, then the first 
		// message is the one we are going to want to process.
		if (fileDescriptor->message_type_count() >= 1)
		{
			const gp::Descriptor *descriptor = fileDescriptor->message_type(0);
			MessageClassName = descriptor->name();

			log << "Decide whether to generate code for message " << MessageClassName << std::endl;

			if (IsMessageToDefineForBrighter(MessageClassName, BrighterInterface, BrighterMessageTypeToken))
			{
				log << "Generating Brighter Extensions, using interface " << BrighterInterface << "and MessageType." << BrighterMessageTypeToken << std::endl;

				//const gp::MessageOptions &MsgOpts = descriptor->options();

				//log << descriptor->DebugString() << std::endl;

				//log << "Option count: " << MsgOpts.uninterpreted_option_size() << std::endl;
				//log << "Option count: " << MsgOpts.uninterpreted_option_size() << std::endl;

				gp::io::ZeroCopyOutputStream *stream = context->Open(GeneratingFileName);
				gp::io::Printer printer(stream, '$');
				printer.Print(GetBrighterExtensionCodeTemplate().c_str(), 
					"classname_token", MessageClassName, 
					"brighter_interface_token", BrighterInterface, 
					"namespace_token", CSharpNamespace,
					"messagetype_token", BrighterMessageTypeToken);
			}
			else 
			{
				log << "Not generating Brighter integration code for " << MessageClassName << std::endl;
			}
		}

		// Give the log one final newline at the end.
		log << std::endl;
		return true;
	}

private:
	// Insert the given code into the given file at the given insertion point.
	void Insert(gp::compiler::GeneratorContext *context,
		const std::string &filename, const std::string &insertion_point,
		const std::string &code) const {
		std::unique_ptr<gp::io::ZeroCopyOutputStream> output(
			context->OpenForInsert(filename, insertion_point));
		gp::io::CodedOutputStream coded_out(output.get());
		coded_out.WriteRaw(code.data(), code.size());
	}

	// Used to hold the Brigher specific tokens we need to use in code generation based on whether this message is an event, a command, etc.
	struct BrighterTypes {
		std::string Interface;
		std::string MessageType;

		BrighterTypes(std::string brighterInterface, std::string brighterMessageType)
		{
			Interface = brighterInterface;
			MessageType = brighterMessageType;
		}
	};

	// Looks at the end of the classname to determine whether this is one 
	// of the classes that can be delivered via Brighter.
	bool IsMessageToDefineForBrighter(const std::string messageClass, std::string &brighterInterface, std::string &BrighterMessageTypeToken) const
	{
		std::list<std::pair<std::string, BrighterTypes>> tokens;
		// The first value in these pairs is the token that the message name must end in
		// to trigger Brigher code generation.  When matched, the second value in the
		// pair is the Brigher interface that will be used to extend the ProtoBuf 
		// generated class.
		BrighterTypes ICommand { "ICommand", "MT_COMMAND" };
		BrighterTypes IRequest { "IRequest", "MT_DOCUMENT" };
		BrighterTypes IEvent { "IEvent", "MT_EVENT" };
		tokens.push_back(std::make_pair("Request", ICommand));
		tokens.push_back(std::make_pair("Response", IRequest));
		tokens.push_back(std::make_pair("Command", ICommand));
		tokens.push_back(std::make_pair("Event", IEvent));

		unsigned Len = messageClass.length();

		for (std::list<std::pair<std::string, BrighterTypes>>::iterator it = tokens.begin();
			it != tokens.end(); it++)
		{
			// Don't even bother comparing if the source string is shorter than the token we seek.
			if (Len > it->first.length())
			{
				// Get the end of the string (enough to match the token)
				const std::string lastToken = messageClass.substr(Len - it->first.length(), it->first.length());
				if (0 == lastToken.compare(it->first))
				{
					brighterInterface = it->second.Interface;
					BrighterMessageTypeToken = it->second.MessageType;
					return true;
				}
			}
		}

		brighterInterface = "";
		return false;
	}

	std::string StripProto(std::string filename) const {
		StripSuffix(&filename, ".proto");
		return filename;
	}

	bool StripSuffix(std::string *filename, const std::string &suffix) const {
		if (filename->length() >= suffix.length()) {
			size_t suffix_pos = filename->length() - suffix.length();
			if (filename->compare(suffix_pos, std::string::npos, suffix) == 0) {
				filename->resize(filename->size() - suffix.size());
				return true;
			}
		}

		return false;
	}

	std::string GetBrighterExtensionCodeTemplate() const
	{
		return
			"// Generated code.  Do not modify. \n"
			"// Brigher extensions to ProtoBuf messages produced by the protoc-gen-brighter.exe Protoc.exe plug-in module.\n"
			"\n"
			"using System;\n"
			"using paramore.brighter.commandprocessor;\n"
			"using Google.Protobuf;\n"
			"\n"
			"\n"
			"namespace $namespace_token$\n"
			"{\n"
			"   /// <summary>\n"
			"   /// Brigher $brighter_interface_token$ interface implementation\n"
			"   /// The interface to derive from is chosen when the .proto file\n"
			"   /// is parsed based on the message class name.  Message classes \n"
			"   /// ending in Request, Response, Command and Event are derived\n"
			"   /// from ICommand, IResponse, ICommand and IEvent respectively.\n"
			"   /// Any class not ending in one of these keywords is not\n"
			"   /// given the Brighter extensions.\n"
			"   /// </summary>\n"
			"   public sealed partial class $classname_token$ : $brighter_interface_token$\n"
			"   {\n"
			"      public Guid Id {get; set;}\n"
			"      // The protobuf generated code cleverly gives us a partial method that is called by the\n"
			"      // generated constructor, which we need to set the required Id field to a new Guid value.\n"
			"      // Note that in .net 6.0 an auto property with initializer is allowed, which would remove\n"
			"      // the need for using this partial method.\n"
			"      partial void OnConstruction()\n"
			"      {\n"
			"         Id = Guid.NewGuid();\n"
			"      }\n"
			"   }\n"
			"\n"
			"\n"
			"   /// <summary>\n"
			"   /// Auto-generated brighter mapping class for $classname_token$ to use\n"
			"   /// Brighter's serialization and deserialization logic.\n"
			"   /// </summary>\n"
			"   public partial class $classname_token$BrighterMapper : IAmAMessageMapper<$classname_token$>\n"
			"   {\n"
			"      // _header defined at class scope to allow alteration through partial method AlterMessageHeader()\n"
			"      private MessageHeader _header = null;\n"
			"\n"
			"      public Message MapToMessage($classname_token$ request)\n"
			"      {\n"
			"         _header = new MessageHeader(messageId: request.Id, topic: $classname_token$.Descriptor.Name, messageType: MessageType.$messagetype_token$);\n"
			"         _header.ContentType = \"application/x-protobuf\";\n"
			"\n"
			"         // Partial method allows making client specific adjustments to the header before it is included in the Message object\n"
			"         AlterMessageHeader(request);\n"
			"\n"
			"         Message msg = new Message(\n"
			"            header: _header,\n"
			"            body: new MessageBody(request.ToByteArray(), $classname_token$.Descriptor.Name));\n"
			"         return msg;\n"
			"      }\n"
			"\n"
			"\n"
			"      public $classname_token$ MapToRequest(Message message)\n"
			"      {\n"
			"         return $classname_token$.Parser.ParseFrom(message.Body.Bytes);\n"
			"      }\n"
			"\n"
			"      /// <summary>\n"
			"      /// Allows user code to alter _header as part of the mapping process and before it is published.\n"
			"      /// <param name=\"request\">The request that has been serialized for sending.  May contain information needed to make custom alterations to the header</param>\n"
			"      /// </summary>\n"
			"      partial void AlterMessageHeader($classname_token$ request);\n"
			"   }\n"
			"}\n"
			"\n";
	}
};




int main(int argc, char *argv[])
{
	BrighterCodeGenerator generator;
	return google::protobuf::compiler::PluginMain(argc, argv, &generator);
}

