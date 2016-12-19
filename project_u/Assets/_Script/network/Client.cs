using UnityEngine;
using System.Collections;
using System;
using System.Net;
using System.Net.Sockets;

namespace network
{
	public class Client : comext.utils.Singleton<Client>, System.IDisposable
	{
		const int kBufferSize = 2048;
		const int kRingBufferSize = 16384;

		Socket socket;
		comext.utils.RingBuffer ringBuffer = new comext.utils.RingBuffer(kRingBufferSize);

		public event System.Action<Packet> onPacketReceived;
		public event System.Action<bool> onConnectivityChanged;

		public void Dispose()
		{
		}


		public void Connect(string host, ushort port)
		{
			socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			IPAddress ipAddr;
			if (!IPAddress.TryParse(host, out ipAddr))
			{
				ipAddr = Dns.GetHostEntry(host).AddressList[0];
			}
			var remoteEp = new IPEndPoint(ipAddr, port);
			socket.BeginConnect(remoteEp, ConnectCallback, null);
		}


		void SetConnectivityChanged(bool connected)
		{
			if (onConnectivityChanged != null)
			{
				onConnectivityChanged(connected);
			}
		}

		void ConnectCallback(System.IAsyncResult ar)
		{
			try
			{
				socket.EndConnect(ar);
				if (socket.Connected)
				{
					SetConnectivityChanged(true);
					StartReceive();
				}
			}
			catch (Exception e)
			{
				SetConnectivityChanged(false);
				Debug.Log(e.Message);
			}
		}

		class StateObject
		{
			public Socket socket;
			public byte[] buffer;
			public int bufferSize;

			public StateObject(Socket socket, int bufferSize = kBufferSize)
			{
				this.socket = socket;
				this.buffer = new byte[bufferSize];
				this.bufferSize = bufferSize;
			}
		}

		void StartReceive()
		{
			var obj = new StateObject(socket);
			socket.BeginReceive(obj.buffer, 0, obj.bufferSize, SocketFlags.None, ReceiveCallback, obj);
		}

		void ReceiveCallback(System.IAsyncResult ar)
		{
			try
			{
				var obj = (StateObject)ar.AsyncState;
				var bytesRead = obj.socket.EndReceive(ar);
				if (bytesRead > 0)
				{
					ringBuffer.Write(obj.buffer, bytesRead);
					var constRingBuffer = comext.utils.ConstRingBuffer.Create(ringBuffer);
					var packet = Packet.TryAssemble(constRingBuffer);
					constRingBuffer.Dispose();
					if (packet != null)
					{
						ringBuffer.Advance(packet.size);
						if (onPacketReceived != null)
						{
							onPacketReceived(packet);
						}
					}
				}
				if (obj.socket.Connected && bytesRead > 0)
				{
					obj.socket.BeginReceive(obj.buffer, 0, obj.buffer.Length, SocketFlags.None, ReceiveCallback, obj);
				}
				else
				{
					SetConnectivityChanged(false);
				}
			}
			catch (Exception e)
			{
				SetConnectivityChanged(false);
				Debug.LogError(e.Message);
			}
		}
	}
}
