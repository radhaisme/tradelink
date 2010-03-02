﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using TradeLink.API;
using TradeLink.Common;
using System.ComponentModel;
using System.Diagnostics;

namespace IQFeedBroker
{
    public class IQFeedHelper : TLServer_WM
    {
        #region Variables

        private const string IQ_FEED = "iqconnect";
        private readonly string IQ_FEED_PROGRAM = "IQConnect.exe";
        private const string IQ_FEED_REGISTRY_LOCATION = "SOFTWARE\\DTN\\IQFeed";
        private AsyncCallback m_pfnCallback;
        private Socket m_sockAdmin;
        private Socket m_sockIQConnect;
        private Socket m_hist;
        private byte[] m_szAdminSocketBuffer = new byte[8096];
        private byte[] m_szLevel1SocketBuffer = new byte[8096];
        private byte[] m_buffhist = new byte[8096];
        private Basket _basket;
        private string _user;
        private string _pswd;
        string _prod;
        private bool _registered;

        BackgroundWorker _connect;
        #endregion


        #region Properties

        public bool IsConnected { get { return _registered; } }

        private bool HaveUserCredentials
        {
            get
            {
                return !(string.IsNullOrEmpty(_user) && string.IsNullOrEmpty(_pswd));
            }
        }

        #endregion


        #region Constructors

        public IQFeedHelper()
        {
            _basket = new BasketImpl();
            _connect = new BackgroundWorker();
            _connect.DoWork += new DoWorkEventHandler(bw_DoWork);
            newProviderName = Providers.IQFeed;
            newRegisterStocks += new DebugDelegate(IQFeedHelper_newRegisterStocks);
            newFeatureRequest += new MessageArrayDelegate(IQFeedHelper_newFeatureRequest);
            newUnknownRequest += new UnknownMessageDelegate(IQFeedHelper_newUnknownRequest);
            _cb_hist = new AsyncCallback(OnReceiveHist);
        }

        void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            // start iqfeed and try to connect in background so it doesn't delay UI
            debug("wait a moment...");
            Connect();

            ConnectToAdmin();
            ConnectToLevelOne();
            ConnectHist();
        }

        long IQFeedHelper_newUnknownRequest(MessageTypes t, string msg)
        {
            switch (t)
            {
                case MessageTypes.DAYHIGH:
                    {
                        // get index for request
                        int idx = _highs.getindex(msg);
                        // ignore if no index
                        if (idx == GenericTracker.UNKNOWN) return 0;
                        decimal v = _highs[idx];
                        // ensure we have a high
                        if (v == decimal.MinValue) return 0;
                        return WMUtil.pack(v);
                    }
                case MessageTypes.DAYLOW:
                    {
                        // get index for request
                        int idx = _highs.getindex(msg);
                        // ignore if no index
                        if (idx == GenericTracker.UNKNOWN) return 0;
                        decimal v = _highs[idx];
                        // ensure we have a high
                        if (v == decimal.MaxValue) return 0;
                        return WMUtil.pack(v);
                    }
                case MessageTypes.BARREQUEST:
                    {
                        BarRequest br = BarImpl.ParseBarRequest(msg);
                        RequestBars(br);
                        return (long)MessageTypes.OK;
                    }
            }
            return (long)MessageTypes.FEATURE_NOT_IMPLEMENTED;
        }
        IdTracker _idt = new IdTracker();
        Dictionary<uint, BarRequest> reqid2req = new Dictionary<uint, BarRequest>();
        void RequestBars(BarRequest br)
        {
            string command;
            uint id = _idt.AssignId;
            if (br.Interval == (int)BarInterval.Day)
            {
                command = String.Format("HDT,{0},{1},,1,{2}\r\n", br.Symbol,br.StartDateTime.ToLongDateString(),br.EndDateTime.ToLongDateString(),id);
            }
            else
            {
                command = String.Format("HIT,{0},{1},{2} {3},{4} {5},,000000,235959,1,{6}\r\n", br.Symbol, br.Interval, br.StartDateTime.ToString("yyyyMMdd"), br.StartDateTime.ToString("HHmmss"), br.EndDateTime.ToString("yyyyMMdd"), br.EndDateTime.ToString("HHmmss"),id);
            }
            reqid2req.Add(id, br);
            // we form a watch command in the form of wSYMBOL\r\n
            byte[] watchCommand = new byte[command.Length];
            watchCommand = Encoding.ASCII.GetBytes(command);
            try
            {
                m_hist.Send(watchCommand, watchCommand.Length, SocketFlags.None);
                debug("Requested historical bars for: " + br.Symbol);
            }
            catch (Exception ex)
            {
                debug("Exception sending barrequest: " + br.ToString());
                debug(ex.Message + ex.StackTrace);
            }

        }

