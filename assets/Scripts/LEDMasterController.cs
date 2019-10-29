using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;
using UnityEngine.UI;

using System.IO.Ports;


public class LEDMasterController : MonoBehaviour
{

    //////////////////////////////////
    //
    // Private Variable
    //
    //////////////////////////////////
    /// <summary>
    public string m_portName = "COM0"; // should be specified in the inspector
    SerialPort m_serialPort;


    /// </summary>
    //SerialToArduinoMgr m_SerialToArduinoMgr; 
    Thread m_Thread;

    //public SerialPort m_port;

    public byte[] m_LEDArray; // 200  LEDs

    float m_Delay;
    public const int m_LEDCount = 200; // m_LEDCount = 200

    //////////////////////////////////
    //
    // Function
    //
    //////////////////////////////////

    // Setting for events from LEDColorGenController
    LEDColorGenController m_ledColorGenController;

    private void Awake()
    { // init me

        // Serial Communication between C# and Arduino
        //http://www.hobbytronics.co.uk/arduino-serial-buffer-size = how to change the buffer size of arduino
        //https://www.codeproject.com/Articles/473828/Arduino-Csharp-and-Serial-Interface
        //Don't forget that the Arduino reboots every time you open the serial port from the computer - 
        //and the bootloader runs for a second or two gobbling up any serial data you send its way..
        //This takes a second or so, and during that time any data that you send is lost.
        //https://forum.arduino.cc/index.php?topic=459847.0

        //https://docs.microsoft.com/en-us/dotnet/api/system.io.ports.serialport.writetimeout?view=netframework-4.8

        // Set up the serial Port

        m_serialPort = new SerialPort(m_portName, 115200); // bit rate= 567000 bps


        //m_SerialPort.ReadTimeout = 50;
       // m_serialPort.ReadTimeout = 1000;  // sets the timeout value: 1000 ms  = sufficient for our purpose?
                                          // InfiniteTimeout is the default.
                                          //  m_SerialPort1.WriteTimeout = 5000??
        m_serialPort.Open();


        //m_SerialToArduinoMgr = new SerialToArduinoMgr();

        //m_SerialToArduinoMgr.Setup();

        //m_port = m_SerialToArduinoMgr.port;

        m_LEDArray = new byte[m_LEDCount * 3]; // 280*3 = 840 < 1024



    }

    void Start()
    {


        //m_ledColorGenController = gameObject.GetComponent<LEDColorGenController>();
        ////It is assumed that all the necessary components are already attached to CommHub gameObject, which  is referred to by
        //// gameObject field variable. gameObject.GetComponent<LEDColorGenController>() == this.gameObject.GetComponent<LEDColorGenController>();
        //if (m_ledColorGenController == null)
        //{
        //    Debug.LogError("The global Variable  m_ledColorGenController is not  defined");
        //    Application.Quit();
        //}

        //m_ledColorGenController.m_ledSenderHandler += UpdateLEDArray;

        // public delegate LEDSenderHandler (byte[] LEDArray); defined in LEDColorGenController
        // public event LEDSenderHandler m_ledSenderHandler;


        // define an action
        Action updateArduino = () => {

            // Write(byte[] buffer, int offset, int count);
            m_serialPort.Write(m_LEDArray, 0, m_LEDArray.Length); 
            // The WriteBufferSize of the Serial Port is 1024, whereas that of Arduino is 64
            //https://stackoverflow.com/questions/22768668/c-sharp-cant-read-full-buffer-from-serial-port-arduino

        };


        //m_Thread = null;
        //if(connected) { // create and start a thread for the action updateArduino
        m_Thread = new Thread(new ThreadStart(updateArduino)); // ThreadStart() is a delegate (pointer type)
       // m_Thread.Start();

    }


    public void UpdateLEDArray( byte[] ledArray)
    {
        // Send the new LED array only when the sending thread has finished sending the previous LEDArray
        // THat is, only when m_Thread.IsAlive is false. Tit happends when the method of the thread returns;
        // That is when the sending thread has sent all the LED array.

        if (!m_Thread.IsAlive)
        {
            m_LEDArray = ledArray;
            m_Thread.Start(); // use the new LED array for the new invocation of the sending thread
        }
        else
        {
            // The sending thread is still sending =>: The arrived LED array is discarded
        }
        
    void Update()
    {
      
    }

}//public class LEDMasterController 

