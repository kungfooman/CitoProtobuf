using System;
using System.IO;
using System.Collections.Generic;

namespace ProtocolBuffers
{
	public static class ProtoPrepare
	{
		public static void Prepare (Proto proto)
		{
			foreach (Message m in proto.Messages)
				PrepareMessage (m);
		}
		
		static void PrepareMessage (Message m)
		{
			m.Name = ProtoPrepare.GetCamelCase (m.Name);
			//Prepare fields
			foreach (Field f in m.Fields) {
				PrepareProtoType (m, f);
				if (f.Default != null)
					f.Default = GetCSDefaultValue (f);
			}	
			foreach (MessageEnum e in m.Enums) {
				e.Name = GetCamelCase (e.Name);
			}
			foreach (Message sub in m.Messages)
				PrepareMessage (sub);
		}
		
		/// <summary>
		/// Prepare: ProtoType, WireType and CSType
		/// </summary>
		static void PrepareProtoType (Message m, Field f)
		{
			//Change property name to C# style, CamelCase.
			f.Name = GetCSPropertyName (m, f.Name);
			
			f.ProtoType = GetScalarProtoType (f.ProtoTypeName);
						
			//Wire, and set type
			switch (f.ProtoType) {
			case ProtoTypes.Double:
			case ProtoTypes.Fixed64:
			case ProtoTypes.Sfixed64:
				f.WireType = Wire.Fixed64;
				break;
			case ProtoTypes.Float:
			case ProtoTypes.Fixed32:
			case ProtoTypes.Sfixed32:
				f.WireType = Wire.Fixed32;
				break;
			case ProtoTypes.Int32:
			case ProtoTypes.Int64:
			case ProtoTypes.Uint32:
			case ProtoTypes.Uint64:
			case ProtoTypes.Sint32:
			case ProtoTypes.Sint64:
			case ProtoTypes.Bool:
				f.WireType = Wire.Varint;
				break;
			case ProtoTypes.String:
			case ProtoTypes.Bytes:
				f.WireType = Wire.LengthDelimited;
				break;
			default:
				IProtoType pt = GetProtoType (m, f.ProtoTypeName);

				if (pt == null)
					throw new InvalidDataException ("Name not found: " + f.ProtoTypeName);
				if (pt is MessageEnum) {
					f.ProtoType = ProtoTypes.Enum;
					f.WireType = Wire.Varint;
					f.ProtoTypeEnum = (MessageEnum)pt;
				}
				if (pt is Message) {
					f.ProtoType = ProtoTypes.Message;
					f.WireType = Wire.LengthDelimited;
					f.ProtoTypeMessage = (Message)pt;
				}
				if (pt.Parent is Proto)
					f.CSType = "";
				else
					f.CSType = pt.Parent.Name + ".";

				string[] parts = f.ProtoTypeName.Split ('.');
				int n = 0;
				while (true) {
					string cc = GetCamelCase (parts [n]);
					if (n >= parts.Length - 2 && pt is Message) {
						f.CSClass = f.CSType + cc;
						f.CSType += "I" + cc;
						break;
					}
					f.CSType += cc;
					n += 1;
					if (n >= parts.Length)
						break;
					else
						f.CSType += ".";
				}

				break;
			}
			
			if (f.Packed) {
				if (f.WireType == Wire.LengthDelimited)
					throw new InvalidDataException ("Packed field not allowed for length delimited types");
				f.WireType = Wire.LengthDelimited;
			}

			if (f.CSType == null)
			{
				f.CSType = GetCSType (f.ProtoType);
				f.CSClass = f.CSType;
			}
			f.CSItemType = f.CSType;
			if (f.Rule == Rules.Repeated) {
				f.CSType = "List<" + f.CSType + ">";
			}
			
		}
		
		//Search for name.
		static IProtoType GetProtoType (Message m, string path)
		{
			string[] parts = path.Split ('.');
			return SearchMessageUp (m, parts);			
		}
		
		static IProtoType SearchMessageUp (Message p, string[] name)
		{
			if (p is Proto)
				return SearchMessageDown (p, name);
			
			Message m = p as Message;
			if (m.Name == name [0]) {
				if (name.Length == 1)
					return m;
				
				string[] subName = new string[name.Length - 1];
				Array.Copy (name, 1, subName, 0, subName.Length);
				
				return SearchMessageDown (m, subName);
			}
			
			IProtoType down = SearchMessageDown (p, name);
			if (down != null)
				return down;
			
			return SearchMessageUp (m.Parent, name);
		}

