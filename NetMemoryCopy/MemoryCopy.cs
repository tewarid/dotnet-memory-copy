using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace NetMemoryCopy
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
        private ByteOrder byteOrder = ByteOrder.BigEndian;

        /// <summary>
        /// Set/retrieve byte order for primitive types.
        /// </summary>
        public ByteOrder ByteOrder 
        {
            get
            {
                return byteOrder;
            }
            set
            {
                byteOrder = value;
            }
        }

        /// <summary>
        /// Build an object of the specified type by reading data from the 
        /// provided byte array. The types should provide a default constructor.
        /// Type can be a primitive type. If type is not a primitive type, 
        /// its class should decorate its properties using the
        /// DataMemberAttribute. The Order property of that attribute can
        /// be used to determine the order in which values will be read.
        /// 
        /// Supports most primitive types listed at 
        /// http://msdn.microsoft.com/en-us/library/eahchzkf.aspx and enums, 
        /// except floating point types.
        /// 
        /// If type is an array of primitive types, it should be initialized when 
        /// its size is read from serialized data.
        /// 
        /// Text data cannot be read directly. Read it as an array of bytes and
        /// handle it appropriately.
        /// 
        /// </summary>
        /// <param name="t">Type of object to build</param>
        /// <param name="data">Serialized data to read</param>
        /// <param name="startIndex">Index to start reading from. Its value 
        /// points to the byte after the last byte read upon return.</param>
        /// <param name="inherit">Causes inherited properties to be read when true.</param>
        /// <returns>object of specified type read from the stream</returns>
        public async Task<object> Read(Type t, Stream stream, bool inherit = true)
        {
            object o;
            byte[] bytes;

            if (t.IsPrimitive)
            {
                if (t == typeof(short))
                {
                    bytes = await ReadBytes(stream, byteOrder, sizeof(short));
                    o = BitConverter.ToInt16(bytes, 0);
                }
                else if (t == typeof(ushort))
                {
                    bytes = await ReadBytes(stream, byteOrder, sizeof(ushort));
                    o = BitConverter.ToUInt16(bytes, 0);
                }
                else if (t == typeof(int))
                {
                    bytes = await ReadBytes(stream, byteOrder, sizeof(int));
                    o = BitConverter.ToInt32(bytes, 0);
                }
                else if (t == typeof(uint))
                {
                    bytes = await ReadBytes(stream, byteOrder, sizeof(uint));
                    o = BitConverter.ToUInt32(bytes, 0);
                }
                else if (t == typeof(byte))
                {
                    o = stream.ReadByte();
                }
                else if (t == typeof(long))
                {
                    bytes = await ReadBytes(stream, byteOrder, sizeof(long));
                    o = BitConverter.ToInt64(bytes, 0);
                }
                else if (t == typeof(ulong))
                {
                    bytes = await ReadBytes(stream, byteOrder, sizeof(ulong));
                    o = BitConverter.ToUInt64(bytes, 0);
                }
                else
                {
                    throw new NotSupportedException("Type not supported: " + t);
                }
                return o;
            }

            // Not a primitive type

            o = Activator.CreateInstance(t);

            IList<PropertyInfo> properties = GetProperties(o, inherit);
            foreach (PropertyInfo property in properties)
            {
                object pVal;
                Type pType;

                if (property.PropertyType.IsEnum)
                {
                    pVal = property.GetValue(o, null);
                    pType = Enum.GetUnderlyingType(property.PropertyType);
                }
                else
                {
                    pVal = property.GetValue(o, null);
                    pType = pVal.GetType();
                }

                if (pType.IsPrimitive)
                {
                    property.SetValue(o, Convert.ChangeType(await Read(pType,
                        stream, inherit), pType), null);
                }
                else if (pVal is byte[])
                {
                    int len = ((byte[])pVal).Length;
                    await ReadBytes(stream, (byte[])pVal);
                }
                else
                {
                    Array a = pVal as Array;
                    if (a != null)
                    {
                        for (int i = 0; i < ((Array)pVal).Length; i++)
                        {
                            a.SetValue(await Read(a.GetType().GetElementType(),
                                stream, inherit), i);
                        }
                    }
                    else
                    {
                        throw new NotSupportedException("Type not supported: " + pType);
                    }
                }
            }

            return o;
        }

        /// <summary>
        /// Serializes an object into the specified byte array. See Read method for
        /// further details.
        /// </summary>
        /// <param name="o">Object to serialize.</param>
        /// <param name="data">Byte array where serialized data will be written.</param>
        /// <param name="startIndex">Index from which to start writing data.</param>
        /// <param name="inherit">Determines whether inherited properties should be serialized.</param>
        public async Task Write(object o, Stream stream, bool inherit = true)
        {
            byte[] bytes;

            if (o.GetType().IsPrimitive)
            {
                if (o is short)
                {
                    bytes = BitConverter.GetBytes((short)o);
                    ReverseBytes(bytes, byteOrder);
                    await stream.WriteAsync(bytes, 0, sizeof(short));
                }
                else if (o is ushort)
                {
                    bytes = BitConverter.GetBytes((ushort)o);
                    ReverseBytes(bytes, byteOrder);
                    await stream.WriteAsync(bytes, 0, sizeof(ushort));
                }
                else if (o is int)
                {
                    bytes = BitConverter.GetBytes((int)o);
                    ReverseBytes(bytes, byteOrder);
                    await stream.WriteAsync(bytes, 0, sizeof(int));
                }
                else if (o is uint)
                {
                    bytes = BitConverter.GetBytes((uint)o);
                    ReverseBytes(bytes, byteOrder);
                    await stream.WriteAsync(bytes, 0, sizeof(uint));
                }
                else if (o is long)
                {
                    bytes = BitConverter.GetBytes((long)o);
                    ReverseBytes(bytes, byteOrder);
                    await stream.WriteAsync(bytes, 0, sizeof(long));
                }
                else if (o is ulong)
                {
                    bytes = BitConverter.GetBytes((ulong)o);
                    ReverseBytes(bytes, byteOrder);
                    await stream.WriteAsync(bytes, 0, sizeof(ulong));
                }
                else if (o is byte)
                {
                    stream.WriteByte((byte)o);
                }
                else
                {
                    throw new NotSupportedException("Type not supported: " + o.GetType());
                }
                return;
            }

            IList<PropertyInfo> properties = GetProperties(o, inherit);

            foreach (PropertyInfo property in properties)
            {
                object pVal;
                Type pType;

                if (property.PropertyType.IsEnum)
                {
                    pType = Enum.GetUnderlyingType(property.PropertyType);
                    pVal = Convert.ChangeType(property.GetValue(o, null), pType);
                }
                else
                {
                    pVal = property.GetValue(o, null);
                    pType = pVal.GetType();
                }

                if (pType.IsPrimitive)
                {
                    await Write(pVal, stream, inherit);
                }
                else if (pVal is byte[])
                {
                    bytes = (byte[])pVal;
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                }
                else
                {
                    Array a = pVal as Array;
                    if (a != null)
                    {
                        foreach (object item in a)
                        {
                            await Write(item, stream, inherit);
                        }
                    }
                    else
                    {
                        throw new NotSupportedException("Type not supported: " + pType);
                    }
                }
            }
        }

        static Dictionary<string, IList<PropertyInfo>> cache = new Dictionary<string, IList<PropertyInfo>>();

        /// <summary>
        /// Retrieve an ordered list of properties for this object adorned with the
        /// DataMember attribute. The list is ordered by the Order parameter of the
        /// attribute.
        /// </summary>
        /// <param name="o">The object for which to recover the properties for</param>
        /// <param name="inherit">Specifies whether inherited properties should be 
        /// recovered or not.</param>
        /// <returns>An ordered list of properties.</returns>
        private static IList<PropertyInfo> GetProperties(object o, bool inherit)
        {
            Type type = o.GetType();

            IList<PropertyInfo> list;
            string tf = inherit ? "t" : "f";
            cache.TryGetValue(type.FullName + tf, out list);

            if (list != null)
            {
                return list;
            }

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

                if (attribute != null)
                {
                    sFields.Add(((DataMemberAttribute)attribute).Order, property);
                }
            }

            list = sFields.Values;

            cache.Add(type.FullName + tf, list);

            return list;
        }

        private static bool IsDataMemberAttribute(object o)
        {
            return o is DataMemberAttribute;
        }

        private static async Task ReadBytes(Stream stream, byte[] b)
        {
            int read;
            int offset = 0;
            int count = b.Length;
            while (count > 0)
            {
                read = await stream.ReadAsync(b, offset, count);
                if (read == 0)
                {
                    // reached end of stream
                    break;
                }
                offset += read;
                count -= read;
            }
        }

        private static async Task<byte[]> ReadBytes(Stream stream,
            ByteOrder byteOrder, int length)
        {
            byte[] bytes = new byte[length];
            await ReadBytes(stream, bytes);
            if ((byteOrder == ByteOrder.BigEndian && BitConverter.IsLittleEndian) ||
                (byteOrder == ByteOrder.LittleEndian && !BitConverter.IsLittleEndian))
            {
                ReverseBytes(bytes);
            }
            return bytes;
        }

        public static void ReverseBytes(byte[] inArray, ByteOrder byteOrder)
        {
            if ((byteOrder == ByteOrder.BigEndian && BitConverter.IsLittleEndian) ||
                (byteOrder == ByteOrder.LittleEndian && !BitConverter.IsLittleEndian))
            {
                ReverseBytes(inArray);
            }
        }

        public static void ReverseBytes(byte[] inArray)
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
        }
    }
}
