using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;

namespace Network
{
	// State object for reading client data asynchronously
	public class Session
	{
		// Client socket.
		private Socket sock = null;
		// Remote socket address
		private EndPoint addr = null;
		// Size of recv buffer.
		private const int RecvBufferSize = 1024 * 16;
		// Size of send buffer.
		private const int SendBufferSize = 1024 * 16;
		// Send buffer.
		private byte[] sendBuffer = new byte[SendBufferSize];
		// Recv buffer.
		private byte[] recvBuffer = new byte[RecvBufferSize];
		// Size of recv buffer.
		private int recvBytes = 0;
		// Size of send buffer.
		private int sendBytes = 0;
		// Send lock object
		private Object sendLock = new object();
		// Recv lock object
		private Object recvLock = new object();
		// user
		private Object user;
		// 会话日志
		public delegate void OnSessionLog_d(string fmt, params object[] args);
		// 收包委托
		public delegate void OnRecvPacket_d(object UserData, BinaryReader PacketReader);
		// 断连委托
		public delegate void OnDisconnect_d(object UserData);
		// 收包事件
		public event OnRecvPacket_d OnRecvPacket;
		// 断连事件
		public event OnDisconnect_d OnDisconnect;
		// 会话事件
		public event OnSessionLog_d OnSessionLog = TraceMsg;
		// 用户数据
		public object UserData { get { return user; } set { user = value; } }
		// 连接状态
		public bool Connected { get { return sock != null ? sock.Connected : false; } }
		// 构造
		public Session()
		{
		}

		// 设置重连地址
		public void Connect(string host, int port, object state)
		{
			try
			{
				if (sock != null && sock.Connected)
					sock.Close();

				sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				addr = new IPEndPoint(IPAddress.Parse(host), port);

				sock.Connect(addr);
			}
			catch (SocketException err)
			{
				MessageBox.Show(err.Message);
			}

			if( sock.Connected )
			{
				user = state;
				sock.BeginReceive(recvBuffer, 0, recvBuffer.Length, 0, new AsyncCallback(OnRecv), sock);
			}
			else
			{
				sock = null;
				addr = null;
				user = null;
			}
		}

		public void Accept(Socket handler, object state)
		{
			sock = handler;
			user = state;

			addr = sock.RemoteEndPoint;
			try
			{
				sock.BeginReceive(recvBuffer, 0, recvBuffer.Length, 0, new AsyncCallback(OnRecv), sock);
			}
			catch( Exception e )
			{
				OnSessionLog(e.Message);
			}
		}

		public void ReConnect()
		{
			try
			{
				if(sock != null && sock.Connected )
					sock.Close();

				if (addr != null)
				{
					sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
					sock.Connect(addr);
				}
			}
			catch (Exception err)
			{
				OnSessionLog(err.Message);
				sock.Close();
			}

			if( sock != null && sock.Connected )
				sock.BeginReceive(recvBuffer, 0, recvBuffer.Length, 0, new AsyncCallback(OnRecv), sock);
		}

		private void OnRecv(IAsyncResult ar)
		{
			Socket s = ar.AsyncState as Socket;
			try
			{
				// Read data from the client socket. 
				int readBytes = s.EndReceive(ar);

				OnSessionLog("Recv Data {0} Bytes, Connected state = {1}", readBytes, s.Connected );

				if (readBytes > 0)
				{
					recvBytes += readBytes;
					BinaryReader br = new BinaryReader(new MemoryStream(recvBuffer, 0, recvBytes));

					ushort packet_len = br.ReadUInt16();
					if (recvBytes >= packet_len)
					{
						byte[] recvPacket = new byte[packet_len];
						Array.Copy(recvBuffer, recvPacket, packet_len);

						recvBytes -= packet_len;
						Array.Copy(recvBuffer, packet_len, recvBuffer, 0, recvBytes);

						if( OnRecvPacket != null )
							Application.Current.Dispatcher.BeginInvoke( OnRecvPacket, user, new BinaryReader(new MemoryStream(recvPacket)) );
					}

					if (s.Connected)
					{
						s.BeginReceive(recvBuffer, recvBytes, recvBuffer.Length - recvBytes, SocketFlags.None, OnRecv, s);
						OnSessionLog("Post Recv {0} - {1}, {2}", recvBytes, recvBuffer.Length, s);
					}
				}
				else
				{
					if (OnDisconnect != null)
						OnDisconnect(user);

					s.Close();
				}
			}
			catch( Exception e )
			{
				OnSessionLog("{0}\n{1}", e.Message, e.StackTrace);

				if ( OnDisconnect != null )
					OnDisconnect( user );

				s.Close();
			}
		}

