using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Runtime.Serialization;
using NetMemoryCopy;
using System.Collections;

namespace NetMemoryCopyTest
{
    enum Command : int
    {
        C1 = 0x000000C1,
        C2
    }

    enum Code : byte
    {
        B1 = 0xB1,
        B2
    }

    class A
    {
        [DataMember(Order=1)]
        public Command Command { get; set; }

        public A()
        { }

        public A(Command command)
        {
            Command = command;
        }
    }

    class B : A
    {
        [DataMember(Order = 3)]
        public short Reason { get; set; }

        Code code = Code.B1;

        [DataMember(Order = 2)]
        private Code PrivateCode 
        {
            get 
            {
                return code;
            }
            set
            {
                code = value;
            }
        }

        public Code Code
        {
            get { return code; }
        }

        public B()
        {}

        public B(Command command) : base(command)
        {

        }
    }

    class C
    {
        [DataMember(Order = 1)]
        public ushort Length
        {
            get
            {
                return Items == null ? (ushort)0 : (ushort)Items.Length;
            }
            set
            {
                Items = new B[value];
            }
        }

        [DataMember(Order = 2)]
        public B[] Items { get; set; }

        public C() 
        { }
    }

    struct S
    {
        [DataMember(Order=0)]
        public uint Command { get; private set; }
        [DataMember(Order = 1)]
        public short Value { get; private set; }

        public S(uint command, short value) : this()
        {
            Command = command;
            Value = value;
        }
    }

    [TestClass]
    public class MemoryCopyTest
    {
        A a;
        B b1;
        B b2;
        C c;
        S s;
        byte[] aBigEndian;
        byte[] b1BigEndian;
        byte[] b1BigEndianNoInheritance;
        byte[] b1LittleEndian;
        byte[] cBigEndian;
        byte[] sBigEndian;

        NetMemoryCopy.MemoryCopy bigCopy;
        NetMemoryCopy.MemoryCopy littleCopy;

        [TestInitialize]
        public void Initialize()
        {
            a = new A(Command.C1);
            aBigEndian = new byte[4] { 0, 0, 0, 0xC1 };
            b1 = new B(Command.C1);
            b1.Reason = 0xAB;
            b1BigEndian = new byte[7] { 0, 0, 0, 0xC1, 0xB1, 0, 0xAB };
            b1BigEndianNoInheritance = new byte[3] { 0xB1, 0, 0xAB };
            b1LittleEndian = new byte[7] { 0xC1, 0, 0, 0, 0xB1, 0xAB, 0 };
            b2 = new B(Command.C2);
            b2.Reason = 0xCD;
            c = new C();
            c.Length = 2;
            c.Items[0] = b1;
            c.Items[1] = b2;
            cBigEndian = new byte[16] { 0, 2, 0, 0, 0, 0xC1, 0xB1, 0, 0xAB, 0, 0, 0, 0xC2, 0xB1, 0, 0xCD };
            s = new S(0x00AB00CD, -255);
            sBigEndian = new byte[6] { 0, 0xAB, 0, 0xCD, 0xFF, 0x01 };

            bigCopy = new NetMemoryCopy.MemoryCopy();
            littleCopy = new NetMemoryCopy.MemoryCopy();
            littleCopy.ByteOrder = ByteOrder.LittleEndian;
        }

        [TestMethod]
        public void TestWrite()
        {
            byte[] data = new byte[aBigEndian.Length];
            int index = 0;
            bigCopy.Write(a, data, ref index, false);
            Assert.AreEqual(aBigEndian.Length, index);
            CollectionAssert.AreEqual(aBigEndian, data);
        }

        [TestMethod]
        public void TestWriteInherit1()
        {
            byte[] data = new byte[b1BigEndianNoInheritance.Length];
            int index = 0;
            bigCopy.Write(b1, data, ref index, false);
            Assert.AreEqual(b1BigEndianNoInheritance.Length, index);
            CollectionAssert.AreEqual(b1BigEndianNoInheritance, data);
        }

