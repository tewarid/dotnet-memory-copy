using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace MemoryCopy
{
    public enum ByteOrder
    {
        /// <summary>
        /// Determine byte order from input
        /// </summary>
        Determine = 0,
        /// <summary>
        /// Byte order is little endian
        /// </summary>
        LittleEndian,
        /// <summary>
        /// Byte order is big endian
        /// </summary>
        BigEndian
    }

    public sealed class MemoryCopy
    {
        /// <summary>
        /// Build an object of the specified type by reading data from the 
        /// provided byte array. The type should provide a default constructor.
        /// If the type has array objects, they should be instantiated when 
        /// their size is read from serialized data.
        /// </summary>
        /// <param name="t">Type of object to build</param>
        /// <param name="data">Serialized data to read</param>
        /// <param name="startIndex">Index to start reading from. Its value 
        /// points to the byte after the last byte read upon return.</param>
        /// <param name="byteOrder">Byte order of serialized data</param>
        /// <param name="inherit">Causes inherited properties to be read when true.</param>
        /// <returns></returns>
        public static object Read(Type t, byte[] data, ref int startIndex,
            ByteOrder byteOrder, bool inherit)
        {
            object o = Activator.CreateInstance(t);
            IList<PropertyInfo> properties = GetProperties(o, inherit);
            foreach (PropertyInfo property in properties)
            {
                byte[] bytes;

                object val;

                if (property.PropertyType.IsEnum)
                {
                    val = GetValue(property.PropertyType,
                        property.GetValue(o, null));
                }
                else
                {
                    val = property.GetValue(o, null);
                }

                if (val is int)
                {
                    bytes = ExtractBytes(data, startIndex, byteOrder, 4);
                    property.SetValue(o, BitConverter.ToInt32(bytes, 0), null);
                    startIndex += 4;
                }
                else if (val is uint)
                {
                    bytes = ExtractBytes(data, startIndex, byteOrder, 4);
                    property.SetValue(o, BitConverter.ToUInt32(bytes, 0), null);
                    startIndex += 4;
                }
                else if (val is short)
                {
                    bytes = ExtractBytes(data, startIndex, byteOrder, 2);
                    property.SetValue(o, BitConverter.ToInt16(bytes, 0), null);
                    startIndex += 2;
                }
                else if (val is ushort)
                {
                    bytes = ExtractBytes(data, startIndex, byteOrder, 2);
                    property.SetValue(o, BitConverter.ToUInt16(bytes, 0), null);
                    startIndex += 2;
                }
                else if (val is byte)
                {
                    property.SetValue(o, data[startIndex], null);
                    startIndex++;
                }
                else if (val is byte[])
                {
                    int len = ((byte[])val).Length;
                    Array.Copy(data, startIndex, (byte[])val, 0, len);
                    startIndex += len;
                }
                else if (val is string)
                {
                    if (data.Length > 1 && startIndex < data.Length)
                    {
                        System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
                        string str = enc.GetString(data, startIndex, data.Length - 1 - startIndex);
                        property.SetValue(o, (str.Contains("\0")) ? str.Remove(str.IndexOf('\0', 0)) : str, null);
                    }
                    else
                    {
                        property.SetValue(o, null, null);
                    }
                    startIndex++;
                }
                else if (val is Array && val.GetType().GetElementType().IsPrimitive)
                {
                    for (int i = 0; i < ((Array)val).Length; i++)
                    {
                        // TODO assuming uint
                        bytes = ExtractBytes(data, startIndex, byteOrder, 4);
                        ((Array)val).SetValue(BitConverter.ToUInt32(bytes, 0), i);
                        startIndex += 4;
                    }
                }
                else if (val is Array)
                {
                    Array a = (Array)val;
                    for (int i = 0; i < ((Array)val).Length; i++)
                    {
                        a.SetValue(Read(a.GetValue(i).GetType(), data, ref startIndex, 
                            byteOrder, inherit), i);
                    }
                }
            }
            return o;
        }

        /// <summary>
        /// Serializes an object into the specified byte array.
        /// </summary>
        /// <param name="o">Object to serialize.</param>
        /// <param name="data">Byte array where serialized data will be written.</param>
        /// <param name="startIndex">Index from which to start writing data.</param>
        /// <param name="byteOrder">Byte order.</param>
        /// <param name="inherit">Determines whether inherited properties should be serialized.</param>
        public static void Write(object o, byte[] data, ref int startIndex,
            ByteOrder byteOrder, bool inherit)
        {
            IList<PropertyInfo> properties = GetProperties(o, inherit);

            foreach (PropertyInfo property in properties)
            {
                byte[] bytes;

                object val = null;

                if (property.PropertyType.IsEnum)
                {
                    val = GetValue(property.PropertyType, property.GetValue(o, null));
                }
                else
                {
                    val = property.GetValue(o, null);
                }

                if (val is int)
                {
                    bytes = BitConverter.GetBytes((int)val);
                    ReverseBytes(bytes, byteOrder);
                    Array.Copy(bytes, 0, data, startIndex, 4);
                    startIndex += 4;
                }
                else if (val is uint)
                {
                    bytes = BitConverter.GetBytes((uint)val);
                    ReverseBytes(bytes, byteOrder);
                    Array.Copy(bytes, 0, data, startIndex, 4);
                    startIndex += 4;
                }
                else if (val is ushort)
                {
                    bytes = BitConverter.GetBytes((ushort)val);
                    ReverseBytes(bytes, byteOrder);
                    Array.Copy(bytes, 0, data, startIndex, 2);
                    startIndex += 2;
                }
                else if (val is byte)
                {
                    data[startIndex] = (byte)val;
                    startIndex++;
                }
                else if (val is byte[])
                {
                    bytes = (byte[])val;
                    Array.Copy(bytes, 0, data, startIndex, bytes.Length);
                    startIndex += bytes.Length;
                }
                else if (val is Array && val.GetType().GetElementType().IsPrimitive)
                {
                    foreach (object item in (Array)val)
                    {
                        // assuming uint
                        bytes = BitConverter.GetBytes((uint)item);
                        ReverseBytes(bytes, byteOrder);
                        Array.Copy(bytes, 0, data, startIndex, 4);
                        startIndex += 4;
                    }
                }
                else if (val is Array)
                {
                    foreach (object item in (Array)val)
                    {
                        Write(item, data, ref startIndex, byteOrder, false);
                    }
                }
            }
        }

        private static IList<PropertyInfo> GetProperties(object o, bool inherit)
        {
            Type type = o.GetType();
            List<PropertyInfo> properties = new List<PropertyInfo>();

            if (inherit)
            {
                Type baseType = type.BaseType;
                while (baseType != null)
                {
                    properties.InsertRange(0, baseType.GetProperties(BindingFlags.NonPublic
                        | BindingFlags.Public | BindingFlags.DeclaredOnly
                        | BindingFlags.Instance));
                    baseType = baseType.BaseType;
                }
            }

            properties.InsertRange(0, type.GetProperties(BindingFlags.NonPublic
                | BindingFlags.Public | BindingFlags.DeclaredOnly
                | BindingFlags.Instance));

            SortedList<int, PropertyInfo> sFields = new SortedList<int, PropertyInfo>();

            foreach (PropertyInfo property in properties)
            {
                object[] attributes = property.GetCustomAttributes(false);
                object attribute = Array.Find(attributes, IsDataMemberAttribute);

                if (attribute == null) continue;
                else sFields.Add(((DataMemberAttribute)attribute).Order, property);
            }
            return sFields.Values;
        }

        private static bool IsDataMemberAttribute(object o)
        {
            return o is DataMemberAttribute;
        }

        private static object GetValue(Type enumType, object enumVal)
        {
            Type t = Enum.GetUnderlyingType(enumType);

            object val = null;

            if (t == typeof(int))
            {
                val = (int)enumVal;
            }
            else if (t == typeof(uint))
            {
                val = (uint)enumVal;
            }
            else if (t == typeof(ushort))
            {
                val = (ushort)enumVal;
            }
            else if (t == typeof(byte))
            {
                val = (byte)enumVal;
            }
            return val;
        }

        public static byte[] ExtractBytes(byte[] data, int startIndex,
            ByteOrder byteOrder, int length)
        {
            byte[] bytes = new byte[length];
            Array.Copy(data, startIndex, bytes, 0, length);
            if ((byteOrder == ByteOrder.BigEndian && BitConverter.IsLittleEndian) ||
                (byteOrder == ByteOrder.LittleEndian && !BitConverter.IsLittleEndian))
            {
                return ReverseBytes(bytes);
            }
            return bytes;
        }

        public static byte[] ReverseBytes(byte[] inArray, ByteOrder byteOrder)
        {
            if ((byteOrder == ByteOrder.BigEndian && BitConverter.IsLittleEndian) ||
                (byteOrder == ByteOrder.LittleEndian && !BitConverter.IsLittleEndian))
            {
                ReverseBytes(inArray);
            }

            return inArray;
        }

        public static byte[] ReverseBytes(byte[] inArray)
        {
            byte temp;
            int highCtr = inArray.Length - 1;

            for (int ctr = 0; ctr < inArray.Length / 2; ctr++)
            {
                temp = inArray[ctr];
                inArray[ctr] = inArray[highCtr];
                inArray[highCtr] = temp;
                highCtr -= 1;
            }
            return inArray;
        }
    }
}
