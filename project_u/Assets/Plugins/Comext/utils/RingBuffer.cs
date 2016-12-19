using System;

namespace comext.utils
{

	public class RingBuffer
	{
		public class IncompleteReadException : System.Exception
		{
			public IncompleteReadException(int expectedBytes, int actualBytes)
				: base(string.Format("Expected to read {0} byte(s), but {1} byte(s) got.", expectedBytes, actualBytes))
			{
			}
		}


		byte[] buffer;
		int posRead;
		int posWrite;
		int posMark = int.MinValue;

		public int length
		{
			get
			{
				if (posRead <= posWrite)
				{
					return posWrite - posRead;
				}
				else
				{
					return buffer.Length - posRead + posWrite;
				}
			}
		}

		public int remaining
		{
			get
			{
				if (posRead <= posWrite)
				{
					return buffer.Length - posWrite + posRead - 1; // write cannot catch up with read
				}
				else
				{
					return posRead - posWrite - 1;
				}
			}
		}

		float growFactor = 1.2f;
		public RingBuffer(int size, float sizeGrowFactor = 1.2f)
		{
			buffer = new byte[size+1];
			posRead = posWrite = 0;
			growFactor = sizeGrowFactor;
		}

		public void Write(byte[] data, int len)
		{
			CheckExtends(len);

			// write first half
			var toWrite = Math.Min(buffer.Length - posWrite, len);
			Array.Copy(data, 0, buffer, posWrite, toWrite);
			posWrite = (posWrite + len) % buffer.Length;

			// write the rest
			var writeSizeFromHead = Math.Min(len - toWrite, Math.Max(posRead - 1, 0));
			if (writeSizeFromHead > 0)
			{
				Array.Copy(data, toWrite, buffer, 0, writeSizeFromHead);
				posWrite = writeSizeFromHead;
			}
		}

		public void CheckExtends(int capacity)
		{
			if (remaining < capacity)
			{
				var newBuffer = new byte[(int)((length + capacity) * growFactor) + 1];

				var oldLength = length;

				Read(newBuffer, length);

				buffer = newBuffer;
				posRead = 0;
				posWrite = oldLength;

				if (posMark != int.MinValue)
				{
					posMark = 0;
				}
			}
		}

		public int Advance(int len)
		{
			if (posRead <= posWrite)
			{
				var oldPosRead = posRead;
				posRead = Math.Min(posRead + len, posWrite);
				return posWrite - oldPosRead;
			}
			else
			{
				var sizeFromHead = (posRead + len) % buffer.Length;
				posRead = Math.Min(sizeFromHead, posWrite);
				return (len - sizeFromHead + posRead);
			}
		}

		public int Read(byte[] dst, int len)
		{
			if (posRead <= posWrite)
			{
				var toRead = Math.Min(length, len);
				if (toRead > 0)
				{
					Array.Copy(buffer, posRead, dst, 0, toRead);
					posRead += toRead;
				}
				return toRead;
			}
			else
			{
				if (posRead + len < buffer.Length)
				{
					var toRead = Math.Min(length, len);
					Array.Copy(buffer, posRead, dst, 0, toRead);
					posRead += toRead;
					return toRead;
				}
				else
				{
					var toRead = buffer.Length - posRead;
					Array.Copy(buffer, posRead, dst, 0, toRead);
					posRead = 0;
					var sizeFromHead = Math.Min(len - toRead, posWrite);
					Array.Copy(buffer, posRead, dst, toRead, sizeFromHead);
					posRead += sizeFromHead;
					return toRead + sizeFromHead;
				}
			}
		}

		// 8 bytes buffer for numeric
		byte[] numBuffer = new byte[8];
		public long ReadLong()
		{
			Mark();
			int sz = Read(numBuffer, 8);
			if (sz != 8)
			{
				Unmark(); 
				throw new IncompleteReadException(8, sz);
			}
			RemoveMark();
			return ByteOrder.Ntoh(BitConverter.ToInt64(numBuffer, 0));
		}

		public int ReadInt()
		{
			Mark();
			int sz = Read(numBuffer, 4);
			if (sz != 4)
			{
				Unmark(); 
				throw new IncompleteReadException(4, sz);
			}
			RemoveMark();
			return ByteOrder.Ntoh(BitConverter.ToInt32(numBuffer, 0));
		}

		public short ReadShort()
		{
			Mark();
			int sz = Read(numBuffer, 2);
			if (sz != 2)
			{
				Unmark(); 
				throw new IncompleteReadException(2, sz);
			}
			RemoveMark();
			return ByteOrder.Ntoh(BitConverter.ToInt16(numBuffer, 0));
		}

		public byte ReadByte()
		{
			Mark();
			int sz = Read(numBuffer, 1);
			if (sz != 1)
			{
				Unmark(); 
				throw new IncompleteReadException(1, sz);
			}
			RemoveMark();
			return numBuffer[0];
		}


		public void Write(long value)
		{
			value = ByteOrder.Hton(value);
			numBuffer[0] = (byte)(value & 0xff);
			numBuffer[1] = (byte)((value & 0xff00) >> 8);
			numBuffer[2] = (byte)((value & 0xff0000) >> 16);
			numBuffer[3] = (byte)((value & 0xff000000) >> 24);
			numBuffer[4] = (byte)((value & 0xff00000000) >> 32);
			numBuffer[5] = (byte)((value & 0xff0000000000) >> 40);
			numBuffer[6] = (byte)((value & 0xff000000000000) >> 48);
			numBuffer[7] = (byte)(((ulong)value & 0xff00000000000000) >> 56);
			Write(numBuffer, 8);
		}

		public void Write(int value)
		{
			value = ByteOrder.Hton(value);
			numBuffer[0] = (byte)(value & 0xff);
			numBuffer[1] = (byte)((value & 0xff00) >> 8);
			numBuffer[2] = (byte)((value & 0xff0000) >> 16);
			numBuffer[3] = (byte)((value & 0xff000000) >> 24);
			Write(numBuffer, 4);
		}

		public void Write(short value)
		{
			value = ByteOrder.Hton(value);
			numBuffer[0] = (byte)(value & 0xff);
			numBuffer[1] = (byte)((value & 0xff00) >> 8);
			Write(numBuffer, 2);
		}

		public void Write(byte value)
		{
			numBuffer[0] = value;
			Write(numBuffer, 2);
		}


		public void Mark()
		{
			posMark = posRead;
		}

		public void Unmark()
		{
			if (posMark != int.MinValue)
			{
				posRead = posMark;
				posMark = int.MinValue;
			}
		}

		public void RemoveMark()
		{
			posMark = int.MinValue;
		}




	}

	public class ConstRingBuffer : System.IDisposable
	{
		public static ConstRingBuffer Create(RingBuffer ringBuffer)
		{
			ringBuffer.Mark();
			return new ConstRingBuffer(ringBuffer);
		}


		public int length
		{
			get
			{
				return ringBuffer.length;
			}

		}
	

		ConstRingBuffer(RingBuffer ringBuffer)
		{
			this.ringBuffer = ringBuffer;
		}

		RingBuffer ringBuffer;
		public int Read(byte[] dst, int len)
		{
			return ringBuffer.Read(dst, len);
		}

		public long ReadLong()
		{
			return ringBuffer.ReadLong();
		}

		public int ReadInt()
		{
			return ringBuffer.ReadInt();
		}

		public short ReadShort()
		{
			return ringBuffer.ReadShort();
		}

		public byte ReadByte()
		{
			return ringBuffer.ReadByte();
		}

		public void Dispose()
		{
			ringBuffer.Unmark();
		}
	}




}
