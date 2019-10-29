using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Runtime.InteropServices;

using Random = UnityEngine.Random;

public class LEDColorGenController : MonoBehaviour
{
    public SimpleBoidsTreeOfVoice m_boids;
    // 보이드의 수
    public float m_BoidsNum;
    public int m_totalNumOfLeds = 160;

    
    [SerializeField] protected ComputeShader BoidLEDComputeShader;

    //https://www.reddit.com/r/Unity3D/comments/7ppldz/physics_simulation_on_gpu_with_compute_shader_in/
  


    protected   int m_kernelIDLED;
    public ComputeBuffer m_BoidLEDBuffer { get; protected set; } // null reference


    BoidLEDData[] m_boidLEDArray;

    byte[] m_LEDArray;




    // Areas of LED lights and person

    public float m_personSpaceDepth = 1; // 1m
    public float m_personSpaceWidth = 0.5f; // m

    public float m_innerCircleRadius = 1; // m
    public float m_outerCircleRadius = 2;

    public float m_outerCircleDepth = 0.5f; //mm

    public float m_percentageOfInnerLEDs = 40;

     // delegate signature (interface definition)

    public delegate void LEDSenderHandler(byte[] m_LEDArray);
    public event LEDSenderHandler m_ledSenderHandler;

    protected const int BLOCK_SIZE = 256; // The number of threads in a single thread group

    protected const int MAX_SIZE_OF_BUFFER = 1000;

    int m_threadGroupSize;

    const float epsilon = 1e-2f;
    const float M_PI = 3.1415926535897932384626433832795f;

    float m_SceneStartTime; // = Time.time;
    float m_SceneDuration = 390.0f; // 120 seconds

   
    
    public struct BoidLEDData
    {
        // public Vector3  WallOrigin; // the reference position of the wall (the boid reference frame) on which the boid is 

        //public Vector3 EulerAngles; // the rotation of the boid reference frame
        public Vector3 Position; //

        public Vector4 Color;         // RGBA color
        public int WallNo;      // the number of the wall whose boids defined the light sources of the branch cylinder
                                // 0=> the core circular wall. 
                                // 1 => the outer circular wall;
    }

    public struct BranchCylinder
    {
        // public Vector3  WallOrigin; // the reference position of the wall (the boid reference frame) on which the boid is 

        //public Vector3 EulerAngles; // the rotation of the boid reference frame
        public Vector3 Position; // the position of the  cylinder origin in the boid reference frame        
        public float Height;

        public float Radius; // the radius of the cylinder
        public Vector4 Color;         // RGBA color of the cylinder; This color is a weighted sum of the colors of the neighbor
                                      // boids of the branch cylinder which is located at Position

        public int WallNo;      // the number of the wall whose boids defined the light sources of the branch cylinder
                                // 0=> the core circular wall. 
                                // 1 => the outer circular wall;
    }



   
    // ComputeBuffer: GPU data buffer, mostly for use with compute shaders.
    // you can create & fill them from script code, and use them in compute shaders or regular shaders.


    // Declare other Component _boids; you can drag any gameobject that has that Component attached to it.
    // This will acess the Component directly rather than the gameobject itself.


    //[SerializeField]  SimpleBoidsTreeOfVoice _boids; // _boids.BoidBuffer is a ComputeBuffer
    //[SerializeField] Material _instanceMaterial;

    // [SerializeField] protected Vector3 RoomMinCorner = new Vector3(-10f, 0f, -10f);
    // [SerializeField] protected Vector3 RoomMaxCorner = new Vector3(10f, 12f, 10f);


    // 보이드의 수
    // public int BoidsNum = 256;


    //m_neuroHeadSetController.onAverageSignalReceived += m_ledColorGenController.UpdateLEDResponseParameter;
    //m_irSensorMasterController.onAverageSignalReceived += m_ledColorGenController.UpdateColorBrightnessParameter;

    void Start()
    {

        //m_threadGroupSize = Mathf.CeilToInt(m_BoidsNum / (float)BLOCK_SIZE);

        m_threadGroupSize = Mathf.CeilToInt(m_totalNumOfLeds / (float)BLOCK_SIZE);
        
        m_boids = this.gameObject.GetComponent<SimpleBoidsTreeOfVoice>();

        if (m_boids== null)
        {
            Debug.LogError("impleBoidsTreeOfVoice component should be added to CommHub");
           // Application.Quit();

        }

        m_BoidsNum = (int)m_boids.m_BoidsNum;

        m_LEDArray = new byte[m_totalNumOfLeds * 3];

        m_kernelIDLED  = BoidLEDComputeShader.FindKernel("SampleLEDColors");

        //define BoidLED

        m_BoidLEDBuffer = new ComputeBuffer(m_totalNumOfLeds, Marshal.SizeOf(typeof(BoidLEDData)) );

        m_boidLEDArray = new BoidLEDData[m_totalNumOfLeds ];
      


        //
        //For each kernel we are setting the buffers that are used by the kernel, so it would read and write to those buffers

        // For the part of boidArray that is set by data are filled by null. 
        // When the array boidArray is created each element is set by null.

        m_BoidLEDBuffer.SetData(m_boidLEDArray); // buffer is R or RW
      

        BoidLEDComputeShader.SetBuffer( mKernelIdGround, "_BoidBuffer", BoidBuffer);
        //BoidComputeShader.SetBuffer(KernelIdGround, "_BoidCountBuffer", BoidCountBuffer);
        BoidComputeShader.SetBuffer(KernelIdCeiling, "_BoidBuffer", BoidBuffer);
        //BoidComputeShader.SetBuffer(KernelIdCeiling, "_BoidCountBuffer", BoidCountBuffer);


        BoidComputeShader.SetBuffer(KernelIdCountBoids, "_BoidCountBuffer", BoidCountBuffer);
        BoidComputeShader.SetBuffer(KernelIdCountBoids, "_BoidBuffer", BoidBuffer);



        InitializeValues();
        InitializeBuffers();
    }


    public void UpdateLEDResponseParameter(double[] electrodeData) {
    }

    public 
        void UpdateColorBrightnessParameter(int[] irDistances) {
    }


    void Update()
    {
        // Get the current values of the boids from the boid computeShader
        m_boids.BoidBuffer.GetData(m_boids.m_boidArray);

        //public static float/iny Range(float min, float max);

        for (int i = 0; i < m_totalNumOfLeds; i++)
        {
            int k = Random.Range(0, (int) m_BoidsNum);
            //_boids.BoidBuffer
            m_LEDArray[i * 3] = (byte) (255 * m_boids.m_boidArray[k].Color[0] ); // Vector4 Color
            m_LEDArray[i * 3 +1] = (byte) ( 255 * m_boids.m_boidArray[k].Color[1] );
            m_LEDArray[i * 3 +2] = (byte) (255 *  m_boids.m_boidArray[k].Color[2] );


        }
        //m_ledSenderHandler.Invoke( m_LEDArray ) when m_LEDArray is ready

        m_ledSenderHandler.Invoke( m_LEDArray) ;

 
     } // Update()



} //  LEDColorGenController class
