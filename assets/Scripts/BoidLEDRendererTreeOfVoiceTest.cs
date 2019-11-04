using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using System.Runtime.InteropServices;

using Random = UnityEngine.Random;

public class BoidLEDRendererTreeOfVoiceTest : MonoBehaviour
{
    int m_totalNumOfLEDs;
    //**************************************
    public struct BoidLEDData
    {

        public Vector3 Position; //
        public Vector3 HeadDir; // heading direction of the boid on the local plane
        public Vector4 Color;         // RGBA color
        public Vector3 Scale;
        public int WallNo;      // the number of the wall whose boids defined the light sources of the branch cylinder
                                // 0=> the inner  circular wall. 
                                // 1 => the outer circular wall;
        public int NearestBoidID;
    }


    public int m_samplingWall = 1; // ceiling

    public float m_samplingRadius = 2; //m

    public int m_numberOfWalls = 2;

    //https://www.reddit.com/r/Unity3D/comments/7ppldz/physics_simulation_on_gpu_with_compute_shader_in/

    protected int m_kernelIDLED;

   // [SerializeField] protected ComputeShader m_BoidLEDComputeShader;
    // m_BoidLEDComputeShader is set to SampleLEDColors.compute in the inspector          
    public ComputeBuffer m_BoidLEDBuffer { get; protected set; }

    BoidLEDData[] m_BoidLEDArray;

    byte[] m_LEDArray;

    [Range(0.0f, 1.0f)]
    [SerializeField] protected float MinCylinderRadiusScale = 0.5f;
    [Range(0.0f, 1.0f)]
    [SerializeField] protected float MaxCylinderRadiusScale = 1.0f;

    public float m_minLEDInterval = 0.2f;
    public float m_maxLEDInterval = 0.5f;

    float m_LEDChainLength = 0;


    public int m_numOfChain1 = 40;
    public int m_numOfChain2 = 40;
    public int m_numOfChain3 = 60;
    public int m_numOfChain4 = 60;

    public float m_beginFromInChain1 = -80;
    public float m_beginFromInChain2 = -70;



    float m_startAngleOfChain1;
    float m_startAngleOfChain2;


    // delegate signature (interface definition)

    public delegate void LEDSenderHandler(byte[] m_LEDArray);
    public event LEDSenderHandler m_LEDSenderHandler;

    protected const int BLOCK_SIZE = 256; // The number of threads in a single thread group

    //protected const int MAX_SIZE_OF_BUFFER = 1000;

    int m_threadGroupSize;

    const float epsilon = 1e-2f;
    const float M_PI = 3.1415926535897932384626433832795f;


    //***************************************




    // ComputeBuffer: GPU data buffer, mostly for use with compute shaders.
    // you can create & fill them from script code, and use them in compute shaders or regular shaders.


    // Declare other Component _boids; you can drag any gameobject that has that Component attached to it.
    // This will acess the Component directly rather than the gameobject itself.


    SimpleBoidsTreeOfVoice m_boids; // _boids.BoidBuffer is a ComputeBuffer
    LEDColorGenController m_LEDColorGenController;

    [SerializeField] Material m_boidLEDInstanceMaterial;

    int BoidsNum;
    

    CircleMesh m_instanceMeshCircle;
    CylinderMesh m_instanceMeshCylinder;

    Mesh m_boidInstanceMesh;

    public float m_scale = 1.0f; // the scale of the instance mesh
      
    ComputeBuffer m_boidLEDArgsBuffer;
     
    uint[] m_boidLEDArgs = new uint[5] { 0, 0, 0, 0, 0 };

    uint numIndices;
    Vector3[] vertices3D;

    int[] indices;

    float unitRadius = 1;

    float height = 10; // m; scale = 0.1 ~ 0.3
    float radius = 0.1f; // 0.1 m =10cm

    // parameters for cylinder construction
    int nbSides = 18;
    int nbHeightSeg = 1;


