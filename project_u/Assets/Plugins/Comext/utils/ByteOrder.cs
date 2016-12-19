using UnityEngine;
using System.Collections;
using System.Net;

namespace comext.utils
{
	public class ByteOrder
	{
		public static long Ntoh(long value)
		{
			return IPAddress.NetworkToHostOrder(value);
		}

		public static int Ntoh(int value)
		{
			return IPAddress.NetworkToHostOrder(value);
		}

		public static short Ntoh(short value)
		{
			return IPAddress.NetworkToHostOrder(value);
		}

		public static long Hton(long value)
		{
			return IPAddress.HostToNetworkOrder(value);
		}

		public static int Hton(int value)
		{
			return IPAddress.HostToNetworkOrder(value);
		}

		public static short Hton(short value)
		{
			return IPAddress.HostToNetworkOrder(value);
		}
	}
}
