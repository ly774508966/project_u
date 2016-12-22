using UnityEngine;
using System.Collections;
using NUnit.Framework;

namespace comext.test
{
	public class TestUtils
	{

		// Use this for initialization
		[Test]
		public void TestRingBuffer () {

			var ringBuffer = new comext.utils.RingBuffer(10);
			Assert.AreEqual(0, ringBuffer.length);
			Assert.AreEqual(10, ringBuffer.remaining);

			ringBuffer.Write(new byte[] { 1 }, 1);
			Assert.AreEqual(1, ringBuffer.length);
			Assert.AreEqual(9, ringBuffer.remaining);
			var ret = ringBuffer.ReadByte();
			Assert.AreEqual(1, ret);
			Assert.AreEqual(0, ringBuffer.length);
			Assert.AreEqual(10, ringBuffer.remaining);

			ringBuffer.Write(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 10);
			Assert.AreEqual(10, ringBuffer.length);
			Assert.AreEqual(0, ringBuffer.remaining);

			ringBuffer.Write(new byte[] { 11 }, 1);
			Assert.AreEqual(11, ringBuffer.length);

			for (int i = 1; i <= 11; ++i)
			{
				ret = ringBuffer.ReadByte();
				Assert.AreEqual(i, ret);
			}

			try
			{
				ringBuffer.ReadByte();
				Assert.Fail("Never get here");
			}
			catch (System.Exception e)
			{
				Assert.True(e is comext.utils.RingBuffer.IncompleteReadException);
				Debug.Log(e.Message);
			}
			Assert.AreEqual(0, ringBuffer.length);

			ringBuffer.Write(new byte[] { 99 }, 1);
			Assert.AreEqual(1, ringBuffer.length);
			ret = ringBuffer.ReadByte();
			Assert.AreEqual(99, ret);

			ringBuffer.Write(0x1122334455667788);
			var longRet = ringBuffer.ReadLong();
			Assert.AreEqual(0x1122334455667788, longRet);
		}


	}
}