		static IProtoType SearchMessageDown (Message p, string[] name)
		{
			if (name.Length == 1) {
				foreach (MessageEnum me in p.Enums) {
					if (me.Name == name [0])
						return me;
				}
			}
			
			foreach (Message sub in p.Messages) {
				if (sub.Name == name [0]) {
					if (name.Length == 1)
						return sub;
					string[] subName = new string[name.Length - 1];
					Array.Copy (name, 1, subName, 0, subName.Length);
					
					return SearchMessageDown (sub, subName);
				}
			}
			
			return null;
		}
		
		
		/// <summary>
		/// Return the type given the name from a .proto file.
		/// Return Unknonw if it is a message or an enum.
		/// </summary>
		static ProtoTypes GetScalarProtoType (string type)
		{
			switch (type) {
			case "double":
				return ProtoTypes.Double;
			case "float":
				return ProtoTypes.Float;
			case "int32":
				return ProtoTypes.Int32;
			case "int64":
				return ProtoTypes.Int64;
			case "uint32":
				return ProtoTypes.Uint32;
			case "uint64":
				return ProtoTypes.Uint64;
			case "sint32":
				return ProtoTypes.Sint32;
			case "sint64":
				return ProtoTypes.Sint64;
			case "fixed32":
				return ProtoTypes.Fixed32;
			case "fixed64":
				return ProtoTypes.Fixed64;
			case "sfixed32":
				return ProtoTypes.Sfixed32;
			case "sfixed64":
				return ProtoTypes.Sfixed64;
			case "bool":
				return ProtoTypes.Bool;
			case "string":
				return ProtoTypes.String;
			case "bytes":
				return ProtoTypes.Bytes;
			default:
				return ProtoTypes.Unknown;
			}
		}
			
		/// <summary>
		/// Gets the c# representation for a given .proto type.
		/// </summary>
		/// <param name='name'>
		/// Name of message or enum, null otherwise
		/// </param>
		static string GetCSType (ProtoTypes type)
		{
			switch (type) {
			case ProtoTypes.Double:
				return "double";
			case ProtoTypes.Float:
				return "float";
			case ProtoTypes.Int32:
			case ProtoTypes.Sint32:
			case ProtoTypes.Sfixed32:
				return "int";
			case ProtoTypes.Int64:
			case ProtoTypes.Sint64:
			case ProtoTypes.Sfixed64:
				return "long";
			case ProtoTypes.Uint32:
			case ProtoTypes.Fixed32:
				return "uint";
			case ProtoTypes.Uint64:
			case ProtoTypes.Fixed64:
				return "ulong";
			case ProtoTypes.Bool:
				return "bool";
			case ProtoTypes.String:
				return "string";
			case ProtoTypes.Bytes:
				return "byte[]";
				
			default:
				throw new NotImplementedException ();
			}
		}
		
		/// <summary>
		/// Get the default value in c# form
		/// </summary>
		static string GetCSDefaultValue (Field f)
		{
			switch (f.ProtoType) {
			case ProtoTypes.Double:
			case ProtoTypes.Float:
			case ProtoTypes.Fixed32:
			case ProtoTypes.Fixed64:
			case ProtoTypes.Sfixed32:
			case ProtoTypes.Sfixed64:
			case ProtoTypes.Int32:
			case ProtoTypes.Int64:
			case ProtoTypes.Uint32:
			case ProtoTypes.Uint64:
			case ProtoTypes.Sint32:
			case ProtoTypes.Sint64:
			case ProtoTypes.Bool:
				return f.Default;
			
			case ProtoTypes.String:
				return f.Default;
				
			case ProtoTypes.Bytes:
				throw new NotImplementedException ();
				
			case ProtoTypes.Enum:	
				return f.CSType + "." + f.Default;
				
			case ProtoTypes.Message:
				throw new InvalidDataException ("Don't think there can be a default for messages");
				
			default:
				throw new NotImplementedException ();
			}
		}
		
		/// <summary>
		/// Gets the C# CamelCase version of a given name.
		/// Name collisions with enums are avoided.
		/// </summary>
		static string GetCSPropertyName (Message m, string name)
		{
			string csname = GetCamelCase (name);	
			
			foreach (MessageEnum me in m.Enums)
				if (me.Name == csname)
					return name;
			
			return csname;			
		}
		
		/// <summary>
		/// Gets the CamelCase version of a given name.
		/// </summary>
		static string GetCamelCase (string name)
		{
			string csname = "";
			
			if (name.Contains ("_") == false)
				csname += name.Substring (0, 1).ToUpperInvariant () + name.Substring (1);
			else {
				foreach (string part in name.Split('_')) {
					csname += part.Substring (0, 1).ToUpperInvariant () + part.Substring (1);
				}
			}		
			
			return csname;			
		}

	}
}