        [TestMethod]
        public void TestWriteInherit2()
        {
            byte[] data = new byte[b1BigEndian.Length];
            int index = 0;
            bigCopy.Write(b1, data, ref index, true);
            Assert.AreEqual(b1BigEndian.Length, index);
            CollectionAssert.AreEqual(b1BigEndian, data);
        }

        [TestMethod]
        public void TestWriteLittleEndian()
        {
            byte[] data = new byte[b1LittleEndian.Length];
            int index = 0;
            littleCopy.Write(b1, data, ref index, true);
            Assert.AreEqual(b1LittleEndian.Length, index);
            CollectionAssert.AreEqual(b1LittleEndian, data);
        }

        [TestMethod]
        public void TestWriteArray()
        {
            byte[] data = new byte[cBigEndian.Length];
            int index = 0;
            bigCopy.Write(c, data, ref index, true);
            Assert.AreEqual(cBigEndian.Length, index);
            CollectionAssert.AreEqual(cBigEndian, data);
        }

        [TestMethod]
        public void TestWriteStruct()
        {
            byte[] data = new byte[sBigEndian.Length];
            int index = 0;
            bigCopy.Write(s, data, ref index, true);
            Assert.AreEqual(sBigEndian.Length, index);
            CollectionAssert.AreEqual(sBigEndian, data);
        }

        [TestMethod]
        public void TestRead()
        {
            int index = 0;
            A copy = (A)bigCopy.Read(typeof(A), aBigEndian, ref index, true);
            Assert.AreEqual(aBigEndian.Length, index);
            Assert.AreEqual(a.Command, copy.Command);
        }

        [TestMethod]
        public void TestReadInherit1()
        {
            int index = 0;
            B copy = (B)bigCopy.Read(typeof(B), b1BigEndianNoInheritance, ref index, false);
            Assert.AreEqual(b1BigEndianNoInheritance.Length, index);
            Assert.AreNotEqual(b1.Command, copy.Command);
            Assert.AreEqual(b1.Code, copy.Code);
            Assert.AreEqual(b1.Reason, copy.Reason);
        }

        [TestMethod]
        public void TestReadInherit2()
        {
            int index = 0;
            B copy = (B)bigCopy.Read(typeof(B), b1BigEndian, ref index, true);
            Assert.AreEqual(b1BigEndian.Length, index);
            Assert.AreEqual(b1.Command, copy.Command);
            Assert.AreEqual(b1.Code, copy.Code);
            Assert.AreEqual(b1.Reason, copy.Reason);
        }

        [TestMethod]
        public void TestReadLittleEndian()
        {
            int index = 0;
            B copy = (B)littleCopy.Read(typeof(B), b1LittleEndian, ref index, true);
            Assert.AreEqual(b1LittleEndian.Length, index);
            Assert.AreEqual(b1.Command, copy.Command);
            Assert.AreEqual(b1.Code, copy.Code);
            Assert.AreEqual(b1.Reason, copy.Reason);
        }

        [TestMethod]
        public void TestReadArray()
        {
            int index = 0;
            C copy = (C)bigCopy.Read(typeof(C), cBigEndian, ref index, true);
            Assert.AreEqual(cBigEndian.Length, index);
            Assert.AreEqual(c.Length, copy.Length);
            Assert.AreEqual(c.Items[0].Command, copy.Items[0].Command);
            Assert.AreEqual(c.Items[0].Code, copy.Items[0].Code);
            Assert.AreEqual(c.Items[0].Reason, copy.Items[0].Reason);
            Assert.AreEqual(c.Items[1].Command, copy.Items[1].Command);
            Assert.AreEqual(c.Items[1].Code, copy.Items[1].Code);
            Assert.AreEqual(c.Items[1].Reason, copy.Items[1].Reason);
        }

        [TestMethod]
        public void TestReadStruct()
        {
            int index = 0;
            S copy = (S)bigCopy.Read(typeof(S), sBigEndian, ref index, true);
            Assert.AreEqual(sBigEndian.Length, index);
            Assert.AreEqual(s.Command, copy.Command);
            Assert.AreEqual(s.Value, copy.Value);
        }
    }
}
