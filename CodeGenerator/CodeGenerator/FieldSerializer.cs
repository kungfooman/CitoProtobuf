using System;
using System.Collections.Generic;

namespace SilentOrbit.ProtocolBuffers
{
    static class FieldSerializer
    {
        #region Reader
        /// <summary>
        /// Return true for normal code and false for generated throw.
        /// In the later case a break is not needed to be generated afterwards.
        /// </summary>
        public static bool FieldReader(Field f, CodeWriter cw)
        {
            if (f.Rule == FieldRule.Repeated)
            {
                //Make sure we are not reading a list of interfaces
                if (f.ProtoType.OptionType == "interface")
                {
                    cw.WriteLine("throw new InvalidOperationException(\"Can't deserialize a list of interfaces\");");
                    return false;
                }

                if (f.OptionPacked == true)
                {
                    //TODO: read without buffering
                    cw.Comment("repeated packed");
                    cw.WriteLine("var ms" + f.ID + " = new CitoMemoryStream(ProtocolParser.ReadBytes(stream));");
                    if (f.IsUsingBinaryWriter)
                        cw.WriteLine("BinaryReader br" + f.ID + " = new BinaryReader(ms" + f.ID + ");");
                    cw.WhileBracket("ms" + f.ID + ".Position < ms" + f.ID + ".Length()");
                    cw.WriteLine("instance." + f.CsName + "Add(" + FieldReaderType(f, "ms" + f.ID, "br" + f.ID, null) + ");");
                    //cw.EndBracket();
                    cw.EndBracket();
                } else
                {
                    cw.Comment("repeated");
                    cw.WriteLine("instance." + f.CsName + "Add(" + FieldReaderType(f, "stream", "br", null) + ");");
                }
            } else
            {   
                if (f.OptionReadOnly)
                {
                    //The only "readonly" fields we can modify
                    //We could possibly support bytes primitive too but it would require the incoming length to match the wire length
                    if (f.ProtoType is ProtoMessage)
                    {
                        cw.WriteLine(FieldReaderType(f, "stream", "br", "instance." + f.CsName) + ";");
                        return true;
                    }
                    cw.WriteLine("throw new InvalidOperationException(\"Can't deserialize into a readonly primitive field\");");
                    return false;
                }
                
                if (f.ProtoType is ProtoMessage)
                {
                    if (f.ProtoType.OptionType == "struct")
                    {
                        cw.WriteLine(FieldReaderType(f, "stream", "br", "ref instance." + f.CsName) + ";");
                        return true;
                    }

                    cw.WriteLine("if (instance." + f.CsName + " == null)");
                    if (f.ProtoType.OptionType == "interface")
                        cw.WriteIndent("throw new InvalidOperationException(\"Can't deserialize into a interfaces null pointer\");");
                    else
                        cw.WriteIndent("instance." + f.CsName + " = " + FieldReaderType(f, "stream", "br", null) + ";");
                    cw.WriteLine("else");
                    cw.WriteIndent(FieldReaderType(f, "stream", "br", "instance." + f.CsName) + ";");
                    return true;
                } 

                cw.WriteLine("instance." + f.CsName + " = " + FieldReaderType(f, "stream", "br", "instance." + f.CsName) + ";");
            }
            return true;
        }

        static string FieldReaderType(Field f, string stream, string binaryReader, string instance)
        {
            if (f.OptionCodeType != null)
            {
                switch (f.OptionCodeType)
                {
                    case "DateTime":
                        switch (f.ProtoType.ProtoName)
                        {
                            case ProtoBuiltin.UInt64:
                            case ProtoBuiltin.Int64:
                            case ProtoBuiltin.Fixed64:
                            case ProtoBuiltin.SFixed64:
                                return "new DateTime((long)" + FieldReaderPrimitive(f, stream, binaryReader, instance) + ")";
                        }
                        throw new FormatException("Local feature, DateTime, must be stored in a 64 bit field");

                    case "TimeSpan":
                        switch (f.ProtoType.ProtoName)
                        {
                            case ProtoBuiltin.UInt64:
                            case ProtoBuiltin.Int64:
                            case ProtoBuiltin.Fixed64:
                            case ProtoBuiltin.SFixed64:
                                return "new TimeSpan((long)" + FieldReaderPrimitive(f, stream, binaryReader, instance) + ")";
                        }
                        throw new FormatException("Local feature, TimeSpan, must be stored in a 64 bit field");

                    default:
                    //Assume enum
                        return //"(" + f.OptionCodeType + ")" +
                            FieldReaderPrimitive(f, stream, binaryReader, instance);
                }
            }
            
            return FieldReaderPrimitive(f, stream, binaryReader, instance);
        }