        public delegate void booldel(bool v);
        public event booldel Connected;

        MessageTypes[] IQFeedHelper_newFeatureRequest()
        {
            List<MessageTypes> f = new List<MessageTypes>();
            // add features supported by this connector
            f.Add(MessageTypes.LIVEDATA);
            f.Add(MessageTypes.TICKNOTIFY);
            f.Add(MessageTypes.BROKERNAME);
            f.Add(MessageTypes.REGISTERSTOCK);
            f.Add(MessageTypes.VERSION);

            f.Add(MessageTypes.BARREQUEST);
            f.Add(MessageTypes.BARRESPONSE);
            f.Add(MessageTypes.DAYHIGH);
            f.Add(MessageTypes.DAYLOW);
            return f.ToArray();
        }

        void IQFeedHelper_newRegisterStocks(string msg)
        {
            AddBasket(BasketImpl.FromString(msg));
        }

        void connect(bool iscon)
        {
            if (Connected != null)
                Connected(iscon);
            debug(iscon ? "Connected." : "Connection failed.");
        }

        #endregion


        #region IQFeedHelper Members

        public void Stop()
        {
            Array.ForEach(System.Diagnostics.Process.GetProcessesByName(IQ_FEED), iqProcess => iqProcess.Kill());
            debug("QUITTING****************************");
        }


        internal void ConnectToAdmin()
        {
            // Establish a connection to the admin socket in IQ Feed
            Thread.Sleep(2000);
            ConnectToAdminSocket();
        }


        internal void ConnectToLevelOne()
        {
            try
            {
                Thread.Sleep(2000);
                ConnectToLevelOneSocket();
                Thread.Sleep(2000);
            }
            catch (Exception ex)
            {
                debug(ex.ToString());
            }
        }


        /// <summary>
        /// Subscribe to securities in the basket supplied as long as they aren't already in the underlying basket.
        /// </summary>
        /// <param name="basket"></param>
        internal void AddBasket(Basket basket)
        {
            foreach (Security security in basket)
            {
                AddSecurity(security);
            }
        }


        /// <summary>
        /// Subscribe to the security supplied as long as its not in the underlying basket
        /// </summary>
        /// <param name="security"></param>
        internal void AddSecurity(Security security)
        {
            if (!havesymbol(security.Symbol))
            {
                _highs.addindex(security.Symbol,decimal.MinValue);
                _lows.addindex(security.Symbol,decimal.MaxValue);

                _basket.Add(security);
                AddSecurityToBasket(security);
                debug("Added subscription: " + security.Symbol);
            }
        }


        /// <summary>
        /// Physically adds the security to the underlying basket and connects to the socket passing
        /// this security
        /// </summary>
        /// <param name="security"></param>
        private void AddSecurityToBasket(Security security)
        {

            // we form a watch command in the form of wSYMBOL\r\n
            string command = String.Format("w{0}\r\n", security.Symbol);

            // and we send it to the feed via the socket
            byte[] watchCommand = new byte[command.Length];
            watchCommand = Encoding.ASCII.GetBytes(command);
            m_sockIQConnect.Send(watchCommand, watchCommand.Length, SocketFlags.None);
        }

        bool havesymbol(string sym)
        {
            for (int i = 0; i < _basket.Count; i++)
                if (_basket[i].Symbol == sym) return true;
            return false;
        }



        public void Start(string username, string password, string data1, int data2)
        {
            _registered = false;

            // get IQConnect Location from registry
            // IQFeed Installation directory is stored in the registry key
            RegistryKey key = Registry.LocalMachine.OpenSubKey(IQ_FEED_REGISTRY_LOCATION);
            if (key == null)
            {
                debug("IQ Feed is not installed");
                return;
            }
            // close the key since we don't need it anymore
            key.Close();
            _user = username;
            _pswd = password;
            _prod = data1;
            _connect.RunWorkerAsync();

        }

