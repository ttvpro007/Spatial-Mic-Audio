// Credit https://thomasfredericks.github.io/UnityOSC/

// This is version 1.01(2015.05.27)
// Tested in Unity 4
// Most of the code is based on a library for the Make Controller Kit1

using System;
using System.IO;
using System.Collections;
using System.Threading;
using System.Text;
using UnityEngine;

/// <summary>
/// The Osc class provides the methods required to send, receive, and manipulate OSC messages.
/// Several of the helper methods are static since a running Osc instance is not required for 
/// their use.
/// 
/// When instanciated, the Osc class opens the PacketIO instance that's handed to it and 
/// begins to run a reader thread.  The instance is then ready to service Send OSCMessage requests 
/// and to start supplying OSCMessages as received back.
/// 
/// The Osc class can be called to Send either individual messages or collections of messages
/// in an Osc Bundle.  Receiving is done by delegate.  There are two ways: either submit a method
/// to receive all incoming messages or submit a method to handle only one particular address.
/// 
/// Messages can be encoded and decoded from Strings via the static methods on this class, or
/// can be hand assembled / disassembled since they're just a string (the address) and a list 
/// of other parameters in Object form. 
/// 
/// </summary>
public class OSC : MonoBehaviour
{
    public delegate void OSCMessageHandler(OSCMessage oscM);

    public int inPort = 8081;
    public string outIP = "127.0.0.1";
    public int outPort = 8080;

    private UDPPacketIO OscPacketIO;
    Thread ReadThread;
    // private bool ReaderRunning;
    private OSCMessageHandler AllMessageHandler;

    Hashtable AddressTable;

    ArrayList messagesReceived;

    private object ReadThreadLock = new object();

    byte[] buffer;

    bool paused = false;


#if UNITY_EDITOR

    private void HandleOnPlayModeChanged(UnityEditor.PlayModeStateChange state) //FIX FOR UNITY POST 2017
    {
        // This method is run whenever the playmode state is changed.

        paused = UnityEditor.EditorApplication.isPaused;
        //print ("editor paused "+paused);
        // do stuff when the editor is paused.

    }

#endif

    void Awake()
    {
        OscPacketIO = new UDPPacketIO(outIP, outPort, inPort);
        AddressTable = new Hashtable();

        messagesReceived = new ArrayList();

        buffer = new byte[1000];

        ReadThread = new Thread(Read);
        // ReaderRunning = true;
        ReadThread.IsBackground = true;
        ReadThread.Start();

#if UNITY_EDITOR

        UnityEditor.EditorApplication.playModeStateChanged += HandleOnPlayModeChanged;  //FIX FOR UNITY POST 2017

#endif

    }

    void OnDestroy()
    {
        Close();
    }