        static string FieldReaderPrimitive(Field f, string stream, string binaryReader, string instance)
        {
            if (f.ProtoType is ProtoMessage)
            {
                var m = f.ProtoType as ProtoMessage;
                if (f.Rule == FieldRule.Repeated || instance == null)
                    return m.FullSerializerType + ".DeserializeLengthDelimited(" + stream + ")";
                else
                    return m.FullSerializerType + ".DeserializeLengthDelimited(" + stream + ", " + instance + ")";
            }

            if (f.ProtoType is ProtoEnum)
                return //"(" + f.ProtoType.FullCsType + ")
                    "ProtocolParser.ReadUInt64(" + stream + ")";
            
            if (f.ProtoType is ProtoBuiltin)
            {
                switch (f.ProtoType.ProtoName)
                {
                    case ProtoBuiltin.Double:
                        return binaryReader + ".ReadDouble()";
                    case ProtoBuiltin.Float:
                        return binaryReader + ".ReadSingle()";
                    case ProtoBuiltin.Int32: //Wire format is 64 bit varint
                        return "ProtocolParser.ReadUInt64(" + stream + ")";
                    case ProtoBuiltin.Int64:
                        return "ProtocolParser.ReadUInt64(" + stream + ")";
                    case ProtoBuiltin.UInt32:
                        return "ProtocolParser.ReadUInt32(" + stream + ")";
                    case ProtoBuiltin.UInt64:
                        return "ProtocolParser.ReadUInt64(" + stream + ")";
                    case ProtoBuiltin.SInt32:
                        return "ProtocolParser.ReadZInt32(" + stream + ")";
                    case ProtoBuiltin.SInt64:
                        return "ProtocolParser.ReadZInt64(" + stream + ")";
                    case ProtoBuiltin.Fixed32:
                        return binaryReader + ".ReadUInt32()";
                    case ProtoBuiltin.Fixed64:
                        return binaryReader + ".ReadUInt64()";
                    case ProtoBuiltin.SFixed32:
                        return binaryReader + ".ReadInt32()";
                    case ProtoBuiltin.SFixed64:
                        return binaryReader + ".ReadInt64()";
                    case ProtoBuiltin.Bool:
                        return "ProtocolParser.ReadBool(" + stream + ")";
                    case ProtoBuiltin.String:
                        return "ProtocolParser.ReadString(" + stream + ")";
                    case ProtoBuiltin.Bytes:
                        return "ProtocolParser.ReadBytes(" + stream + ")";
                    default:
                        throw new ProtoFormatException("unknown build in: " + f.ProtoType.ProtoName, f.Source);
                }   

            }

            throw new NotImplementedException();
        }
        #endregion
        #region Writer
        static void KeyWriter(string stream, int id, Wire wire, CodeWriter cw)
        {
            uint n = ((uint)id << 3) | ((uint)wire);
            cw.Comment("Key for field: " + id + ", " + wire);
            //cw.WriteLine("ProtocolParser.WriteUInt32(" + stream + ", " + n + ");");
            VarintWriter(stream, n, cw);
        }

        /// <summary>
        /// Generates writer for a varint value known at compile time
        /// </summary>
        static void VarintWriter(string stream, uint value, CodeWriter cw)
        {
            List<byte> bytes = new List<byte>();

            while (true)
            {
                byte b = (byte)(value & 0x7F);
                value = value >> 7;
                if (value == 0)
                {
                    bytes.Add(b);
                    break;
                }

                //Write part of value
                b |= 0x80;
                bytes.Add(b);
            }

            //Write final byte
            if (bytes.Count == 1)
            {
                cw.WriteLine(stream + ".WriteByte(" + bytes[0] + ");");
                return;
            }

            string line = stream + ".Write(new byte[]{";
            foreach (byte v in bytes)
                line += v + ", ";
            line = line.TrimEnd(new char[] { ' ', ',' });
            line += "}, 0, " + bytes.Count + ");";
            cw.WriteLine(line);
        }