		public void Send(byte[] Data, int offset, int count)
		{
			try
			{
				lock (sendLock)
				{
					int length = count > 0 ? count : Data.Length;
					Array.Copy(Data, offset, sendBuffer, sendBytes, length);

					sendBytes += length;

					if (sendBytes == length)
					{
						sock.BeginSend(sendBuffer, 0, sendBytes, SocketFlags.None, OnSend, sock);
					}
				}
			}
			catch (Exception e)
			{
				OnSessionLog("{0}\n{1}", e.Message, e.StackTrace);

				if (OnDisconnect != null)
					OnDisconnect(user);

				sock.Close();
			}
		}

		public void Send(ushort msg, byte[] Data, int offset, int length)
		{
			try
			{
				if (sock == null || sock.Connected == false)
					return;

				lock (sendLock)
				{
					using (MemoryStream ms = new MemoryStream(sendBuffer, sendBytes, sendBuffer.Length - sendBytes))
					{
						using (BinaryWriter br = new BinaryWriter(ms, Encoding.Default ))
						{
							ushort len = (ushort)(sizeof(ushort) + sizeof(ushort) );

							if( Data != null )
								len += (ushort) (length > 0 ? length : Data.Length);

							br.Write(len);
							br.Write(msg);
							if (Data != null)
								br.Write(Data, 0, length > 0 ? length : Data.Length);

							if (sendBytes == 0)
							{
								sendBytes += (int)ms.Position;
								sock.BeginSend(sendBuffer, 0, (int)ms.Position, SocketFlags.None, OnSend, sock);
							}
							else
							{
								sendBytes += (int)ms.Position;
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				OnSessionLog("{0}\n{1}", e.Message, e.StackTrace);

				if (OnDisconnect != null)
					OnDisconnect(user);

				sock.Close();
			}
		}

		public void Send(ushort msg)
		{
			Send(msg, null, 0, 0);
		}

		private void OnSend(IAsyncResult ar)
		{
			Socket s = ar.AsyncState as Socket;
			try
			{
				int bytes = sock.EndSend(ar);

				lock (sendLock)
				{
					sendBytes -= bytes;
					Array.Copy(sendBuffer, bytes, sendBuffer, 0, sendBytes);

					if (sendBytes > 0)
					{
						s.BeginSend(sendBuffer, 0, sendBytes, SocketFlags.None, OnSend, s);
					}
				}
			}
			catch (Exception e)
			{
				OnSessionLog("{0}\n{1}", e.Message, e.StackTrace);

				if (OnDisconnect != null)
					OnDisconnect(user);

				s.Close();
			}
		}

		// 会话日志
		private static void TraceMsg(string fmt, params object[] args)
		{
			Trace.WriteLine(string.Format(fmt, args), "network");
		}
	}

	public class Server
	{
		public delegate void CreateSession_d( Socket s );

		private CreateSession_d CreateSession;

		public Server( CreateSession_d CreateSession_f )
		{
			CreateSession = CreateSession_f;
		}

		public void Start( IPAddress Host, int Port, int AcceptCount)
		{
			// Establish the local endpoint for the socket.
			// The DNS name of the computer
			// running the listener is "host.contoso.com".
			IPEndPoint localEndPoint = new IPEndPoint(Host, Port);

			// Create a TCP/IP socket.
			Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

			// Bind the socket to the local endpoint and listen for incoming connections.
			try
			{
				listener.Bind(localEndPoint);
				listener.Listen(100);

				while (AcceptCount != 0)
				{
					// allDone.Reset();
					listener.BeginAccept( new AsyncCallback(AcceptCallback), listener);

					AcceptCount--;
				}
			}
			catch (Exception e)
			{
				Console.WriteLine(e.ToString());
			}
		}

		public void Close()
		{
		}

		public void AcceptCallback(IAsyncResult ar)
		{
			// Signal the main thread to continue.
			// allDone.Set();

			Socket listener = ar.AsyncState as Socket;

			// Get the socket that handles the client request.
			Socket sock = listener.EndAccept(ar);

			// Create the state object.
			CreateSession(sock);

			listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
		}
	}
}
