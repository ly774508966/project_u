using UnityEngine;
using System.Collections;
using Google.Protobuf;

namespace network
{
	public class Packet
	{

		public int size { get; private set; }

		byte[] buffer;

		Packet(int size)
		{
			buffer = new byte[size];
		}

		public static Packet TryAssemble(comext.utils.ConstRingBuffer ringBuffer)
		{
			var packetSize = ringBuffer.ReadShort();
			if (packetSize <= ringBuffer.length)
			{
				var packet = new Packet(packetSize);
				ringBuffer.Read(packet.buffer, packetSize);
				return packet;
			}
			return null;
		}


	}

}