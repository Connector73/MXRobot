//*********************************************************
//
// Copyright (c) Connector73. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;
using System.Diagnostics;
using BaseCSTA;
using System.Threading.Tasks;
using Windows.UI.Xaml.Navigation;
using System.Text;
using Windows.System;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace MXRobot
{

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public partial class MainPage : Page
    {
        public Messenger csta;
        public string myJid;

        public void initCSTA()
        {
            csta = new Messenger();
            ICSTAEvent e = (ICSTAEvent)csta;
            e.OnEvent += E_OnEvent;
            ICSTAErrorEvent err = (ICSTAErrorEvent)csta;
            err.OnEvent += E_OnErrorEvent;
            myJid = null;

            csta.AddHandler(new Presence());
            csta.AddHandler(new MessageHistory());
            csta.AddHandler(new MessageAck());
            csta.AddHandler(new SendMessage());
        }

        private string ListValues(Dictionary<string, object> args, string tab)
        {
            string outValue = "";
            foreach (var pair in args)
            {
                if (pair.Value is List<object>)
                {
                    List<object> list = (List<object>)pair.Value;
                    foreach (Dictionary<string, object> item in list)
                    {
                        outValue += "\n" + pair.Key + ":";
                        outValue += "\n" + ListValues(item, tab + "  ");
                    }
                }
                else
                {
                    outValue += String.Format("{0}{1} <- {2}\n", tab, pair.Key, pair.Value);
                }
            }
            return outValue;
        }
        private async void E_OnErrorEvent(object sender, EventArgs e)
        {
            await DoLogin();
        }

        //*************************************************
        //*************************************************
        //********** EXECUTE COMMAND LINE STRING **********
        //*************************************************
        //*************************************************
        private async Task<string> ExecuteCommandLineString(string CommandString)
        {
            const string CommandLineProcesserExe = "c:\\windows\\system32\\cmd.exe";
            const uint CommandStringResponseBufferSize = 8192;
            string currentDirectory = "C:\\";

            StringBuilder textOutput = new StringBuilder((int)CommandStringResponseBufferSize);
            uint bytesLoaded = 0;

            if (string.IsNullOrWhiteSpace(CommandString))
                return ("");

            var commandLineText = CommandString.Trim();

            var standardOutput = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            var standardError = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            var options = new Windows.System.ProcessLauncherOptions
            {
                StandardOutput = standardOutput,
                StandardError = standardError
            };

            try
            {
                var args = "/C \"cd \"" + currentDirectory + "\" & " + commandLineText + "\"";
                var result = await Windows.System.ProcessLauncher.RunToCompletionAsync(CommandLineProcesserExe, args, options);

                //First write std out
                using (var outStreamRedirect = standardOutput.GetInputStreamAt(0))
                {
                    using (var dataReader = new Windows.Storage.Streams.DataReader(outStreamRedirect))
                    {
                        while ((bytesLoaded = await dataReader.LoadAsync(CommandStringResponseBufferSize)) > 0)
                            textOutput.Append(dataReader.ReadString(bytesLoaded));

                        new System.Threading.ManualResetEvent(false).WaitOne(10);
                        if ((bytesLoaded = await dataReader.LoadAsync(CommandStringResponseBufferSize)) > 0)
                            textOutput.Append(dataReader.ReadString(bytesLoaded));
                    }
                }

                //Then write std err
                using (var errStreamRedirect = standardError.GetInputStreamAt(0))
                {
                    using (var dataReader = new Windows.Storage.Streams.DataReader(errStreamRedirect))
                    {
                        while ((bytesLoaded = await dataReader.LoadAsync(CommandStringResponseBufferSize)) > 0)
                            textOutput.Append(dataReader.ReadString(bytesLoaded));

                        new System.Threading.ManualResetEvent(false).WaitOne(10);
                        if ((bytesLoaded = await dataReader.LoadAsync(CommandStringResponseBufferSize)) > 0)
                            textOutput.Append(dataReader.ReadString(bytesLoaded));
                    }
                }

                return (textOutput.ToString());
            }
            catch (UnauthorizedAccessException uex)
            {
                return ("ERROR - " + uex.Message + "\n\nCmdNotEnabled");
            }
            catch (Exception ex)
            {
                return ("ERROR - " + ex.Message + "\n");
            }
        }




        private async void E_OnEvent(object sender, EventArgs e)
        {
            Debug.WriteLine("Event", ((CSTAEventArgs)e).eventName);
            if (((CSTAEventArgs)e).parameters != null)
            {
                Dictionary<string, object> args = (Dictionary<string, object>)((CSTAEventArgs)e).parameters;
                string outValue = ListValues(args, "  ");
                Debug.WriteLine(outValue);

                if (((CSTAEventArgs)e).eventName == "message")
                {
                    if (args["delivered"].ToString() == "false")
                    {
                        string text = args["message"].ToString();
                        await csta.ExecuteHandler("messageAck", new Dictionary<string, string>()
                        {
                            {"userId", myJid },
                            {"msgId", args["msgId"].ToString() },
                            {"reqId", args["reqId"].ToString() }
                        });
                        Debug.WriteLine("Sent ACK for Message");

                        string response = await ExecuteCommandLineString(text);

                        await csta.ExecuteHandler("message", new Dictionary<string, string>()
                        {
                            {"userId", args["from"].ToString() }, // Send to Test User
                            {"ext", "" },
                            {"text", response }
                        });
                        Debug.WriteLine("Sent Echo Message");
                    }
                }
                else if (((CSTAEventArgs)e).eventName == "loginResponce")
                {
                    myJid = args["userId"].ToString();
                }
            }
        }

        public async Task DoLogin()
        {
            bool result = await csta.Connect("631hc.connector73.net", "7778", ConnectType.Secure);
            if (result)
            {
                await csta.Login("maximd", "ihZ6nW62");
            }
            else
            {
                await csta.Disconnect();
                csta = null;
            }
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>

        protected override async void OnNavigatedTo(NavigationEventArgs e)

        {
            base.OnNavigatedTo(e);
            await DoLogin();
        }

        public MainPage()
        {
            this.initCSTA();
            this.InitializeComponent();
        }
    }
}
