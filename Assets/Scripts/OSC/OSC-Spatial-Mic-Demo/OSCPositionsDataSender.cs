using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Use to send OSC messages to Python for data processing before sending to MaxMSP through OSC
/// </summary>
public class OSCPositionsDataSender : MonoBehaviour
{
    # region Serialized fields

    [SerializeField] private OSC osc;
    [SerializeField] private string oscAddress = "/unity";
    [SerializeField] private List<Transform> referenceSources;

    # endregion

    public string RemoteHostIP => osc.outIP;
    public int RemotePort => osc.outPort;

    public void SetRemoteHostIP(string remoteHostIP)
    {
        osc.SetRemoteHostIP(remoteHostIP);
    }

    public void SetRemotePort(int remotePort)
    {
        osc.SetRemotePort(remotePort);
    }

    # region MonoBehaviour methods implementation

    private void Start()
    {
        scale = GetComponent<SphereArea>().radius;
        sourcesPositionLookup = new Dictionary<string, Vector3>();
        foreach (Transform referenceSource in referenceSources)
        {
            sourcesPositionLookup[referenceSource.name] = referenceSource.position;
        }
    }

    private void Update()
    {
        string stringMessage = GetModifiedPositionDataString();

        if (stringMessage == string.Empty)
        {
            counter = SEND_FRAME_INTERVAL;
            return;
        }
        
        if (counter >= SEND_FRAME_INTERVAL)
        {
            print(stringMessage);
            SendOSCMessage(stringMessage);
            counter = 0;
        }

        counter++;
    }

    # endregion

    # region Private methods for data processing and sending data to MaxMSP

    private void SendOSCMessage(string stringMessage)
    {
        OSCMessage message = new OSCMessage();
        message.address = oscAddress;
        message.values.Add(stringMessage);
        osc.Send(message);
    }

    private string GetModifiedPositionDataString()
    {
        foreach (Transform referenceSource in referenceSources)
        {
            if (referenceSource.position != sourcesPositionLookup[referenceSource.name])
            {
                sourcesPositionLookup[referenceSource.name] = referenceSource.position;
                float x = referenceSource.position.x / scale;
                float y = referenceSource.position.y / scale;
                float z = referenceSource.position.z / scale;
                return string.Format("{0} {1} {2} {3}", referenceSource.name, x, y, z);
            }
        }

        return string.Empty;
    }

    #endregion

    # region Private fields
    
    private static int SEND_FRAME_INTERVAL = 5;
    private int counter = 0;
    private float scale;
    private Dictionary<string, Vector3> sourcesPositionLookup;

    #endregion
}