        /// <summary>
        /// Generates inline writer of a length delimited byte array
        /// </summary>
        static void BytesWriter(string stream, string memoryStream, CodeWriter cw)
        {
            cw.Comment("Length delimited byte array");

            //Original
            //cw.WriteLine("ProtocolParser.WriteBytes(" + stream + ", " + memoryStream + ".ToArray());");

            //Much slower than original
            /*
            cw.WriteLine("ProtocolParser.WriteUInt32(" + stream + ", (uint)" + memoryStream + ".Length);");
            cw.WriteLine(memoryStream + ".Seek(0, System.IO.SeekOrigin.Begin);");
            cw.WriteLine(memoryStream + ".CopyTo(" + stream + ");");
            */

            //Same speed as original
            /*
            cw.WriteLine("ProtocolParser.WriteUInt32(" + stream + ", (uint)" + memoryStream + ".Length);");
            cw.WriteLine(stream + ".Write(" + memoryStream + ".ToArray(), 0, (int)" + memoryStream + ".Length);");
            */

            //10% faster than original using GetBuffer rather than ToArray
            cw.WriteLine("int " + memoryStream + "Length = " + memoryStream + ".Length();");
            cw.WriteLine("ProtocolParser.WriteUInt32(" + stream + ", " + memoryStream + "Length);");
            cw.WriteLine(stream + ".Write(" + memoryStream + ".GetBuffer(), 0, " + memoryStream + "Length);");
        }

        /// <summary>
        /// Generates code for writing one field
        /// </summary>
        public static void FieldWriter(ProtoMessage m, Field f, CodeWriter cw)
        {
            if (f.Rule == FieldRule.Repeated)
            {
                if (f.OptionPacked == true)
                {
                    //Repeated packed
                    cw.IfBracket("instance." + f.CsName + " != null");

                    KeyWriter("stream", f.ID, Wire.LengthDelimited, cw);
                    if (f.ProtoType.WireSize < 0)
                    {
                        //Un-optimized, unknown size
                        cw.WriteLine("var ms" + f.ID + " = new CitoMemoryStream()");
                        if (f.IsUsingBinaryWriter)
                            cw.WriteLine("BinaryWriter bw" + f.ID + " = new BinaryWriter(ms" + f.ID + ");");

                        cw.ForeachBracket("var i" + f.ID + " in instance." + f.CsName);
                        cw.WriteLine(FieldWriterType(f, "ms" + f.ID, "bw" + f.ID, "i" + f.ID));
                        //cw.EndBracket();

                        BytesWriter("stream", "ms" + f.ID, cw);
                        cw.EndBracket();
                    } else
                    {
                        //Optimized with known size
                        //No memorystream buffering, write size first at once

                        //For constant size messages we can skip serializing to the MemoryStream
                        cw.WriteLine("ProtocolParser.WriteUInt32(stream, " + f.ProtoType.WireSize + "u * instance." + f.CsName + ".Count);");

                        cw.ForeachBracket("var i" + f.ID + " in instance." + f.CsName);
                        cw.WriteLine(FieldWriterType(f, "stream", "bw", "i" + f.ID));
                        cw.EndBracket();
                    }
                    cw.EndBracket();
                } else
                {
                    //Repeated not packet
                    cw.IfBracket("instance." + f.CsName + " != null");
                    //cw.ForeachBracket("var i" + f.ID + " in instance." + f.CsName);
                    cw.WriteLine("for(int k=0; k < instance." + f.CsName + "Count; k++)");
                    cw.Bracket();
                    cw.WriteLine("var i" + f.ID + " = instance." + f.CsName + "[k];");
                    KeyWriter("stream", f.ID, f.ProtoType.WireType, cw);
                    cw.WriteLine(FieldWriterType(f, "stream", "bw", "i" + f.ID));
                    cw.EndBracket();
                    cw.EndBracket();
                }
                return;
            } else if (f.Rule == FieldRule.Optional)
            {           
                if (f.ProtoType is ProtoMessage || 
                    f.ProtoType.ProtoName == ProtoBuiltin.String ||
                    f.ProtoType.ProtoName == ProtoBuiltin.Bytes)
                {
                    if (f.ProtoType.Nullable) //Struct always exist, not optional
                        cw.IfBracket("instance." + f.CsName + " != null");
                    KeyWriter("stream", f.ID, f.ProtoType.WireType, cw);
                    cw.WriteLine(FieldWriterType(f, "stream", "bw", "instance." + f.CsName));
                    if (f.ProtoType.Nullable) //Struct always exist, not optional
                        cw.EndBracket();
                    return;
                }
                if (f.ProtoType is ProtoEnum)
                {
                    if (f.OptionDefault != null)
                        cw.IfBracket("instance." + f.CsName + " != " + f.ProtoType.FullCsType + "Enum." + f.OptionDefault);
                    KeyWriter("stream", f.ID, f.ProtoType.WireType, cw);
                    cw.WriteLine(FieldWriterType(f, "stream", "bw", "instance." + f.CsName));
                    if (f.OptionDefault != null)
                        cw.EndBracket();
                    return;
                }
                KeyWriter("stream", f.ID, f.ProtoType.WireType, cw);
                cw.WriteLine(FieldWriterType(f, "stream", "bw", "instance." + f.CsName));
                return;
            } else if (f.Rule == FieldRule.Required)
            {   
                if (f.ProtoType is ProtoMessage && f.ProtoType.OptionType != "struct" || 
                    f.ProtoType.ProtoName == ProtoBuiltin.String ||
                    f.ProtoType.ProtoName == ProtoBuiltin.Bytes)
                {
                    cw.WriteLine("if (instance." + f.CsName + " == null)");
                    cw.WriteIndent("throw new ArgumentNullException(\"" + f.CsName + "\", \"Required by proto specification.\");");
                }
                KeyWriter("stream", f.ID, f.ProtoType.WireType, cw);
                cw.WriteLine(FieldWriterType(f, "stream", "bw", "instance." + f.CsName));
                return;
            }
            throw new NotImplementedException("Unknown rule: " + f.Rule);
        }