        void Connect()
        {
            try
            {
                int iqConnectProcessCount = System.Diagnostics.Process.GetProcessesByName(IQ_FEED).Length;
                switch (iqConnectProcessCount)
                {
                    case 1:
                        debug("IQ Connect price feed is already running");
                        break;
                    case 0:
                        debug("IQFeed not running, attempting to start...");
                        string args = string.Empty;
                        if (HaveUserCredentials)
                        {
                            args += String.Format("-product {0} -version 1.0.0.0 -login {1} -password {2} -savelogininfo -autoconnect", _prod, _user, _pswd);
                            Process p = Process.Start(IQ_FEED_PROGRAM, args);
                        }
                        break;
                    default:
                        throw new ApplicationException(string.Format("IQ Connect Feed has {0} instances currently running", iqConnectProcessCount));
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        /// <summary>
        /// Establishes a connection to the admin socket in the IQ Feed
        /// </summary>
        private void ConnectToAdminSocket()
        {
            try
            {
                m_sockAdmin = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress ipLocalHost = IPAddress.Parse(Properties.Settings.Default.IP_LOCAL_HOST);
                RegistryKey key = Registry.CurrentUser.OpenSubKey(String.Format("{0}\\Startup", IQ_FEED_REGISTRY_LOCATION));
                int port = int.Parse(key.GetValue(String.Format("{0}Port", Properties.Settings.Default.ADMINISTRATION_SOCKET_NAME), "9300").ToString());
                IPEndPoint endPoint = new IPEndPoint(ipLocalHost, port);
                m_sockAdmin.Connect(endPoint);
                WaitForData(Properties.Settings.Default.ADMINISTRATION_SOCKET_NAME);
            }
            catch (Exception ex)
            {
                debug(String.Format("ADMIN SOCKET ERROR: {0}", ex.Message));
            }
        }


        /// <summary>
        /// Establishes a connection to the admin socket in IQFeed
        /// </summary>
        private void ConnectToLevelOneSocket()
        {
            try
            {
                m_sockIQConnect = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress ipLocalHost = IPAddress.Parse(Properties.Settings.Default.IP_LOCAL_HOST);
                RegistryKey key = Registry.CurrentUser.OpenSubKey(String.Format("{0}\\Startup", IQ_FEED_REGISTRY_LOCATION));
                int port = int.Parse(key.GetValue(String.Format("{0}Port", Properties.Settings.Default.LEVEL_ONE_SOCKET_NAME), "5009").ToString());
                IPEndPoint endPoint = new IPEndPoint(ipLocalHost, port);
                m_sockIQConnect.Connect(endPoint);
                WaitForData(Properties.Settings.Default.LEVEL_ONE_SOCKET_NAME);
            }
            catch (Exception ex)
            {
                debug(String.Format("LEVEL ONE ERROR: {0}", ex.Message));
                throw ex;
            }
        }
        const string HISTSOCKET = "HISTSOCK";
        void ConnectHist()
        {
            try
            {
                debug("Attempting to connect to historical feed.");
                m_hist = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress local = IPAddress.Loopback;
                RegistryKey rk = Registry.CurrentUser.OpenSubKey(IQ_FEED_REGISTRY_LOCATION + "\\Startup");
                int port = 0;
                if (!int.TryParse(rk.GetValue("LookupPort").ToString(), out port))
                {
                    debug("could not find historical data port.");
                    return;
                }
                m_hist.Connect(new IPEndPoint(local, port));
                if (m_hist.Connected)
                    debug("historical connected.");
                WaitForData(HISTSOCKET);
            }
            catch (Exception ex)
            {
                debug("historical connect failed.");
                debug(ex.Message + ex.StackTrace);
            }

        }

        AsyncCallback _cb_hist = null;

        private void WaitForData(string socketName)
        {
            try
            {
                if (m_pfnCallback == null)
                    m_pfnCallback = new AsyncCallback(OnReceiveData);

                if (socketName == Properties.Settings.Default.LEVEL_ONE_SOCKET_NAME)
                {
                    if (m_sockIQConnect != null)
                        m_sockIQConnect.BeginReceive(m_szLevel1SocketBuffer, 0, m_szLevel1SocketBuffer.Length, SocketFlags.None, m_pfnCallback, socketName);
                }
                else if (socketName == Properties.Settings.Default.ADMINISTRATION_SOCKET_NAME)
                    m_sockAdmin.BeginReceive(m_szAdminSocketBuffer, 0, m_szAdminSocketBuffer.Length, SocketFlags.None, m_pfnCallback, socketName);
                else if (socketName == HISTSOCKET)
                {
                    m_hist.BeginReceive(m_buffhist, 0, m_buffhist.Length, SocketFlags.None, _cb_hist, socketName);
                }
            }
            catch (Exception ex)
            {
                debug("socket error on: " + socketName);
                debug(ex.Message + ex.StackTrace);
            }
        }

        string _histbuff = string.Empty;
        const string HISTEND = "!ENDMSG!";
        void OnReceiveHist(IAsyncResult result)
        {
            try
            {
                int bytesReceived = m_hist.EndReceive(result);
                string recvdata = Encoding.ASCII.GetString(m_buffhist, 0, bytesReceived);
                string rawData = _histbuff ==string.Empty ? recvdata : _histbuff+recvdata;
                string[] bars = rawData.Split(Environment.NewLine.ToCharArray());
                foreach (string bar in bars)
                {
                    string[] r = bar.Split(',');
                    // this should get hit on ENDMSG
                    if (r.Length < 8) 
                        continue;
                    // get request id
                    uint rid = 0;
                    if (!uint.TryParse(r[0], out rid))
                        continue;
                    BarRequest br;
                    if (!reqid2req.TryGetValue(rid, out br))
                        continue;
                    bool errorfree = true;
                    Bar b = new BarImpl();
                    try
                    {
                        DateTime dt = DateTime.Parse(r[1]);
                        decimal h = Convert.ToDecimal(r[2]);
                        decimal l = Convert.ToDecimal(r[3]);
                        decimal o = Convert.ToDecimal(r[4]);
                        decimal c = Convert.ToDecimal(r[5]);
                        long v = Convert.ToInt64(r[7]);
                        // build bar
                        b = new BarImpl(o, h, l, c, v, Util.ToTLDate(dt), Util.ToTLTime(dt), br.Symbol, br.Interval);
                    }
                    catch { errorfree = false; }
                    // send it
                    if (errorfree)
                        TLSend(BarImpl.Serialize(b), MessageTypes.BARRESPONSE, br.Client);
                }
                string lastrecord = bars[bars.Length-1];
                if (lastrecord.Contains(HISTEND))
                    _histbuff = string.Empty;
                else
                    _histbuff = lastrecord;
                // wait for more historical data
                WaitForData(HISTSOCKET);
            }
            catch (Exception ex)
            {
                debug(ex.Message + ex.StackTrace);
            }
        }


        /// <summary>
        /// This is our callback that gets called by the .NET socket class when new data arrives on the socket
        /// </summary>
        /// <param name="asyn"></param>
        private void OnReceiveData(IAsyncResult result)
        {
            // first verify we received data from the correct socket.
            if (result.AsyncState.ToString().Equals(Properties.Settings.Default.ADMINISTRATION_SOCKET_NAME))
            {
                try
                {
                    //debug(String.Format("Result State: {0}", result.AsyncState));
                    int bytesReceived = m_sockAdmin.EndReceive(result);
                    string rawData = Encoding.ASCII.GetString(m_szAdminSocketBuffer, 0, bytesReceived);
                    bool connectToLevelOne = _registered;

                    while (rawData.Length > 0)
                    {
                        string data = rawData.Substring(0, rawData.IndexOf("\n"));
                        //debug(String.Format("ADMIN: {0}", data));
                        if (data.StartsWith("S,STATS,"))
                        {
                            #region Register this application (if necessary)...May want to extract this into a separate function as it's only called once.
                            if (!_registered)
                            {
                                string command = String.Format("S,REGISTER CLIENT APP,{0},{1}\r\n", Properties.Settings.Default.PROGRAM_NAME, Assembly.GetExecutingAssembly().GetName().Version);
                                byte[] size = new byte[command.Length];
                                int bytesToSend = size.Length;
                                size = Encoding.ASCII.GetBytes(command);
                                m_sockAdmin.Send(size, bytesToSend, SocketFlags.None);
                                _registered = true;
                                connect(_registered);
                            }
                            #endregion
                        }
                        else if (data.StartsWith("S,REGISTER CLIENT APP COMPLETED"))
                        {
                            #region Set the login and other attribute settings...KRJ: Not written this yet to see if can do on start up
                            string command = "S,SET LOGINID,244023\r\nS,SET PASSWORD,8488\r\nS,SET SAVE LOGIN INFO,On\r\nS,SET AUTOCONNECT,On\r\nS,CONNECT\r\n";
                            byte[] size = new byte[command.Length];
                            size = Encoding.ASCII.GetBytes(command);
                            int bytesToSend = size.Length;
                            m_sockAdmin.Send(size, bytesToSend, SocketFlags.None);
                            _registered = true;
                            #endregion
                        }

                        rawData = rawData.Substring(data.Length + 1);
                    }

                    WaitForData(Properties.Settings.Default.ADMINISTRATION_SOCKET_NAME);
                }
                catch (SocketException ex)
                {
                    debug(ex.Message+ex.StackTrace);
                }
                catch (Exception ex)
                {
                    debug(ex.Message + ex.StackTrace);
                }
            }
            else if (result.AsyncState.ToString().Equals(Properties.Settings.Default.LEVEL_ONE_SOCKET_NAME))
            {
                try
                {
                    int bytesReceived = 0;
                    bytesReceived = m_sockIQConnect.EndReceive(result);
                    string rawData = Encoding.ASCII.GetString(m_szLevel1SocketBuffer, 0, bytesReceived);

                    string[] splitTickData = rawData.Split('\n');

                    Array.Reverse(splitTickData);
                    var sentTicks = new Dictionary<string, int>();
                    Array.ForEach(splitTickData, str =>
                    {
                        string[] actualData = str.Split(',');
                        int val;
                        if ((actualData.Length >= 15) && (!sentTicks.TryGetValue(actualData[1], out val)))
                        {
                            FireTick(actualData);
                        }
                    });
                }
                catch (SocketException ex)
                {
                    debug(String.Format("LEVEL ONE: Socket Exception: {0}", ex.Message));
                }
                catch (Exception ex)
                {
                    debug(String.Format("LEVEL ONE: Full Exception: {0}", ex.Message));
                    debug(ex.ToString());
                }

                WaitForData(Properties.Settings.Default.LEVEL_ONE_SOCKET_NAME);
            }
            else
            {
                debug(result.AsyncState.ToString());
            }
        }

        GenericTracker<decimal> _highs = new GenericTracker<decimal>();
        GenericTracker<decimal> _lows = new GenericTracker<decimal>();

        private void FireTick(string[] actualData)
        {
                try
                {
                    if ((actualData[0] == "P") || (actualData[0] == "Q"))
                    {
                        Tick tick = new TickImpl();
                        tick.date = Util.ToTLDate(DateTime.Now);
                        tick.time = Util.DT2FT(DateTime.Now);
                        tick.symbol = actualData[1];
                        tick.bid = Convert.ToDecimal(actualData[10]);
                        tick.ask = Convert.ToDecimal(actualData[11]);
                        tick.ex = actualData[2];
                        tick.trade = Convert.ToDecimal(actualData[3]);
                        tick.size = Convert.ToInt32(actualData[7]);
                        tick.BidSize = Convert.ToInt32(actualData[12]);
                        tick.AskSize= Convert.ToInt32(actualData[13]);
                        // get symbol index for custom data requests
                        int idx = _highs.getindex(tick.symbol);

                        // update custom data (tryparse is faster than convert)
                        decimal v = 0;
                        if (decimal.TryParse(actualData[8], out v))
                            _highs[idx] = v;
                        if (decimal.TryParse(actualData[9], out v))
                            _lows[idx] = v;
                        

                        newTick(tick);
                    }
                    //else if (actualData[0] == "F")
                    //{
                    //    Tick tick = new TickImpl();
                    //    tick.date = Util.ToTLDate(DateTime.Now);
                    //    tick.time = Util.DT2FT(DateTime.Now);
                    //    tick.symbol = actualData[1];
                    //    tick.bid = Convert.ToDecimal(0.00);
                    //    tick.ask = Convert.ToDecimal(0.00);
                    //    tick.ex = null;
                    //    tick.trade = Convert.ToDecimal(0.00);
                    //    tick.size = 1;
                    //    tick.bs = Convert.ToInt32(0.00);
                    //    tick.os = Convert.ToInt32(0.00);

                    //    TickReceived(tick);
                    //}
                }
                catch (Exception ex)
                {
                    debug("Tick error: " + string.Join(",", actualData));
                    debug(ex.Message+ex.StackTrace);
                }


                        


        }

        void debug(string msg)
        {
            if (SendDebug != null)
                SendDebug(msg);
        }
        public event TradeLink.API.DebugDelegate SendDebug;

        

        #endregion
    }


}