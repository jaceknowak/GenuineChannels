using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Security.Principal;
using System.Threading;
using Belikov.GenuineChannels;
using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.GenuineTcp;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.Security.SSPI;
using Belikov.GenuineChannels.TransportContext;
using KnownObjects;
using Belikov.GenuineChannels.DotNetRemotingLayer;

namespace Client
{
	/// <summary>
	/// ChatClient demostrates simple client application.
	/// </summary>
	class ChatClient : MarshalByRefObject, IMessageReceiver
	{
		/// <summary>
		/// The only instance.
		/// </summary>
		public static ChatClient Instance = new ChatClient();

		/// <summary>
		/// Nickname.
		/// </summary>
		public static string Nickname;

		/// <summary>
		/// Chat room.
		/// </summary>
		public static IChatRoom IChatRoom;

		/// <summary>
		/// A proxy to server business object.
		/// </summary>
		public static IChatServer IChatServer;

		/// <summary>
		/// To provide thread-safe access to ChatClient.IChatServer member.
		/// </summary>
		public static object IChatServerLock = new object();

        private const string SessionName = "/TEST/SSPI1";

        private static void SetCredentials(ITransportContextProvider iContextProvider, NetworkCredential userCredential, string targetName)
        {
            if (iContextProvider != null)
            {
                ITransportContext transportContext = iContextProvider.ITransportContext;
                var keyProvider = (KeyProvider_SspiClient)transportContext.IKeyStore.GetKey(SessionName);
                keyProvider.AuthIdentity = userCredential;
                keyProvider.ServerName = targetName;

                SetSecuritySessionParameters(transportContext);
            }
        }

        private static SecuritySessionAttributes GetExistingAttributes(SecuritySessionParameters parameters)
        {
            return (parameters != null) ? parameters.Attributes : SecuritySessionAttributes.None;
        }

        private static void SetSecuritySessionParameters(ISetSecuritySession context)
        {
            SecuritySessionAttributes existingAttributes = GetExistingAttributes(context.SecuritySessionParameters);
            context.SecuritySessionParameters = new SecuritySessionParameters(SessionName, existingAttributes, TimeSpan.MinValue);
        }

        private const string TargetServerName = "localhost"; // win2012r2-jacek

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			// wait for the server
			Console.WriteLine("Sleep for 3 seconds.");
			Thread.Sleep(TimeSpan.FromSeconds(3));

            KeyProvider_SspiClient keyProvider_SspiClient = new KeyProvider_SspiClient(SspiFeatureFlags.Encryption | SspiFeatureFlags.Signing, SupportedSspiPackages.Negotiate, null, TargetServerName);
            ////SecuritySessionServices.SetGlobalKey(SessionName, keyProvider_SspiClient);

			// setup .NET Remoting
			Console.WriteLine("Configuring Remoting environment...");
			System.Configuration.ConfigurationSettings.GetConfig("DNS");
			GenuineGlobalEventProvider.GenuineChannelsGlobalEvent += GenuineChannelsEventHandler;
			//GlobalLoggerContainer.Logger = new BinaryLog(@"c:\tmp\client.log", false);
			//// RemotingConfiguration.Configure("Client.exe.config");

            IDictionary props = new Hashtable();
            props["name"] = "gtcp";
            props["priority"] = "100";

            BinaryServerFormatterSinkProvider srv = new BinaryServerFormatterSinkProvider();
            BinaryClientFormatterSinkProvider clnt = new BinaryClientFormatterSinkProvider();
            GenuineTcpChannel channel = new GenuineTcpChannel(props, clnt, srv);
            channel.ITransportContext.IKeyStore.SetKey(SessionName, keyProvider_SspiClient);
            channel.ITransportContext.IParameterProvider[GenuineParameter.SecuritySessionForPersistentConnections] = "/TEST/SSPI1";
            channel.ITransportContext.IParameterProvider[GenuineParameter.SecuritySessionForNamedConnections] = "/TEST/SSPI1";
            channel.ITransportContext.IParameterProvider[GenuineParameter.SecuritySessionForInvocationConnections] = "/TEST/SSPI1";
           // WellKnownServiceTypeEntry WKSTE = new WellKnownServiceTypeEntry(typeof(ChatClient), "MessageReceiver.rem", WellKnownObjectMode.Singleton);
           // RemotingConfiguration.RegisterWellKnownServiceType(WKSTE);
            
            ////ITransportContextProvider iTcpProvider = (ITransportContextProvider) ChannelServices.GetChannel("gtcp");
            IIdentity identity = WindowsIdentity.GetCurrent();
            ////GenuineUtility.CurrentRemoteHost.DestroySecuritySession("D");
            SetCredentials(channel, null, TargetServerName);
            SecuritySessionServices.SetGlobalKey("/TEST/SSPI1", keyProvider_SspiClient);
            ChannelServices.RegisterChannel(channel, false);
		    
			Console.WriteLine(".NET Remoting has been configured from Client.exe.config file.");

			Console.WriteLine("Please enter a nickname:");
			Nickname = Console.ReadLine();

			// bind client's receiver
			//// RemotingServices.Marshal(Instance, "MessageReceiver.rem");

           ////SecuritySessionServices.SetCurrentSecurityContext ( new SecuritySessionParameters ("SESSION", SecuritySessionAttributes.ForceSync, TimeSpan.MinValue, GenuineConnectionType.Persistent, null, TimeSpan.FromMinutes(5) ) );

			for(;;)
			{
				try
				{
					// subscribe to the chat event
					lock(ChatClient.IChatServerLock)
					{
                        //// RemotingServices.Marshal((RemoteDataServer)serverInstance, "RemoteServer.rem");

					    try
					    {
					        var cs =
					            (IChatServer)
					                Activator.GetObject(typeof (IChatServer),
					                    ConfigurationSettings.AppSettings["RemoteHostUri"] + "/ChatServer.rem");
					        var s = cs.Test;
                            Console.WriteLine(s);
					        ChatClient.IChatRoom = cs.EnterToChatRoom(ChatClient.Nickname);
					    }
					    catch (Exception ex)
					    {
					        Console.WriteLine(ex.StackTrace);
					    }
					}

					for(;;)
					{
						Console.WriteLine("Enter a message to send or an empty string to exit.");

						string str = Console.ReadLine();
						if (str.Length <= 0)
							return ;

						ChatClient.IChatRoom.SendMessage(str);
					}
				}
				catch(Exception ex)
				{
					Console.WriteLine("\r\n\r\n---Exception: {0}. Stack trace: {1}.", ex.Message, ex.StackTrace);
				}

				Console.WriteLine("Next attempt to connect to the server will be in 3 seconds.");
				Thread.Sleep(3000);
			}
		}

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

			if (e.EventType == GenuineEventType.GeneralServerRestartDetected)
			{
				// server has been restarted so it does not know that we have been subscribed to
				// messages and ours nickname
				lock(ChatClient.IChatServerLock)
				{
					ChatClient.IChatServer = (IChatServer) Activator.GetObject(typeof(IChatRoom),
						ConfigurationSettings.AppSettings["RemoteHostUri"] + "/ChatServer.rem");
					ChatClient.IChatRoom = ChatClient.IChatServer.EnterToChatRoom(ChatClient.Nickname);
				}
			}
		}

		/// <summary>
		/// Message receiver.
		/// It receives messages async and writes them separately from the main thread.
		/// But it does not matter for console application.
		/// </summary>
		/// <param name="message">The message.</param>
		public object ReceiveMessage(string message, string nickname)
		{
			Console.WriteLine("Message \"{0}\" from \"{1}\".", message, nickname);
			return null;
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
