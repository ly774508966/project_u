/*
MIT License

Copyright (c) 2016 xiaobin83

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
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
