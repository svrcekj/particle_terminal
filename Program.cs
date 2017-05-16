using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Multi_Client
{
    class Program
    {

        private static Socket _clientSocket;
        const int POLL_TIME = 50;

        static bool firstTime = true;

        private static IPAddress activeIP;
        private static List<IPAddress> activeIpList = new List<IPAddress>();

        static IPAddress _START_IP = IPAddressTools.IncrementIPAddress(GetLocalIPAddress());

        const int NR_OF_IPs = 254;
        private const int _PORT = 23;

        static void Main()
        {
            Console.Title = "Client";

            while (true)
            {
                _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _clientSocket.ReceiveTimeout = 500;
                _clientSocket.SendTimeout = 100;
                FindPhoton();
                RequestLoop();
                _clientSocket.Close(1);

            }
        }

        private static void FindPhoton()
        {
            bool photonFound = false;
            int tryCount = 0;

            while (!photonFound)
            {
                if (tryCount > 0)
                    Console.WriteLine("  try {0} out of 5...", tryCount);

                activeIP = GetActiveServerIp(_START_IP, NR_OF_IPs);

                //Console.Clear();
                bool removeGateway = false;
                if (removeGateway)
                {
                }

                foreach (var openedAddr in activeIpList)
                {
                    if (ConnectToServer(openedAddr))
                    {
                        activeIP = openedAddr;
                        photonFound = true;
                        break;
                    }
                }
                //Console.WriteLine("------------------------------------------------------------");
                if (activeIP.Equals(IPAddress.None))
                {
                    //Console.WriteLine("...try {0} out of 5...", 5-tryCount);
                    //Console.ReadKey();
                    //return;
                }
                if (tryCount++ == 5)
                {
                    Console.Write("\nPhoton dongle not found. Enter IP address manualy: ");
                    string ip = Console.ReadLine();
                    try
                    {
                        activeIP = IPAddress.Parse(ip);
                        ConnectToServer(activeIP);
                    }
                    catch (FormatException e)
                    {
                        Console.WriteLine("Not a valid IP address!");
                    }
                    break;
                }
            }
        }

        private static IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    byte firstByte = ip.GetAddressBytes()[0];
                    if (firstByte == 192)
                        return ip;
                }
            }
            throw new Exception("Local IP Address Not Found!");
        }

        private static IPAddress GetActiveServerIp(IPAddress _START_IP, int nrOfIps)
        {
            AutoResetEvent[] waiter = new AutoResetEvent[256];
            Ping[] pingSender = new Ping[256];

            IPAddress addr = _START_IP;
            // Wait desired amount of seconds for a reply.
            int timeout = 2000;

            activeIpList.Clear();
            
            // Set options for transmission:
            // The data can go through 64 gateways or routers
            // before it is destroyed, and the data packet
            // cannot be fragmented.
            PingOptions options = new PingOptions(64, true);

            // Create a buffer of 32 bytes of data to be transmitted.
            string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            byte[] buffer = Encoding.ASCII.GetBytes(data);

            
            if (firstTime)
            {
                Console.WriteLine("Scanning IP's from {0} to {1}",
                    _START_IP.ToString(), IPAddressTools.ModIPAddress(_START_IP, NR_OF_IPs - 1).ToString());
                firstTime = false;
            }
            
            for (int i = 0; i < nrOfIps; i++)
            {
                waiter[i] = new AutoResetEvent(false);
                pingSender[i] = new Ping();

                // When the PingCompleted event is raised,
                // the PingCompletedCallback method is called.
                pingSender[i].PingCompleted += new PingCompletedEventHandler(PingCompletedCallback);

                addr = IPAddressTools.ModIPAddress(_START_IP, i);
                //Console.WriteLine("Checking address {0}: ", addr.ToString());

                // Send the ping asynchronously.
                // Use the waiter as the user token.
                // When the callback completes, it can wake up this thread.
                pingSender[i].SendAsync(addr, timeout, buffer, options, waiter[i]);
                // the following line commented because we want asynchronous call
            }

            for (int i = 0; i < nrOfIps; i++)
            {
                waiter[i].WaitOne();
            }
            //Console.WriteLine("------------------------------------------------------------");
            //Console.WriteLine("Ping requests sent to all IPs.");
            //Console.WriteLine("------------------------------------------------------------");
            return IPAddress.None;
        }

        private static void PingCompletedCallback(object sender, PingCompletedEventArgs e)
        {
            // If the operation was canceled, display a message to the user.
            if (e.Cancelled)
            {
                Console.WriteLine("Ping canceled.");

                // Let the main thread resume. 
                // UserToken is the AutoResetEvent object that the main thread 
                // is waiting for.
                ((AutoResetEvent)e.UserState).Set();
            }

            // If an error occurred, display the exception to the user.
            if (e.Error != null)
            {
                Console.WriteLine("Ping failed:");
                Console.WriteLine(e.Error.ToString());

                // Let the main thread resume. 
                ((AutoResetEvent)e.UserState).Set();
            }

            PingReply reply = e.Reply;

            DisplayReply(reply);

            // Let the main thread resume.
            ((AutoResetEvent)e.UserState).Set();
        }

        public static void DisplayReply(PingReply reply)
        {
            if (reply == null)
                return;

            if (reply.Status == IPStatus.Success)
            {
                //Console.WriteLine("ping status: {0}", reply.Status);
                Console.WriteLine("Found device at {0}", reply.Address.ToString());
                //Console.WriteLine("RoundTrip time: {0}", reply.RoundtripTime);
                //Console.WriteLine("Time to live: {0}", reply.Options.Ttl);
                //Console.WriteLine("Don't fragment: {0}", reply.Options.DontFragment);
                //Console.WriteLine("Buffer size: {0}", reply.Buffer.Length);
                activeIpList.Add(reply.Address);
            }
        }

        private static bool ConnectToServer(IPAddress addr)
        {
            int attempts = 0;

            Console.WriteLine("Trying connect to " + addr.ToString());

            _clientSocket.Blocking = false;

            while (!_clientSocket.Connected)
            {
                try
                {
                    attempts++;
                    Console.WriteLine("Connection attempt " + attempts);
                    _clientSocket.Connect(addr, _PORT);
                }
                catch (SocketException sockErr)
                {
                    if ((sockErr.SocketErrorCode == SocketError.WouldBlock) ||
                        (sockErr.SocketErrorCode == SocketError.AlreadyInProgress) ||
                        (sockErr.SocketErrorCode == SocketError.InProgress))
                    {
                        //Console.WriteLine("Conection attempt in progress...");
                    }
                    else if (sockErr.SocketErrorCode == SocketError.Success)
                    {
                        Console.WriteLine("Conection succeeded.");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("Socket Error: " + sockErr.ErrorCode.ToString());
                    }
                    Thread.Sleep(900);
                    if (attempts == 5)
                    {
                        Console.WriteLine("Connection failed.");
                        //_clientSocket.Close();
                        //_clientSocket.Disconnect(true);
                        //_clientSocket.Dispose();
                        return false;
                    }
                }
            }

            _clientSocket.Blocking = true;

            if (_clientSocket.Connected)
            {
                Console.WriteLine("Conection succeeded.");
                return true;
            }
            else
            {
                Console.WriteLine("Conection failed.");
                return false;
            }
        }

        private static int getChunkSize(char b)
        {
            int[] chunkSizeArray =  {4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048};

            int index = b - '0';
            if (index < 0)
                return 2;
            else if (index > 9)
                return 4096;
            else
                return chunkSizeArray[index];
        }

        //=========================================================================================
        //=========================================================================================
        //=========================================================================================

        private static void printHelp()
        {
            Console.WriteLine();
            Console.WriteLine("--------------------------------------------------------------------------------------------------");
            Console.WriteLine("General read commands are:");
            Console.WriteLine("");
            Console.WriteLine("     -r <CC> <N>");
            Console.WriteLine("     -r <CC><AAAA> <N>");
            Console.WriteLine("");
            Console.WriteLine("General write commands are:");
            Console.WriteLine("");
            Console.WriteLine("     -w <B0>");
            Console.WriteLine("     -w <B0><B1><B2>..<Bn>");
            Console.WriteLine("     -w <B0> <B1> <B2> .. <Bn>");
            Console.WriteLine("");
            Console.WriteLine("where");
            Console.WriteLine("");
            Console.WriteLine("     <CC> must be a 2-digit hexa number (00 .. FF),");
            Console.WriteLine("     <AAAA> must be a 4-digit hexa number (0000 .. FFFF),");
            Console.WriteLine("     <N> can be 4 digit decimal number (0 .. 9999) and");
            Console.WriteLine("     <B0> .. <Bn> must be 2 digit hexa nubers (max 16 allowed).");
            Console.WriteLine("");
            Console.WriteLine("Examples: -r 85 9, -r b40004 7, -r d05000 256.");
            Console.WriteLine("");
            Console.WriteLine("Special commands are:");
            Console.WriteLine("");
            Console.WriteLine("     help, exit, clear, reconnect,");
            Console.WriteLine("     reset, init, id, dummy<X>, frame<X>, loopdummy<X>, loopframe<X>, demo, speed, heat.");
            Console.WriteLine("");
            Console.WriteLine("where");
            Console.WriteLine("");
            Console.WriteLine("     <X> can be 1 digit decimal number (0 .. 9) or can be omitted.");
            Console.WriteLine("");
            Console.WriteLine("Examples: -w A0, -w f75234, -w f7 52, 34, -w b60031 80.");
            Console.WriteLine("");
            Console.WriteLine("Type help to print this help again.");
            Console.WriteLine("--------------------------------------------------------------------------------------------------");
        }

        private static void RequestLoop()
        {
            //Stopwatch stopWatch = new Stopwatch();
            int recTimeUsecs;

            int dataWanted;
            bool printData;

            bool first_run = true;
            int loopCounter = 0;
            string request = "help";
            const int loopsPerCommand = 100;

            //int minSpeed, maxSpeed, avgSpeed;
            //bool displayAsFrame = false;
            //bool clearScreenBetweenFrames = false;
            //int colsPerRow = 32;

            byte[] sendBuffer = new byte[256];
            byte[] recBuffer = new byte[65536 + 1];

            while (true)
            {
                dataWanted = 0;
                printData = false;

                if (first_run)
                {
                    first_run = false;
                    printHelp();
                }

                if (loopCounter <= 0)
                {
                    Console.Write("\nType the command: ");
                    request = Console.ReadLine();
                }

                //Prepare the buffer to be sent to TCP server (Photon)
                sendBuffer = Encoding.ASCII.GetBytes(request);

                if (request.ToLower() == "exit")
                {
                    SendMessageToServer(sendBuffer);
                    Exit();
                }

                else if (request.ToLower() == "help")
                {
                    printHelp();
                    continue;
                }

                else if (request.ToLower() == "clear")
                {
                    Console.Clear();
                    continue;
                }

                else if (request.ToLower() == "speed")
                {
                    loopCounter = 1;
                    dataWanted = 4;
                    printData = true;
                }

                else if (request.ToLower() == "chunk")
                {
                    loopCounter = 1;
                    dataWanted = 4;
                    printData = true;
                }

                else if (request.ToLower() == "clear")
                {
                    Console.Clear();
                    continue;
                }

                else if (request.StartsWith("loopdummy"))
                {
                    loopCounter = loopsPerCommand;
                    dataWanted = (request.Length == 9) ? 14 * 24 * 2 : getChunkSize(request[request.Length - 1]);
                    sendBuffer = Encoding.ASCII.GetBytes(request.Substring(4));
                }

                else if (request.StartsWith("dummy"))
                {
                    loopCounter = 1;
                    dataWanted = (request.Length == 5) ? 14 * 24 * 2 : getChunkSize(request[request.Length - 1]);
                    printData = true;
                }

                else if (request.StartsWith("loopframe"))
                {
                    loopCounter = loopsPerCommand;
                    dataWanted = (request.Length == 9) ? 14 * 24 * 2 : getChunkSize(request[request.Length - 1]);
                    sendBuffer = Encoding.ASCII.GetBytes(request.Substring(4));
                }

                else if (request.StartsWith("frame"))
                {
                    loopCounter = 1;
                    dataWanted = (request.Length == 5) ? 14 * 24 * 2 : getChunkSize(request[request.Length - 1]);
                    printData = true;
                    //displayAsFrame = true;
                }

                else if (request.ToLower() == "id")
                {
                    loopCounter = 1;
                    dataWanted = 6;
                    printData = true;
                }

                else if (request.ToLower() == "raw")
                {
                    loopCounter = 1;
                    dataWanted = 2 * 24 * 14;
                    printData = true;
                }

                else if (request.ToLower() == "reconnect")
                {
                    break;
                }

                else if (request.ToLower() == "demo")
                {
                    Console.Clear();
                    sendBuffer = Encoding.ASCII.GetBytes("frame");
                    loopCounter = -1;
                    for (int i = 0; i < 200; i++)
                    {
                        SendMessageToServer(sendBuffer);
                        int received = ReceiveDataFromServer(recBuffer, 14 * 24 * 2, 100, out recTimeUsecs, true);

                        Console.Clear();
                        Console.WriteLine();
                        DisplayFrame(recBuffer, 14, 24, true);
                        String s = String.Format("Summary: received {0} bytes in {1:0} miliseconds ({2:1} kb/s). Frame no. {3}/200",
                                received, recTimeUsecs/1000, received / recTimeUsecs / 1000, i);
                        Console.WriteLine(s);
                        Thread.Sleep(50);
                        if (Console.KeyAvailable)
                        {
                            Console.ReadKey(true);
                            break;
                        }

                    }
                }

                else if (request.Contains("heat"))
                {
                    sendBuffer = Encoding.ASCII.GetBytes("heat");
                    loopCounter = -1;
                    int iterations = 500;
                    int[] speeds = new int[iterations];
                    for (int i = 0; i < iterations; i++)
                    {
                        SendMessageToServer(sendBuffer);
                        int received = ReceiveDataFromServer(recBuffer, 14 * 24 * 2, 100, out recTimeUsecs, true);
                        speeds[i] = recTimeUsecs;
                        Thread.Sleep(5);
                        Console.Clear();
                        Console.WriteLine("Press any key to stop reading...");
                        Console.WriteLine();
                        DisplayFrame(recBuffer, 14, 24, false);
                        String s = String.Format("Summary: received {0} bytes in {1:0} miliseconds ({2:1} kb/s). Frame no. {3}/{4}",
                                received, recTimeUsecs / 1000, received / recTimeUsecs / 1000, i+1, iterations);
                        Console.WriteLine(s);
                        if (Console.KeyAvailable)
                        {
                            Console.ReadKey(true);
                            iterations = i;
                            break;
                        }
                    }
                    Console.WriteLine();
                    Console.WriteLine("Summary of transaction times in ms: ");
                    int cols = 0;
                    for (int i = 0; i < iterations-1; i++)
                    {
                        Console.Write("{0,3}, ", (Decimal)(speeds[i] / 1000));
                        if (cols++ == 20)
                        {
                            cols = 0;
                            Console.WriteLine();
                        }
                    }
                    Console.Write("{0,3}.", (Decimal)(speeds[iterations-1] / 1000));
                    Console.WriteLine();
                }

                else if (request.ToLower().Contains("-r"))
                {
                    loopCounter = 1;
                    dataWanted = Int32.Parse(request.Substring(request.LastIndexOf(" ")));
                    printData = true;
                }

                if (loopCounter == 0)
                {
                    SendMessageToServer(sendBuffer);
                }

                int ansLen = 0;
                while (loopCounter > 0)
                {
                    loopCounter--;

                    ansLen = SendMessageAndGetResponse(sendBuffer, recBuffer, dataWanted, 1000, out recTimeUsecs, true);
                    if (printData)
                    {
                        printReceivedData(recBuffer, ansLen);
                    }
                }

                //process the response
                if (ansLen > 0)
                {
                    if (request.ToLower() == "speed")
                    {
                        int speed = 0;
                        speed += recBuffer[3];
                        speed += recBuffer[2] * 256;
                        speed += recBuffer[1] * 256 * 256;
                        speed += recBuffer[0] * 256 * 256 * 256;
                        if (ansLen == 4)
                        {
                            Console.WriteLine("Reported speed is {0:N0} Hz.", speed);
                        }
                        else
                        {
                            Console.WriteLine("Wrong answer length. The speed is pobably {0:N0} Hz.", speed);
                        }
                    }
                    if (request.ToLower() == "chunk")
                    {
                        int speed = recBuffer[3];
                        speed += 256 * recBuffer[2];
                        speed += 256 * 256 * recBuffer[1];
                        speed += 256 * 256 * 256 * recBuffer[0];
                        if (ansLen == 4)
                        {
                            Console.WriteLine("Reported chunk size is {0:N0} bytes.", speed);
                        }
                        else
                        {
                            Console.WriteLine("Wrong answer length. The chunk size is pobably {0:N0} bytes.", speed);
                        }
                    }
                }
            }
        }

        private static void printReceivedData(byte[] data, int size)
        {
            int cols = 32;
            DisplayData(data, cols, size);
            //DisplayFrame(data, cols, size / cols);
        }

        private static int ReceiveDataFromServer(byte[] recBuffer, int dataWanted, int timeout, out int microseconds, bool logData)
        {
            int totalBytes = 0;
            int received = 0;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            TimeSpan ts = TimeSpan.MinValue;
            microseconds = 1;
            int dataRemaining = dataWanted;

            while (dataRemaining > 0)
            {
                try
                {
                    received = _clientSocket.Receive(recBuffer, totalBytes, _clientSocket.Available, SocketFlags.None);
                    ts = stopWatch.Elapsed;
                    dataRemaining -= received;
                    totalBytes += received;
                }
                catch (SocketException e)
                {
                    Console.WriteLine("{0} Error code: {1}.", e.Message, e.ErrorCode);
                    return 0;
                }
                catch (ObjectDisposedException e)
                {
                    Console.WriteLine("{0} - Socket not opened", e.Message);
                    return 0;
                }

                if (received > 0)
                {
                    // Format and display the TimeSpan value.
                    double ms = ts.TotalMilliseconds;
                    double speed = received / ms;
                    String s = String.Format("Received {0} bytes in {1} ms ({2} kb/s).",
                        received, ms.ToString("F1"), speed.ToString("F1"));

                    if (logData)
                        Console.WriteLine(s);
                }
            }
            ts = stopWatch.Elapsed;
            microseconds = (int)(1000*ts.TotalMilliseconds);
            return totalBytes;
        }

        private static void SendMessageToServer(byte[] sendBuffer)
        {
            try
            {
                _clientSocket.Send(sendBuffer);
            }
            catch (SocketException e)
            {
                Console.WriteLine("{0} Error code: {1}.", e.Message, e.ErrorCode);
            }
            catch (ObjectDisposedException e)
            {
                Console.WriteLine("{0} - Socket not opened", e.Message);
            }
        }

        private static int SendMessageAndGetResponse(
            byte[] sendMsg, byte[] recBuffer, int dataWanted, int timeout, out int microseconds, bool logData)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            SendMessageToServer(sendMsg);
            int bytesRead = 0;
            int recTime;
            bytesRead = ReceiveDataFromServer(recBuffer, dataWanted, timeout, out recTime, logData);
            microseconds = (int)(sw.ElapsedMilliseconds * 1000);
            if (microseconds == 0)
                microseconds = 1;
            double ms = microseconds / 1000;
            double speed = bytesRead / ms;
            String s = String.Format("Transaction summary: Received {0} bytes in {1} ms ({2} kb/s).",
                bytesRead, ms.ToString("F1"), speed.ToString("F1"));
            Console.WriteLine(s);

            return bytesRead;
        }

        /// <summary>
        /// Close socket and exit app
        /// </summary>
        private static void Exit()
        {
            //_clientSocket.Shutdown(SocketShutdown.Both);
            _clientSocket.Close();
            Environment.Exit(0);
        }

        private static void DisplayData(byte[] buf, int columns, int len)
        {
            int colCnt = 0;
            StringBuilder s = new StringBuilder();
            for(int i = 0; i < len; i++)
            {
                s.Append(buf[i].ToString("X2"));
                if (++colCnt < columns)
                    s.Append(" ");
                else
                {
                    s.Append("\n");
                    colCnt = 0;
                }
            }
            Console.WriteLine(s.ToString());
        }

        private static void DisplayFrame(byte[] buf, int columns, int rows, bool both)
        {
            StringBuilder s = new StringBuilder();

            if (both == true)
            {
                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < columns; j++)
                    {
                        ushort val = 0;
                        val += (ushort)(buf[i * columns * 2 + j * 2 + 1] << 8);
                        val += buf[i * columns * 2 + j * 2 + 0];
                        s.Append(val.ToString("X4"));
                        s.Append("  ");
                    }
                    s.Append("\n");
                }
                Console.WriteLine(s.ToString());
                Console.WriteLine();
            }

            s.Clear();
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    int val = 0;
                    val += buf[i * columns * 2 + j * 2 + 1] << 8;
                    val += buf[i * columns * 2 + j * 2 + 0];

                    //if (val < 0)
                    //    s.Append(val.ToString("D4"));
                    //else if (val <= 9999)
                    //    s.Append(" " + val.ToString("D4"));
                    //else
                    //    s.Append("" + val.ToString("D5"));
                    s.Append(String.Format("{0,4}", val));
                    s.Append(" ");
                }
                s.Append("\n");
            }

            Console.WriteLine(s.ToString());
        }

    }
}

