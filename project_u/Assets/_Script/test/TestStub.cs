using UnityEngine;
using System.Collections;

public class TestStub : MonoBehaviour {

	// Use this for initialization
	void Start () {

		var ringBuffer = new comext.utils.RingBuffer(10);
		Debug.Assert(ringBuffer.length == 0);
		Debug.Assert(ringBuffer.remaining == 10);
		ringBuffer.Write(new byte[] { 1 }, 1);
		Debug.Assert(ringBuffer.length == 1);
		Debug.Assert(ringBuffer.remaining == 9);
		var ret = ringBuffer.ReadByte();
		Debug.Assert(ret == 1);
		Debug.Assert(ringBuffer.length == 0);
		Debug.Assert(ringBuffer.remaining == 10);

		ringBuffer.Write(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 10);
		Debug.Assert(ringBuffer.length == 10);
		Debug.Assert(ringBuffer.remaining == 0);

		ringBuffer.Write(new byte[] { 11 }, 1);
		Debug.Assert(ringBuffer.length == 11);

		for (int i = 1; i <= 11; ++i)
		{
			ret = ringBuffer.ReadByte();
			Debug.Assert(ret == i);
		}

		try
		{
			ringBuffer.ReadByte();
			Debug.Assert(false, "Never get here");
		}
		catch (System.Exception e)
		{
			Debug.Assert(e is comext.utils.RingBuffer.IncompleteReadException);
			Debug.Log(e.Message);
		}
		Debug.Assert(ringBuffer.length == 0);

		ringBuffer.Write(new byte[] { 99 }, 1);
		Debug.Assert(ringBuffer.length == 1);
		ret = ringBuffer.ReadByte();
		Debug.Assert(ret == 99);


		ringBuffer.Write(0x1122334455667788);
		var longRet = ringBuffer.ReadLong();
		Debug.Assert(longRet == 0x1122334455667788);
	}


}