        static string FieldWriterType(Field f, string stream, string binaryWriter, string instance)
        {
            if (f.OptionCodeType != null)
            {
                switch (f.OptionCodeType)
                {
                    case "DateTime":
                    case "TimeSpan":
                        return FieldWriterPrimitive(f, stream, binaryWriter, instance + ".Ticks");
                    default: //enum
                        break;
                }
            }
            return FieldWriterPrimitive(f, stream, binaryWriter, instance);
        }

        static string FieldWriterPrimitive(Field f, string stream, string binaryWriter, string instance)
        {

            if (f.ProtoType is ProtoEnum)
                return "ProtocolParser.WriteUInt64(" + stream + "," + instance + ");";

            if (f.ProtoType is ProtoMessage)
            {
                ProtoMessage pm = f.ProtoType as ProtoMessage;
                CodeWriter cw = new CodeWriter();
                cw.WriteLine("var ms" + f.ID + " = new CitoMemoryStream();");
                cw.WriteLine(pm.FullSerializerType + ".Serialize(ms" + f.ID + ", " + instance + ");");
                BytesWriter(stream, "ms" + f.ID, cw);
                //cw.EndBracket();
                return cw.Code;
            }

            switch (f.ProtoType.ProtoName)
            {
                case ProtoBuiltin.Double:
                case ProtoBuiltin.Float:
                case ProtoBuiltin.Fixed32:
                case ProtoBuiltin.Fixed64:
                case ProtoBuiltin.SFixed32:
                case ProtoBuiltin.SFixed64:
                    return binaryWriter + ".Write(" + instance + ");";
                case ProtoBuiltin.Int32: //Serialized as 64 bit varint
                    return "ProtocolParser.WriteUInt64(" + stream + "," + instance + ");";
                case ProtoBuiltin.Int64:
                    return "ProtocolParser.WriteUInt64(" + stream + "," + instance + ");";
                case ProtoBuiltin.UInt32:
                    return "ProtocolParser.WriteUInt32(" + stream + ", " + instance + ");";
                case ProtoBuiltin.UInt64:
                    return "ProtocolParser.WriteUInt64(" + stream + ", " + instance + ");";
                case ProtoBuiltin.SInt32:
                    return "ProtocolParser.WriteZInt32(" + stream + ", " + instance + ");";
                case ProtoBuiltin.SInt64:
                    return "ProtocolParser.WriteZInt64(" + stream + ", " + instance + ");";
                case ProtoBuiltin.Bool:
                    return "ProtocolParser.WriteBool(" + stream + ", " + instance + ");";
                case ProtoBuiltin.String:
                    return "ProtocolParser.WriteBytes(" + stream + ", ProtoPlatform.StringToBytes(" + instance + "));";
                case ProtoBuiltin.Bytes:
                    return "ProtocolParser.WriteBytes(" + stream + ", " + instance + ");";
            }

            throw new NotImplementedException();
        }
        #endregion
    }
}