    /// <summary>
    /// Set the method to call back on when a message with the specified
    /// address is received.  The method needs to have the OSCMessageHandler signature - i.e. 
    /// void amh( OSCMessage oscM )
    /// </summary>
    /// <param name="key">Address string to be matched</param>   
    /// <param name="ah">The method to call back on.</param>   
    public void SetAddressHandler(string key, OSCMessageHandler ah)
    {
        ArrayList al = (ArrayList)Hashtable.Synchronized(AddressTable)[key];
        if (al == null)
        {
            al = new ArrayList();
            al.Add(ah);
            Hashtable.Synchronized(AddressTable).Add(key, al);
        }
        else
        {
            al.Add(ah);
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
#if !UNITY_EDITOR

		paused = pauseStatus;
		print ("Application paused : " + pauseStatus);

#endif
    }

    void Update()
    {
        if (messagesReceived.Count > 0)
        {
            //Debug.Log("received " + messagesReceived.Count + " messages");
            lock (ReadThreadLock)
            {
                foreach (OSCMessage om in messagesReceived)
                {

                    if (AllMessageHandler != null)
                        AllMessageHandler(om);

                    ArrayList al = (ArrayList)Hashtable.Synchronized(AddressTable)[om.address];
                    if (al != null)
                    {
                        foreach (OSCMessageHandler h in al)
                        {
                            h(om);
                        }
                    }
                }

                messagesReceived.Clear();
            }
        }
    }

    /// <summary>
    /// Make sure the PacketExchange is closed.
    /// </summary>
    public void Close()
    {
        // if (ReaderRunning)
        if (ReadThread.ThreadState == ThreadState.Running)
        {
            // ReaderRunning = false;
            ReadThread.Abort();
        }

        if (OscPacketIO != null && OscPacketIO.IsOpen())
        {
            OscPacketIO.Close();
            OscPacketIO = null;
        }
    }

    /// <summary>
    /// Read Thread.  Loops waiting for packets.  When a packet is received, it is 
    /// dispatched to any waiting All Message Handler.  Also, the address is looked up and
    /// any matching handler is called.
    /// </summary>
    private void Read()
    {
        try
        {
            // while (ReaderRunning)
            while (ReadThread.ThreadState == ThreadState.Running)
            {
                int length = OscPacketIO.ReceivePacket(buffer);

                if (length > 0)
                {
                    lock (ReadThreadLock)
                    {
                        if (paused == false)
                        {
                            ArrayList newMessages = OSC.PacketToOSCMessages(buffer, length);
                            messagesReceived.AddRange(newMessages);
                        }
                    }
                }
                else
                    Thread.Sleep(5);
            }
        }

        catch (Exception e)
        {
            Debug.Log("ThreadAbortException" + e);
        }
        finally
        {

        }
    }

    /// <summary>
    /// Send an individual OSC message.  Internally takes the OSCMessage object and 
    /// serializes it into a byte[] suitable for sending to the PacketIO.
    /// </summary>
    /// <param name="oscMessage">The OSC Message to send.</param>   
    public void Send(OSCMessage oscMessage)
    {
        byte[] packet = new byte[1000];
        int length = OSC.OSCMessageToPacket(oscMessage, packet, 1000);
        OscPacketIO.SendPacket(packet, length);
    }

    /// <summary>
    /// Sends a list of OSC Messages.  Internally takes the OSCMessage objects and 
    /// serializes them into a byte[] suitable for sending to the PacketExchange.
    /// </summary>
    /// <param name="oms">The OSC Message to send.</param>   
    public void Send(ArrayList oms)
    {
        byte[] packet = new byte[1000];
        int length = OSC.OSCMessagesToPacket(oms, packet, 1000);
        OscPacketIO.SendPacket(packet, length);
    }

    /// <summary>
    /// Set the method to call back on when any message is received.
    /// The method needs to have the OSCMessageHandler signature - i.e. void amh( OSCMessage oscM )
    /// </summary>
    /// <param name="amh">The method to call back on.</param>   
    public void SetAllMessageHandler(OSCMessageHandler amh)
    {
        AllMessageHandler = amh;
    }

    public void SetRemoteHostIP(string remoteHostIP)
    {
        outIP = remoteHostIP;
        OscPacketIO.RemoteHostIP = outIP;
    }

    public void SetRemotePort(int remotePort)
    {
        outPort = remotePort;
        OscPacketIO.RemotePort = outPort;
    }

    /// <summary>
    /// Creates an OSCMessage from a string - extracts the address and determines each of the values. 
    /// </summary>
    /// <param name="message">The string to be turned into an OSCMessage</param>
    /// <returns>The OSCMessage.</returns>
    public static OSCMessage StringToOSCMessage(string message)
    {
        OSCMessage oM = new OSCMessage();
        Console.WriteLine("Splitting " + message);
        string[] ss = message.Split(new char[] { ' ' });
        IEnumerator sE = ss.GetEnumerator();
        if (sE.MoveNext())
            oM.address = (string)sE.Current;
        while (sE.MoveNext())
        {
            string s = (string)sE.Current;
            // Console.WriteLine("  <" + s + ">");
            if (s.StartsWith("\""))
            {
                StringBuilder quoted = new StringBuilder();
                bool looped = false;
                if (s.Length > 1)
                    quoted.Append(s.Substring(1));
                else
                    looped = true;
                while (sE.MoveNext())
                {
                    string a = (string)sE.Current;
                    // Console.WriteLine("    q:<" + a + ">");
                    if (looped)
                        quoted.Append(" ");
                    if (a.EndsWith("\""))
                    {
                        quoted.Append(a.Substring(0, a.Length - 1));
                        break;
                    }
                    else
                    {
                        if (a.Length == 0)
                            quoted.Append(" ");
                        else
                            quoted.Append(a);
                    }
                    looped = true;
                }
                oM.values.Add(quoted.ToString());
            }
            else
            {
                if (s.Length > 0)
                {
                    try
                    {
                        int i = int.Parse(s);
                        // Console.WriteLine("  i:" + i);
                        oM.values.Add(i);
                    }
                    catch
                    {
                        try
                        {
                            float f = float.Parse(s);
                            // Console.WriteLine("  f:" + f);
                            oM.values.Add(f);
                        }
                        catch
                        {
                            // Console.WriteLine("  s:" + s);
                            oM.values.Add(s);
                        }
                    }
                }
            }
        }

        return oM;
    }

    /// <summary>
    /// Takes a packet (byte[]) and turns it into a list of OSCMessages.
    /// </summary>
    /// <param name="packet">The packet to be parsed.</param>
    /// <param name="length">The length of the packet.</param>
    /// <returns>An ArrayList of OSCMessages.</returns>
    public static ArrayList PacketToOSCMessages(byte[] packet, int length)
    {
        ArrayList messages = new ArrayList();
        ExtractMessages(messages, packet, 0, length);
        return messages;
    }

    /// <summary>
    /// Puts an array of OSCMessages into a packet (byte[]).
    /// </summary>
    /// <param name="messages">An ArrayList of OSCMessages.</param>
    /// <param name="packet">An array of bytes to be populated with the OSCMessages.</param>
    /// <param name="length">The size of the array of bytes.</param>
    /// <returns>The length of the packet</returns>
    public static int OSCMessagesToPacket(ArrayList messages, byte[] packet, int length)
    {
        int index = 0;
        if (messages.Count == 1)
            index = OSCMessageToPacket((OSCMessage)messages[0], packet, 0, length);
        else
        {
            // Write the first bundle bit
            index = InsertString("#bundle", packet, index, length);
            // Write a null timestamp (another 8bytes)
            int c = 8;
            while ((c--) > 0)
                packet[index++]++;
            // Now, put each message preceded by it's length
            foreach (OSCMessage oscM in messages)
            {
                int lengthIndex = index;
                index += 4;
                int packetStart = index;
                index = OSCMessageToPacket(oscM, packet, index, length);
                int packetSize = index - packetStart;
                packet[lengthIndex++] = (byte)((packetSize >> 24) & 0xFF);
                packet[lengthIndex++] = (byte)((packetSize >> 16) & 0xFF);
                packet[lengthIndex++] = (byte)((packetSize >> 8) & 0xFF);
                packet[lengthIndex++] = (byte)((packetSize) & 0xFF);
            }
        }

        return index;
    }

    /// <summary>
    /// Creates a packet (an array of bytes) from a single OSCMessage.
    /// </summary>
    /// <remarks>A convenience method, not requiring a start index.</remarks>
    /// <param name="oscM">The OSCMessage to be returned as a packet.</param>
    /// <param name="packet">The packet to be populated with the OSCMessage.</param>
    /// <param name="length">The usable size of the array of bytes.</param>
    /// <returns>The length of the packet</returns>
    public static int OSCMessageToPacket(OSCMessage oscM, byte[] packet, int length)
    {
        return OSCMessageToPacket(oscM, packet, 0, length);
    }

    /// <summary>
    /// Creates an array of bytes from a single OSCMessage.  Used internally.
    /// </summary>
    /// <remarks>Can specify where in the array of bytes the OSCMessage should be put.</remarks>
    /// <param name="oscM">The OSCMessage to be turned into an array of bytes.</param>
    /// <param name="packet">The array of bytes to be populated with the OSCMessage.</param>
    /// <param name="start">The start index in the packet where the OSCMessage should be put.</param>
    /// <param name="length">The length of the array of bytes.</param>
    /// <returns>The index into the packet after the last OSCMessage.</returns>
    private static int OSCMessageToPacket(OSCMessage oscM, byte[] packet, int start, int length)
    {
        int index = start;
        index = InsertString(oscM.address, packet, index, length);
        //if (oscM.values.Count > 0)
        {
            StringBuilder tag = new StringBuilder();
            tag.Append(",");
            int tagIndex = index;
            index += PadSize(2 + oscM.values.Count);

            foreach (object o in oscM.values)
            {
                if (o is int)
                {
                    int i = (int)o;
                    tag.Append("i");
                    packet[index++] = (byte)((i >> 24) & 0xFF);
                    packet[index++] = (byte)((i >> 16) & 0xFF);
                    packet[index++] = (byte)((i >> 8) & 0xFF);
                    packet[index++] = (byte)((i) & 0xFF);
                }
                else
                {
                    if (o is float)
                    {
                        float f = (float)o;
                        tag.Append("f");
                        byte[] buffer = new byte[4];
                        MemoryStream ms = new MemoryStream(buffer);
                        BinaryWriter bw = new BinaryWriter(ms);
                        bw.Write(f);
                        packet[index++] = buffer[3];
                        packet[index++] = buffer[2];
                        packet[index++] = buffer[1];
                        packet[index++] = buffer[0];
                    }
                    else
                    {
                        if (o is string)
                        {
                            tag.Append("s");
                            index = InsertString(o.ToString(), packet, index, length);
                        }
                        else
                        {
                            tag.Append("?");
                        }
                    }
                }
            }
            InsertString(tag.ToString(), packet, tagIndex, length);
        }

        return index;
    }

    /// <summary>
    /// Receive a raw packet of bytes and extract OSCMessages from it.  Used internally.
    /// </summary>
    /// <remarks>The packet may contain a OSC message or a bundle of messages.</remarks>
    /// <param name="messages">An ArrayList to be populated with the OSCMessages.</param>
    /// <param name="packet">The packet of bytes to be parsed.</param>
    /// <param name="start">The index of where to start looking in the packet.</param>
    /// <param name="length">The length of the packet.</param>
    /// <returns>The index after the last OSCMessage read.</returns>
    private static int ExtractMessages(ArrayList messages, byte[] packet, int start, int length)
    {
        int index = start;
        switch ((char)packet[start])
        {
            case '/':
                index = ExtractMessage(messages, packet, index, length);
                break;
            case '#':
                string bundleString = ExtractString(packet, start, length);
                if (bundleString == "#bundle")
                {
                    // skip the "bundle" and the timestamp
                    index += 16;
                    while (index < length)
                    {
                        int messageSize = (packet[index++] << 24) + (packet[index++] << 16) + (packet[index++] << 8) + packet[index++];
                        /*int newIndex = */
                        ExtractMessages(messages, packet, index, length);
                        index += messageSize;
                    }
                }
                break;
        }

        return index;
    }

    /// <summary>
    /// Extracts a messages from a packet.
    /// </summary>
    /// <param name="messages">An ArrayList to be populated with the OSCMessage.</param>
    /// <param name="packet">The packet of bytes to be parsed.</param>
    /// <param name="start">The index of where to start looking in the packet.</param>
    /// <param name="length">The length of the packet.</param>
    /// <returns>The index after the OSCMessage is read.</returns>
    private static int ExtractMessage(ArrayList messages, byte[] packet, int start, int length)
    {
        OSCMessage oscM = new OSCMessage();
        oscM.address = ExtractString(packet, start, length);
        int index = start + PadSize(oscM.address.Length + 1);
        string typeTag = ExtractString(packet, index, length);
        index += PadSize(typeTag.Length + 1);
        //oscM.values.Add(typeTag);
        foreach (char c in typeTag)
        {
            switch (c)
            {
                case ',':
                    break;
                case 's':
                    {
                        string s = ExtractString(packet, index, length);
                        index += PadSize(s.Length + 1);
                        oscM.values.Add(s);
                        break;
                    }
                case 'i':
                    {
                        int i = (packet[index++] << 24) + (packet[index++] << 16) + (packet[index++] << 8) + packet[index++];
                        oscM.values.Add(i);
                        break;
                    }
                case 'f':
                    {
                        byte[] buffer = new byte[4];
                        buffer[3] = packet[index++];
                        buffer[2] = packet[index++];
                        buffer[1] = packet[index++];
                        buffer[0] = packet[index++];
                        MemoryStream ms = new MemoryStream(buffer);
                        BinaryReader br = new BinaryReader(ms);
                        float f = br.ReadSingle();
                        oscM.values.Add(f);
                        break;
                    }
            }
        }

        messages.Add(oscM);

        return index;
    }

    /// <summary>
    /// Removes a string from a packet.  Used internally.
    /// </summary>
    /// <param name="packet">The packet of bytes to be parsed.</param>
    /// <param name="start">The index of where to start looking in the packet.</param>
    /// <param name="length">The length of the packet.</param>
    /// <returns>The string</returns>
    private static string ExtractString(byte[] packet, int start, int length)
    {
        StringBuilder sb = new StringBuilder();
        int index = start;
        while (packet[index] != 0 && index < length)
            sb.Append((char)packet[index++]);

        return sb.ToString();
    }

    private static string Dump(byte[] packet, int start, int length)
    {
        StringBuilder sb = new StringBuilder();
        int index = start;
        while (index < length)
            sb.Append(packet[index++] + "|");

        return sb.ToString();
    }

    /// <summary>
    /// Inserts a string, correctly padded into a packet.  Used internally.
    /// </summary>
    /// <param name="string">The string to be inserted</param>
    /// <param name="packet">The packet of bytes to be parsed.</param>
    /// <param name="start">The index of where to start looking in the packet.</param>
    /// <param name="length">The length of the packet.</param>
    /// <returns>An index to the next byte in the packet after the padded string.</returns>
    private static int InsertString(string s, byte[] packet, int start, int length)
    {
        int index = start;
        foreach (char c in s)
        {
            packet[index++] = (byte)c;
            if (index == length)
                return index;
        }
        packet[index++] = 0;
        int pad = (s.Length + 1) % 4;
        if (pad != 0)
        {
            pad = 4 - pad;
            while (pad-- > 0)
                packet[index++] = 0;
        }

        return index;
    }

    /// <summary>
    /// Takes a length and returns what it would be if padded to the nearest 4 bytes.
    /// </summary>
    /// <param name="rawSize">Original size</param>
    /// <returns>padded size</returns>
    private static int PadSize(int rawSize)
    {
        int pad = rawSize % 4;
        if (pad == 0)
            return rawSize;
        else
            return rawSize + (4 - pad);
    }
}