//***********************************************************************************************************************

public sealed class IPAddressTools
{
    public static UInt32 ConvertIPv4AddressToUInt32(IPAddress address)
    {
        if (address == null)
            throw new ArgumentNullException("address", "The value of address is a null reference.");

        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new ArgumentException("The specified address's family is invalid.", "address");

        Byte[] addressBytes = address.GetAddressBytes();
        UInt32 addressInteger =
              (((UInt32)addressBytes[0]) << 24)
            + (((UInt32)addressBytes[1]) << 16)
            + (((UInt32)addressBytes[2]) << 8)
            + ((UInt32)addressBytes[3]);
        return addressInteger;
    }

    public static IPAddress ConvertUInt32ToIPv4Address(UInt32 addressInteger)
    {
        if (addressInteger < 0 || addressInteger > 4294967295)
            throw new ArgumentOutOfRangeException("addressInteger", "The value of addressInteger must be between 0 and 4294967295.");

        Byte[] addressBytes = new Byte[4];
        addressBytes[0] = (Byte)((addressInteger >> 24) & 0xFF);
        addressBytes[1] = (Byte)((addressInteger >> 16) & 0xFF);
        addressBytes[2] = (Byte)((addressInteger >> 8) & 0xFF);
        addressBytes[3] = (Byte)(addressInteger & 0xFF);
        return new IPAddress(addressBytes);
    }

    public static IPAddress IncrementIPAddress(IPAddress address)
    {
        return ModIPAddress(address, 1);
    }

    public static IPAddress ModIPAddress(IPAddress address, int offset)
    {
        if (address == null)
            throw new ArgumentNullException("address", "The value of address is a null reference.");

        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)

            throw new ArgumentException("The specified address's family is invalid.");

        UInt32 addressInteger = ConvertIPv4AddressToUInt32(address);
        addressInteger += (UInt32)offset;
        return ConvertUInt32ToIPv4Address(addressInteger);
    }
}

//***********************************************************************************************************************