    protected void SetBoidLEDArray(BoidLEDData[] m_BoidLEDArray)
    {


        float radius;
        float theta, phi;

        // Arange a chain of 40 LEDs (with interval of 50cm) along the the logarithmic spiral 
        // r = a * exp( b * theta), where 0 <= theta <= 3 * 2pi: 
        // band with radii 1m and 0.85m three  rounds. Arrange another chain along the circle band 
        // with radii 0.85m and 0.7m  three rounds, each round with differnt radii.
        //Two chains are arranged so that their LEDs are placed in a zigzag manner.

        //x = r*cos(th); y = r *sin(th)
        // x = a*exp(b*th)cos(th), y = a*exp(b*th)sin(th)
        // dr/dth = b*r; 

        //conditions: r0 = a exp( b 0) = 0.85; r1 = a exp( b* 3 * 2pi)
        // r0 = a exp(0) = a; a = r0; r1 = 0.85 * exp( b* 4pi) ==> b = the radius growth rate.

        // exp( b * 4pi) = r1/ a; b * 6pi = ln( r1/a). b = ln( r1/a) / 6pi;

        //  L( r(th), th0, th) = a( root( 1 + b^2) /b ) [ exp( b * th) - exp( b * th0) ]
        // = root(1 + b^2) / b * [ a*exp(b *th) - a * exp(th0) ] 
        // L( r(th), th0, th_i) =  root(1 + b^2)/b * [ r(th_i)  - r(th0)] = i * 0.5, 0.5= led Interval 
        //  => the value of th_i can be determined.

        // The ith LED  will be placed at location (r_i, th_i) such that  L(r(th_i), th0, th_i) = 0.5 * i, r_i = a*exp(b*th_i), 
        //  i =0 ~ 39

        //Define the parameters  a and b of the logarithmic spiral curve r = a * exp(b * th).

        float r0 = 1.0f;  // a1  in r = a1 * exp(b1 * th) is set so that the radius r0 is 0.7 when th =0;
        float r1 = 2.0f; // r1 = a1 exp (b1* 3 * 2pi)

        float r2 = 3.0f; //  a2  in r = a2 * exp( b2 * th); b2 is set so that r is r2 when th =0;
        float r3 = 4.0f; // r3 = a2* exp(b2* 3 * 2pi)

        float a1 = r0;
        float b1 = Mathf.Log(r1 / a1) / (6 * M_PI);

        float a2 = r2;
        float b2 = Mathf.Log(r3 / a2) / (6 * M_PI);




        Debug.Log("Inner Chain:");

        m_LEDChainLength = 0;

        for (int i = 0; i < m_numOfChain1 + m_numOfChain2; i++)
        {
            // set the head direction of the boid:  direction angle on xz plane

            float th_i = GetAngularPositionOfLED(a1, b1, m_startAngleOfChain1, ref m_LEDChainLength,
                                                 Random.Range(m_minLEDInterval, m_maxLEDInterval), i);
            float r_i = a1 * Mathf.Exp(b1 * th_i);

            Debug.Log(i + "th LED Ploar POS (th,r):" + new Vector2(th_i * 180 / M_PI, r_i).ToString("F4"));

            m_BoidLEDArray[i].HeadDir = new Vector3(Mathf.Cos(th_i), 0.0f, Mathf.Sin(th_i));

            Debug.Log(i + "th LED HeadDir:" + m_BoidLEDArray[i].HeadDir.ToString("F4"));
            Vector3 ledPos = r_i * m_BoidLEDArray[i].HeadDir;

            m_BoidLEDArray[i].Position = ledPos;


            float initScaleX = Random.Range(MinCylinderRadiusScale, MaxCylinderRadiusScale); // 0.5 ~ 1.0
                                                                                   //float initScaleY = Random.Range(MinCylinderRadius, MaxCylinderRadius);
                                                                                   //float initScaleZ = Random.Range(MinCylinderRadius, MaxCylinderRadius);

            m_BoidLEDArray[i].Scale = new Vector3(initScaleX, initScaleX, initScaleX);

            Debug.Log(i + "th LED POS:" + ledPos.ToString("F4"));
        } // for  (int i )

        //Debug.Log("Second Chain:");
        //for (int i = 0; i < m_numOfChain2; i++)
        //{
        //    // set the head direction of the boid:  direction angle on xz plane

        //    float th_i = GetAngularPositionOfLED(a1, b1, m_startAngleOfChain2, ledInterval,i);
        //    float r_i = a1 * Mathf.Exp(b1 * th_i);

        //    Debug.Log(i + "th LED Ploar POS (th,r):" + (new Vector2(th_i * 180 / M_PI, r_i) ).ToString("F4") );

        //    m_BoidLEDArray[m_numOfChain1 + i].HeadDir = new Vector3(Mathf.Cos(th_i), 0.0f, Mathf.Sin(th_i));


        //    Debug.Log(i + "th LED HeadDir:" + m_BoidLEDArray[m_numOfChain1 + i].HeadDir.ToString("F4"));

        //    Vector3 ledPos = r_i * m_BoidLEDArray[m_numOfChain1 + i].HeadDir;

        //    m_BoidLEDArray[m_numOfChain1 + i].Position = ledPos;

        //    float initScaleX = Random.Range(MinCylinderRadius, MaxCylinderRadius); // 0.5 ~ 1.0
        //                                                                           //float initScaleY = Random.Range(MinCylinderRadius, MaxCylinderRadius);
        //                                                                           //float initScaleZ = Random.Range(MinCylinderRadius, MaxCylinderRadius);

        //    m_BoidLEDArray[m_numOfChain1 + i].Scale = new Vector3(initScaleX, initScaleX, initScaleX);


        //    Debug.Log(i + "th LED POS:" + ledPos.ToString("F4") );

        //} // for  (int i )

        Debug.Log("Outer Chain:");
        m_LEDChainLength = 0;

        for (int i = 0; i < m_numOfChain3 + m_numOfChain4; i++)
        {
            // set the head direction of the boid:  direction angle on xz plane

            float th_i = GetAngularPositionOfLED(a2, b2, m_startAngleOfChain2, ref m_LEDChainLength,
                                                 Random.Range(m_minLEDInterval, m_maxLEDInterval), i);
            float r_i = a2 * Mathf.Exp(b2 * th_i);

            Debug.Log(i + "th LED Ploar POS (th,r):" + new Vector2(th_i * 180 / M_PI, r_i).ToString("F4"));

            m_BoidLEDArray[m_numOfChain1 + m_numOfChain2 + i].HeadDir = new Vector3(Mathf.Cos(th_i), 0.0f, Mathf.Sin(th_i));


            Debug.Log(i + "th LED HeadDir:" + m_BoidLEDArray[m_numOfChain1 + m_numOfChain2 + i].HeadDir.ToString("F4"));

            Vector3 ledPos = r_i * m_BoidLEDArray[m_numOfChain1 + +m_numOfChain2 + i].HeadDir;

            m_BoidLEDArray[m_numOfChain1 + m_numOfChain2 + i].Position = ledPos;

            float initScaleX = Random.Range(MinCylinderRadiusScale, MaxCylinderRadiusScale); // 0.5 ~ 1.0
                                                                                   //float initScaleY = Random.Range(MinCylinderRadius, MaxCylinderRadius);
                                                                                   //float initScaleZ = Random.Range(MinCylinderRadius, MaxCylinderRadius);

            m_BoidLEDArray[m_numOfChain1 + m_numOfChain2 + i].Scale = new Vector3(initScaleX, initScaleX, initScaleX);


            Debug.Log(i + "th LED POS:" + ledPos.ToString("F4"));

        } // for  (int i )

        //Debug.Log("Fourth Chain:");
        //for (int i = 0; i < m_numOfChain4; i++)
        //{
        //    // set the head direction of the boid:  direction angle on xz plane

        //    float th_i = GetAngularPositionOfLED(a2, b2, m_startAngleOfChain4, ledInterval,i);
        //    float r_i = a2 * Mathf.Exp(b2 * th_i);


        //    Debug.Log(i + "th LED Ploar POS (th,r):" + new Vector2( th_i * 180 / M_PI, r_i).ToString("F4"));

        //    m_BoidLEDArray[m_numOfChain1 + m_numOfChain2 + m_numOfChain3 +  i].HeadDir = new Vector3(Mathf.Cos(th_i), 0.0f, Mathf.Sin(th_i));

        //    Debug.Log(i + "th LED HeadDir:" + m_BoidLEDArray[m_numOfChain1 + m_numOfChain2 + m_numOfChain3 + i].HeadDir.ToString("F4"));

        //    Vector3 ledPos = r_i * m_BoidLEDArray[m_numOfChain1 + +m_numOfChain2 + m_numOfChain3 + i].HeadDir;

        //    m_BoidLEDArray[m_numOfChain1 + +m_numOfChain2 + m_numOfChain3 + i].Position = ledPos;

        //    float initScaleX = Random.Range(MinCylinderRadius, MaxCylinderRadius); // 0.5 ~ 1.0
        //                                                                           //float initScaleY = Random.Range(MinCylinderRadius, MaxCylinderRadius);
        //                                                                           //float initScaleZ = Random.Range(MinCylinderRadius, MaxCylinderRadius);

        //    m_BoidLEDArray[m_numOfChain1 + m_numOfChain2 + m_numOfChain3 + i].Scale = new Vector3(initScaleX, initScaleX, initScaleX);




        //    Debug.Log(i + "th LED POS:" + ledPos.ToString("F4"));
        //} // for  (int i )



    } // SetBoidLEDArray()


