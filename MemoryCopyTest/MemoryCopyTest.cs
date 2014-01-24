using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Runtime.Serialization;
using MemoryCopy;
using System.Collections;

namespace MemoryCopyTest
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
        Command Command { get; set; }

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
                return (ushort)Items.Length;
            }
            set
            {
                Items = new B[value];
            }
        }

        [DataMember(Order = 2)]
        public B[] Items { get; set; }
    }

    [TestClass]
    public class MemoryCopyTest
    {
        A a;
        B b1;
        B b2;
        C c;
        MemoryCopy.MemoryCopy bigCopy;
        MemoryCopy.MemoryCopy littleCopy;

        [TestInitialize]
        public void Initialize()
        {
            a = new A(Command.C1);
            b1 = new B(Command.C1);
            b1.Reason = 0xAB;
            b2 = new B(Command.C2);
            b2.Reason = 0xCD;
            c = new C();
            c.Length = 2;
            c.Items[0] = b1;
            c.Items[1] = b2;
            bigCopy = new MemoryCopy.MemoryCopy();
            littleCopy = new MemoryCopy.MemoryCopy();
            littleCopy.ByteOrder = ByteOrder.LittleEndian;
        }

        [TestMethod]
        public void TestWrite()
        {
            byte[] dataExpected = new byte[4] { 0, 0, 0, 0xC1 };
            byte[] data = new byte[dataExpected.Length];
            int index = 0;
            bigCopy.Write(a, data, ref index, false);
            Assert.AreEqual(dataExpected.Length, index);
            CollectionAssert.AreEqual(dataExpected, data);
        }

        [TestMethod]
        public void TestWriteInherit1()
        {
            byte[] dataExpected = new byte[3] { 0xB1, 0, 0xAB};
            byte[] data = new byte[dataExpected.Length];
            int index = 0;
            bigCopy.Write(b1, data, ref index, false);
            Assert.AreEqual(dataExpected.Length, index);
            CollectionAssert.AreEqual(dataExpected, data);
        }

        public void TestWriteInherit2()
        {
            byte[] dataExpected = new byte[7] { 0, 0, 0, 0xC1, 0xB1, 0, 0xAB };
            byte[] data = new byte[dataExpected.Length];
            int index = 0;
            bigCopy.Write(b1, data, ref index, true);
            Assert.AreEqual(dataExpected.Length, index);
            CollectionAssert.AreEqual(dataExpected, data);
        }

        [TestMethod]
        public void TestWriteLittleEndian()
        {
            byte[] dataExpected = new byte[7] { 0xC1, 0, 0, 0, 0xB1, 0xAB, 0 };
            byte[] data = new byte[dataExpected.Length];
            int index = 0;
            littleCopy.Write(b1, data, ref index, true);
            Assert.AreEqual(dataExpected.Length, index);
            CollectionAssert.AreEqual(dataExpected, data);
        }

        [TestMethod]
        public void TestWriteArray()
        {
            byte[] dataExpected = new byte[16] { 0, 2, 0, 0, 0, 0xC1, 0xB1, 0, 0xAB, 0, 0, 0, 0xC2, 0xB1, 0, 0xCD };
            byte[] data = new byte[dataExpected.Length];
            int index = 0;
            bigCopy.Write(c, data, ref index, true);
            Assert.AreEqual(dataExpected.Length, index);
            CollectionAssert.AreEqual(dataExpected, data);
        }
    }
}
