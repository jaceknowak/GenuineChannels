using System;
using System.Collections;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using Belikov.GenuineChannels.GenuineHttp;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.Security.SSPI;
using KnownObjects;
using Belikov.GenuineChannels;
using Belikov.GenuineChannels.DotNetRemotingLayer;

namespace Server
{
	/// <summary>
	/// Chat server implements server that configures Genuine Server TCP Channel and implements
	/// chat server behavior.
	/// </summary>
	class ChatServer : MarshalByRefObject, IChatServer
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			try
			{
				// setup .NET remoting
				System.Configuration.ConfigurationSettings.GetConfig("DNS");
				GenuineGlobalEventProvider.GenuineChannelsGlobalEvent += new GenuineChannelsGlobalEventHandler(GenuineChannelsEventHandler);
				//GlobalLoggerContainer.Logger = new BinaryLog(@"c:\tmp\server.log", false);

				
                ////RemotingConfiguration.Configure("Server.exe.config");
                
                IDictionary props = new Hashtable();
                props["name"] = "gtcp";
                props["priority"] = "100";
			    props["port"] = "8737";
                // Null entries specify the default formatters.
                GenuineHttpServerChannel channel = new GenuineHttpServerChannel(props, null, null);
                ChannelServices.RegisterChannel(channel);

                KeyProvider_SspiServer keyProvider_SspiServer = new KeyProvider_SspiServer(SspiFeatureFlags.None, SupportedSspiPackages.NTLM);
                SecuritySessionServices.SetGlobalKey("SESSION", keyProvider_SspiServer);

				// bind the server
				RemotingServices.Marshal(new ChatServer(), "ChatServer.rem");

				Console.WriteLine("Server has been started. Press enter to exit.");
				Console.ReadLine();
			}
			catch(Exception ex)
			{
				Console.WriteLine("Exception: {0}. Stack trace: {1}.", ex.Message, ex.StackTrace);
			}
		}

		/// <summary>
		/// Catches Genuine Channels events and removes client session when
		/// user disconnects.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public static void GenuineChannelsEventHandler(object sender, GenuineEventArgs e)
		{
		    if (e.SourceException == null)
				Console.WriteLine("\r\n\r\n---Global event: {0}\r\nRemote host: {1}", 
					e.EventType,
					e.HostInformation == null ? "<unknown>" : e.HostInformation.ToString());
			else
				Console.WriteLine("\r\n\r\n---Global event: {0}\r\nRemote host: {1}\r\nException: {2}", 
					e.EventType, 
					e.HostInformation == null ? "<unknown>" : e.HostInformation.ToString(), 
					e.SourceException);

			if (e.EventType == GenuineEventType.GeneralConnectionClosed)
			{
				// the client disconnected
				string nickname = e.HostInformation["Nickname"] as string;
				if (nickname != null)
					Console.WriteLine("Client \"{0}\" has been disconnected.", nickname);
			}
		}

		public static ChatRoom GlobalRoom = new ChatRoom();

		/// <summary>
		/// Logs into the chat room.
		/// </summary>
		/// <param name="nickname">Nickname.</param>
		/// <returns>Chat room interface.</returns>
		public IChatRoom EnterToChatRoom(string nickname)
		{
            var remoteHost = GenuineUtility.CurrentRemoteHost;
            if (remoteHost != null)
            {
                SecuritySession_SspiServer securitySession_SspiServer = remoteHost.GetSecuritySession(GenuineUtility.CurrentInvocationSecuritySessionParameters.Name, null) as SecuritySession_SspiServer;
                if (securitySession_SspiServer != null)
                {
                    var id = securitySession_SspiServer.WindowsIdentity;
                    Console.WriteLine(id.Name);
                }
            }

			GlobalRoom.AttachClient(nickname);
			GenuineUtility.CurrentSession["Nickname"] = nickname;
			return GlobalRoom;
		}

		/// <summary>
		/// This is to insure that when created as a Singleton, the first instance never dies,
		/// regardless of the expired time.
		/// </summary>
		/// <returns></returns>
		public override object InitializeLifetimeService()
		{
			return null;
		}

	}
}