    // Get th_i for the ith LED along the sprial curve r = a * exp(b*th_i)
    float GetAngularPositionOfLED(float a, float b, float th0, ref float LEDChainLength, float ledInterval, int ledNo)
    {// // The ith LED  will be placed at location (r_i, th_i)
     //    such that  L(r(th), th0, th_i) = root(1 + b^2)/b * [ r(th_i)  - r(th0)] =ledInterval  * i, 

        float r_th_0 = a * Mathf.Exp(b * th0);

        float r_th_i = (LEDChainLength) / (Mathf.Sqrt(1 + b * b) / b) + r_th_0;
        float th_i = Mathf.Log((r_th_i / a)) / b;

        LEDChainLength += ledInterval;

        return th_i;

    }    //    r(th_i)  = a*exp(b*th_i), 

    private void Awake()
    {

        //******************
        m_totalNumOfLEDs = m_numOfChain1 + m_numOfChain2 + m_numOfChain3 + m_numOfChain4;

        m_startAngleOfChain1 = m_beginFromInChain1 * M_PI / 180; // degree
        m_startAngleOfChain2 = m_beginFromInChain2 * M_PI / 180; // degree


        m_BoidLEDBuffer = new ComputeBuffer(m_LEDColorGenController.m_totalNumOfLEDs, Marshal.SizeOf(typeof(BoidLEDData)));

        //m_BoidLEDArray = new BoidLEDData[m_boids.n_BoidsNum];

        //For each kernel we are setting the buffers that are used by the kernel, so it would read and write to those buffers

        // For the part of boidArray that is set by data are filled by null. 
        // When the array boidArray is created each element is set by null.

        // create a m_BoidLEDArray to link to m_BoidLEDBuffer:
        SetBoidLEDArray(m_BoidLEDArray); // THe Boid LEDs array is defined without their colors

        m_BoidLEDBuffer.SetData(m_BoidLEDArray); // buffer is R or RW


        //*************************

        if (m_boidLEDInstanceMaterial == null)
        {
            Debug.LogError("The global Variable m_boidLEDInstanceMaterial is not  defined in Inspector");
            // EditorApplication.Exit(0);
            Application.Quit();
            //return;

        }

        m_instanceMeshCircle = new CircleMesh(unitRadius);
        m_instanceMeshCylinder = new CylinderMesh(height, radius, nbSides, nbHeightSeg);

        m_boidInstanceMesh = m_instanceMeshCylinder.m_mesh;

       // m_boidInstanceMesh = m_instanceMeshCircle.m_mesh;

        m_boidLEDArgsBuffer = new ComputeBuffer(
          1,
          m_boidLEDArgs.Length * sizeof(uint),
          ComputeBufferType.IndirectArguments
         );

    } // Awake()
    void Start () 
	{

        //// get the reference to SimpleBoidsTreeOfVoice

        m_boids = this.gameObject.GetComponent<SimpleBoidsTreeOfVoice>();


        if (m_boids == null)
        {
            Debug.LogError("SimpleBoidsTreeOfVoice component should be attached to CommHub");
            //EditorApplication.Exit(0);

            Application.Quit();
            //return;

        }


        //// check if _boids.BoidBuffer is not null
        //if (m_boids.m_BoidBuffer is null)
        //{

        //    Debug.LogError("m_boids.m_BoidBuffer is null");
        //    //EditorApplication.Exit(0);
        //    Application.Quit();

        //    //return; 
        //}


        //BoidLED rendering 

        //m_LEDColorGenController = this.gameObject.GetComponent<LEDColorGenController>();


        //if (m_LEDColorGenController.m_BoidLEDBuffer == null)
        //{
        //    Debug.LogError("m_LEDColorGenController.m_BoidLEDBuffer should be set in Awake() of  LEDColorGenController");
        //    Application.Quit();
        //   // return; // nothing to render; 

        //}


        ///
        numIndices = m_boidInstanceMesh ? m_boidInstanceMesh.GetIndexCount(0) : 0;
        //GetIndexCount(submesh = 0)


        m_boidLEDArgs[0] = numIndices;  // the number of indices in the set of triangles

        // m_boidLEDArgs[1] = (uint) m_totalNumOfLEDs; // the number of instances

        m_boidLEDArgs[1] = (uint)m_LEDColorGenController.m_totalNumOfLEDs; // the number of instances
        m_boidLEDArgsBuffer.SetData( m_boidLEDArgs) ;


        m_boidLEDInstanceMaterial.SetVector("_Scale", new Vector3(m_scale, m_scale, m_scale));

        m_boidLEDInstanceMaterial.SetVector("GroundMaxCorner", m_boids.GroundMaxCorner);
        m_boidLEDInstanceMaterial.SetVector("GroundMinCorner", m_boids.GroundMinCorner);

        m_boidLEDInstanceMaterial.SetVector("CeilingMaxCorner", m_boids.CeilingMaxCorner);
        m_boidLEDInstanceMaterial.SetVector("CeilingMinCorner", m_boids.CeilingMinCorner);

        m_boidLEDInstanceMaterial.SetBuffer("_BoidLEDBuffer", m_LEDColorGenController.m_BoidLEDBuffer);


    } // Start()




