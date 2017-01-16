using System;
using System.Windows.Forms;
using SteamKit2;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using static SteamKit2.SteamMasterServer;
using System.Net;
using IpPublicKnowledge;
using System.Collections.Generic;
using static SteamKit2.CDNClient;

namespace CSCO_Launcher
{
    public partial class main : Form
    {
        public main()
        {
            InitializeComponent();
        }

        private void main_Load(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.FirstStart) { settings settingsForm = new settings(); settingsForm.Show(); }
        }
        public class SteamListConnection
        {
            SteamClient steamClient;
            SteamMasterServer masterServer;
            CallbackManager manager;
            SteamUser steamUser;

            List<Server> serverList = new List<Server>();

            bool isRunning;

            string username;
            string password;
            string authCode, twoFactorAuth;


            public SteamListConnection(List<Server> serverList, string username, string password)
            {
                this.serverList = serverList;
                this.username = username;
                this.password = password;

                steamClient = new SteamClient();
                manager = new CallbackManager(steamClient);
                steamUser = steamClient.GetHandler<SteamUser>();

                manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
                manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);

                manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);

                isRunning = true;

                steamClient.Connect();

                while (isRunning)
                {
                    manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
                }

            }


            void OnConnected(SteamClient.ConnectedCallback callback)
            {
                if (callback.Result != EResult.OK)
                {
                    isRunning = false;
                    return;
                }


                byte[] sentryHash = null;
                if (File.Exists("sentry.bin"))
                {
                    byte[] sentryFile = File.ReadAllBytes("sentry.bin");
                    sentryHash = CryptoHelper.SHAHash(sentryFile);
                }

                Console.WriteLine("Username: " + this.username + " | password: " + this.password);
                steamUser.LogOn(new SteamUser.LogOnDetails
                {
                    Username = this.username,
                    Password = this.password,
                    AuthCode = authCode,
                    TwoFactorCode = twoFactorAuth,
                    SentryFileHash = sentryHash,
                });
            }

            void OnDisconnected(SteamClient.DisconnectedCallback callback)
            {
                Thread.Sleep(TimeSpan.FromSeconds(5));

                steamClient.Connect();
            }

            void OnLoggedOn(SteamUser.LoggedOnCallback callback)
            {
                bool isSteamGuard = callback.Result == EResult.AccountLogonDenied;
                bool is2FA = callback.Result == EResult.AccountLoginDeniedNeedTwoFactor;
                Console.WriteLine("Logged in...");
                if (isSteamGuard || is2FA)
                {
                    Console.WriteLine("Creating message box");
                    //AuthCodeDisplay messageBox = new AuthCodeDisplay();
                    //Application.Run(messageBox);
                    //messageBox.WaitForSubmission();

                    //if (is2FA)
                    //{
                    //    twoFactorAuth = messageBox.GetAuthCode();
                    //    messageBox.Close();

                    //}
                    //else
                    //{
                    //    authCode = messageBox.GetAuthCode();
                    //}

                    //messageBox.Close();

                    return;
                }

                if (callback.Result != EResult.OK)
                {
                    isRunning = false;
                    return;
                }

                Console.WriteLine("Passed 2fa");
                Console.WriteLine("Starting server list grab");
                StartServerListGrab();

            }

            void OnMachineAuth(SteamUser.UpdateMachineAuthCallback callback)
            {
                int fileSize;
                byte[] sentryHash;
                using (var fs = File.Open("sentry.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    fs.Seek(callback.Offset, SeekOrigin.Begin);
                    fs.Write(callback.Data, 0, callback.BytesToWrite);
                    fileSize = (int)fs.Length;

                    fs.Seek(0, SeekOrigin.Begin);
                    using (var sha = new SHA1CryptoServiceProvider())
                    {
                        sentryHash = sha.ComputeHash(fs);
                    }
                }

                steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
                {
                    JobID = callback.JobID,

                    FileName = callback.FileName,

                    BytesWritten = callback.BytesToWrite,
                    FileSize = fileSize,
                    Offset = callback.Offset,

                    Result = EResult.OK,
                    LastError = 0,

                    OneTimePassword = callback.OneTimePassword,

                    SentryFileHash = sentryHash,
                });

                StartServerListGrab();
            }

            async void StartServerListGrab()
            {
                this.masterServer = steamClient.GetHandler<SteamMasterServer>();

                QueryDetails querydetails = new QueryDetails();
                querydetails.AppID = 740;
                //querydetails.GeoLocatedIP = this.GetExternalIP();
                //querydetails.Filter = "\\gamedir\\csco";


                QueryCallback query = await masterServer.ServerQuery(querydetails);

                foreach (QueryCallback.Server serv in query.Servers)
                {
                    Console.WriteLine(serv.EndPoint.Address.ToString() + ":" + serv.EndPoint.Port);
                }

            }

            public IPAddress GetExternalIP()
            {
                return IPK.GetMyPublicIp();
            }

        }
    }
}
