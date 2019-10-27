using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using Random = UnityEngine.Random;

public class LEDColorGenController : MonoBehaviour
{
    // Areas of LED lights and person

    public float personDepth = 1; // 1m
    public float personWidth = 0.5f; // m

    public float innerCircleRadius = 1; // m
    public float outerCircleRadius = 2;
         
    // Setup events for sending LED data to m_LEDMasterController

    public int m_totalNumOfLeds = 7 + 5 + 10 + 10;

    public delegate void LEDSenderHandler(byte[] LEDArray);
    public event LEDSenderHandler m_ledSenderHandler;


    //SimpleBoidsTreeOfVoice _boids; // set in the inspector

    protected const int BLOCK_SIZE = 1024; // The number of threads in a single thread group

    protected const int MAX_SIZE_OF_BUFFER = 10000;

    const float epsilon = 1e-2f;
    const float M_PI = 3.1415926535897932384626433832795f;

    float m_SceneStartTime; // = Time.time;
    float m_SceneDuration = 390.0f; // 120 seconds

    // 보이드의 수
    public float m_BoidsNum;
    
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


    // 컴퓨트 쉐이더
    // Mention another Component instance.
    [SerializeField] protected ComputeShader BoidLEDComputeShader;

    //https://www.reddit.com/r/Unity3D/comments/7ppldz/physics_simulation_on_gpu_with_compute_shader_in/
    // 보이드의 버퍼
    public ComputeBuffer BoidBuffer { get; protected set; } 

    public ComputeBuffer BoidLEDBuffer { get; protected set; } 

    int BufferStartIndex, BufferEndIndex;

    protected int KernelIdLEDColor;

    // for debugging
    BoidLEDData[] boidLEDArray;
       
    byte[] m_LEDArray;

    public SimpleBoidsTreeOfVoice m_boidComponent; // specified in the inspector

    // Add m_ledSenderHandler.Invoke( m_LEDArray ) when m_LEDArray is ready

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

        if (m_boidComponent == null)
        {
            Debug.LogError("_boids component is not set in the inspector");
            Application.Quit();

        }

        m_BoidsNum = (int)m_boidComponent.m_BoidsNum;

        m_LEDArray = new byte[m_totalNumOfLeds * 3];


    }


    public void UpdateLEDResponseParameter(double[] electrodeData) {
    }

    public 
        void UpdateColorBrightnessParameter(int[] irDistances) {
    }


    void Update()
    {
      
    
    //public static float/iny Range(float min, float max);

        for (int i = 0; i < m_totalNumOfLeds; i++)
        {
            int k = Random.Range(0, (int) m_BoidsNum);

            m_LEDArray[i * 3] = (byte) (255 * m_boidComponent.m_boidArray[k].Color[0] ); // Vector4 Color
            m_LEDArray[i * 3 +1] = (byte) ( 255 * m_boidComponent.m_boidArray[k].Color[1] );
            m_LEDArray[i * 3 +2] = (byte) (255 *  m_boidComponent.m_boidArray[k].Color[2] );


        }

        m_ledSenderHandler.Invoke( m_LEDArray) ;

 
     } // Update()



} //  LEDColorGenController class