    // Update is called once per frame
    public void Update () 
	{
		RenderInstancedMesh();              
       
    }

	private void OnDestroy()
	{
	

        if (m_boidLEDArgsBuffer == null) return;
        m_boidLEDArgsBuffer.Release();
        m_boidLEDArgsBuffer = null;

    }

	private void RenderInstancedMesh()
	{

        //"_BoidLEDBuffer" is computed  by LEDColorGenController

        // reading from the buffer written by regular shaders
        //https://gamedev.stackexchange.com/questions/128976/writing-and-reading-computebuffer-in-a-shader

        // _boids.BoidBuffer.GetData(boidArray);

        // m_boidLEDInstanceMaterial.SetBuffer("_BoidLEDBuffer", m_LEDColorGenController.m_BoidLEDBuffer);
        // m_LEDColorGenController.m_BoidLEDBuffer is ready in and AWake() and Update() of m_LEDColorGenController.

        ////BOIDLEDCyliner drawing


        Graphics.DrawMeshInstancedIndirect(
             m_boidInstanceMesh,

            0,
            m_boidLEDInstanceMaterial, // This material defines the shader which receives instanceID
            new Bounds(m_boids.RoomCenter, m_boids.RoomSize),
            m_boidLEDArgsBuffer // this contains the information about the instances
        );




    }//private void RenderInstancedMesh()

}//class BoidLEDRendererTreeOfVoice 
