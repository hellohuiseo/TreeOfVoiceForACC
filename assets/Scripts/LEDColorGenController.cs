using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Runtime.InteropServices;

using Random = UnityEngine.Random;

public class LEDColorGenController : MonoBehaviour
{

    //ComputeBuffer m_BoidLEDRenderDebugBuffer;
    //ComputeBuffer m_BoidLEDRenderDebugBuffer0;

    //// // ComputeBuffer(int count, int stride, ComputeBufferType type);

    //BoidLEDRenderDebugData[] m_ComputeBufferArray;
    //Vector4[] m_ComputeBufferArray0;

    public struct BoidLEDRenderDebugData
    {
        // public Vector3  WallOrigin; // the reference position of the wall (the boid reference frame) on which the boid is 

        //public Vector3 EulerAngles; // the rotation of the boid reference frame
        public int BoidLEDID; // the position of the  boid in the boid reference frame        

        public int  NearestBoidID; // the scale factors
        public int  NeighborCount; // heading direction of the boid on the local plane
        public float NeighborRadius; // the radius of the circle boid
        public Vector4 NearestBoidColor;         // RGBA color
        public Vector4 AvgColor;         // RGBA color

    }


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
        public int NeighborCount;
    }



    //public struct BoidData
    //{
    //    // public Vector3  WallOrigin; // the reference position of the wall (the boid reference frame) on which the boid is 

    //    //public Vector3 EulerAngles; // the rotation of the boid reference frame
    //    public Vector3 Position; // the position of the  boid in the boid reference frame        

    //    public Vector3 Scale; // the scale factors
    //    public Vector3 HeadDir; // heading direction of the boid on the local plane
    //    public float Speed;            // the speed of a boid

    //    public float Radius; // the radius of the circle boid
    //    public Vector3 ColorHSV;
    //    public Vector4 ColorRGB;         // RGBA color
    //    public Vector2 SoundGrain; // soundGrain = (freq, amp)
    //    public float Duration;     // duration of a boid each frame
    //    public int WallNo;      // the number of the wall on which the boid lie. 0=> the ground
    //                            // 1 => the ceiling, 2 => left wall, 3=> right wall. 4=> front wall
    //}

  
    public SimpleBoidsTreeOfVoice m_boids;
    // 보이드의 수
    public float m_BoidsNum;

    public int m_totalNumOfLEDs;

    public int m_samplingWall = 1; // ceiling

    public float m_samplingRadius = 1; //m
  
    public int m_numberOfWalls =2;

    //https://www.reddit.com/r/Unity3D/comments/7ppldz/physics_simulation_on_gpu_with_compute_shader_in/

    protected int m_kernelIDLED;

    [SerializeField] protected ComputeShader m_BoidLEDComputeShader; 
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

   
    public float m_startingRadiusOfInnerChain = 0.7f; // m
    public float m_endingRadiusOfInnerChainThreeTurns = 1f;

    public float m_startingRadiusOfOuterChain = 1.7f; // m
    public float m_endingRadiusOfOuterChainThreeTurns = 2f;


    //cf.  public float CeilingInnerRadius = 0.7f;

    public int m_firstPartOfInnerChain = 40;
    public int m_secondPartOfInnerChain = 40;
    public int m_firstPartOfOuterChain = 60;
    public int m_secondPartOfOuterChain = 60;

    public float m_beginFromInChain1 = -135f;
    public float m_beginFromInChain2 = -70f;

    public float m_innerCylinderHeightScale = 4.5f; // 4.5 m; Unit Hight = 1 m
    public float m_outerCylinderHeightScale = 2.5f; // 4.5 m; Unit Hight = 1 m

    public float m_LEDChainToCeilingScale = 5.0f;

    float m_startAngleOfChain1; // this is the angle where r0, that is, a0 is defined, that is, on the local x axis
    float m_startAngleOfChain2;


    // delegate signature (interface definition)

    public delegate void LEDSenderHandler(byte[] m_LEDArray);
    public event LEDSenderHandler m_LEDSenderHandler;

    protected const int BLOCK_SIZE = 256; // The number of threads in a single thread group

    //protected const int MAX_SIZE_OF_BUFFER = 1000;

    int m_threadGroupSize;

    const float epsilon = 1e-2f;
    const float M_PI = 3.1415926535897932384626433832795f;


    //m_neuroHeadSetController.onAverageSignalReceived += m_ledColorGenController.UpdateLEDResponseParameter;
    //m_irSensorMasterController.onAverageSignalReceived += m_ledColorGenController.UpdateColorBrightnessParameter;

    private void Awake()
    {// initialize me

        m_totalNumOfLEDs = m_firstPartOfInnerChain  + m_secondPartOfInnerChain
                          + m_firstPartOfOuterChain + m_secondPartOfOuterChain;

        m_startAngleOfChain1 = m_beginFromInChain1  * M_PI / 180; // degree
        m_startAngleOfChain2 = m_beginFromInChain2 *  M_PI / 180; // degree

        

        //m_threadGroupSize = Mathf.CeilToInt(m_BoidsNum / (float)BLOCK_SIZE);

        m_threadGroupSize = Mathf.CeilToInt(m_totalNumOfLEDs / (float)BLOCK_SIZE);



        m_LEDArray = new byte[m_totalNumOfLEDs * 3];

        //m_boidArray = new BoidData[ (int) m_BoidsNum ]; // for debugging


        if (m_BoidLEDComputeShader == null)
        {
            Debug.LogError("BoidLEDComputeShader  should be set in the inspector");
            Application.Quit();
            //return;
        }


        m_kernelIDLED = m_BoidLEDComputeShader.FindKernel("SampleLEDColors");


        m_BoidLEDComputeShader.SetInt("_SamplingWall", m_samplingWall);
        m_BoidLEDComputeShader.SetFloat("_SamplingRadius", m_samplingRadius);
        m_BoidLEDComputeShader.SetFloat("_LEDChainToCeilingScale", m_LEDChainToCeilingScale);
    



        //  m_BoidLEDRenderDebugBuffer = new ComputeBuffer(m_totalNumOfLEDs,
        //                                  4 * sizeof(float), ComputeBufferType.Default);

        // Type of the buffer, default is ComputeBufferType.Default (structured buffer)
        //m_BoidLEDRenderDebugBuffer = new ComputeBuffer(m_totalNumOfLEDs, Marshal.SizeOf(typeof(BoidLEDRenderDebugData)));

        //m_BoidLEDRenderDebugBuffer0 = new ComputeBuffer(m_totalNumOfLEDs, Marshal.SizeOf(typeof(Vector4)));

        //// Set the ComputeBuffer for shader debugging
        //// But a RWStructuredBuffer, requires SetRandomWriteTarget to work at all in a non-compute-shader. 
        ////This is all Unity API magic which in some ways is convenient 

        //Graphics.SetRandomWriteTarget(1, m_BoidLEDRenderDebugBuffer);

        // m_ComputeBufferArray = new BoidLEDRenderDebugData[m_totalNumOfLEDs];
        //m_ComputeBufferArray0 = new Vector4[m_totalNumOfLEDs];

        //m_BoidLEDRenderDebugBuffer.SetData(m_ComputeBufferArray);
        //m_BoidLEDRenderDebugBuffer0.SetData(m_ComputeBufferArray0);


        //define BoidLED Buffer

        m_BoidLEDBuffer = new ComputeBuffer(m_totalNumOfLEDs, Marshal.SizeOf(typeof(BoidLEDData)));

        m_BoidLEDArray = new BoidLEDData[m_totalNumOfLEDs];

        //For each kernel we are setting the buffers that are used by the kernel, so it would read and write to those buffers

        // For the part of boidArray that is set by data are filled by null. 
        // When the array boidArray is created each element is set by null.

        // create a m_BoidLEDArray to link to m_BoidLEDBuffer:
        SetBoidLEDArray(m_BoidLEDArray); // THe Boid LEDs array is defined without their colors

        m_BoidLEDBuffer.SetData(m_BoidLEDArray); // buffer is R or RW

        m_BoidLEDComputeShader.SetBuffer(m_kernelIDLED, "_BoidLEDBuffer", m_BoidLEDBuffer);



        Debug.Log("In Awake() in LEDColorGenController:");

        for (int i = 0; i < m_totalNumOfLEDs; i++)
        {
           
            Debug.Log(i + "th LED Position" + m_BoidLEDArray[i].Position);
            Debug.Log(i + "th LED HeadDir" + m_BoidLEDArray[i].HeadDir);
            Debug.Log(i + "th LED Color" + m_BoidLEDArray[i].Color);
            Debug.Log(i + "th LED Color: NeighborCount" + m_BoidLEDArray[i].NeighborCount);

        }


        // m_BoidLEDComputeShader.SetBuffer(m_kernelIDLED, "_BoidLEDRenderDebugBuffer", m_BoidLEDRenderDebugBuffer);

        //m_BoidLEDComputeShader.SetBuffer(m_kernelIDLED, "_BoidLEDRenderDebugBuffer0", m_BoidLEDRenderDebugBuffer0);
    } // Awake()
    void Start()
    {
       //initialize others
        m_boids = this.gameObject.GetComponent<SimpleBoidsTreeOfVoice>();

        //m_BoidBuffer = m_boids.m_BoidBuffer;

        if (m_boids== null)
        {
            Debug.LogError("impleBoidsTreeOfVoice component should be added to CommHub");
            // Application.Quit();
            return;
        }

        m_BoidsNum = (int)m_boids.m_BoidsNum;


        m_BoidLEDComputeShader.SetInt("_BoidsNum", (int)m_BoidsNum);
           
        m_BoidLEDComputeShader.SetBuffer(m_kernelIDLED,  "_BoidBuffer", m_boids.m_BoidBuffer);
               

    }// void Start()


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

        float r0 = m_startingRadiusOfInnerChain;  // a1  in r = a1 * exp(b1 * th) is set so that the radius r0 is 0.7 when th =0;
        float r1 = m_endingRadiusOfInnerChainThreeTurns; // r1 = a1 exp (b1* 3 * 2pi)

        float r2 = m_startingRadiusOfOuterChain; ; //  a2  in r = a2 * exp( b2 * th); b2 is set so that r is r2 when th =0;
        float r3 = m_endingRadiusOfOuterChainThreeTurns; // r3 = a2* exp(b2* 3 * 2pi)

        float a1 = r0;
        float b1 = Mathf.Log(r1 / a1) / (6 * M_PI);

        float a2 = r2;
        float b2 = Mathf.Log(r3 / a2) / (6 * M_PI);
                
       
  
        
        Debug.Log("Inner Chain:");

        m_LEDChainLength = 0;

        for (int i = 0; i < m_firstPartOfInnerChain + m_secondPartOfInnerChain; i++ )
        {
            // set the head direction of the boid:  direction angle on xz plane

            //  thi_i: the angle on the local coordinate system:

            float th_i = GetAngularPositionOfLED(a1, b1, 0.0f, ref m_LEDChainLength,
                                                 Random.Range(m_minLEDInterval, m_maxLEDInterval),  i);
            float r_i = a1 * Mathf.Exp(b1 * th_i );

            float th_i_g = th_i + m_beginFromInChain1 * M_PI/180;

            Debug.Log(i + "th LED Ploar POS (th,r) [global coord]:" + new Vector2(th_i_g * 180 / M_PI, r_i).ToString("F4"));

            Vector3 headDir = new Vector3(Mathf.Cos(th_i_g), 0.0f, Mathf.Sin(th_i_g));

            Debug.Log(i + "th LED HeadDir:" + headDir.ToString("F4"));

            Vector3 ledPos = r_i * headDir;                           

             m_BoidLEDArray[i].Position = ledPos;
            m_BoidLEDArray[i].HeadDir= headDir;

            float initScaleX = Random.Range(0.7f, 1.0f); // 0.5 ~ 1.0
            float initScaleY = Random.Range(0.9f * m_innerCylinderHeightScale , m_innerCylinderHeightScale);
            float initScaleZ = Random.Range(0.7f, 1.0f);
                       
            m_BoidLEDArray[i].Scale = new Vector3(initScaleX, initScaleY, initScaleZ); 

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

        for (int i = 0; i < m_firstPartOfOuterChain + m_secondPartOfOuterChain; i++)
        {
            // set the head direction of the boid:  direction angle on xz plane

            float th_i = GetAngularPositionOfLED(a2, b2, 0.0f, ref m_LEDChainLength,
                                                 Random.Range(m_minLEDInterval, m_maxLEDInterval),  i);
            float r_i = a2 * Mathf.Exp(b2 * th_i);

            float th_i_g = th_i + m_beginFromInChain2 *  M_PI/180;

            Debug.Log(i + "th LED Ploar POS (th,r):" + new Vector2(th_i_g * 180 / M_PI, r_i).ToString("F4"));

            Vector3 headDir  = new Vector3(Mathf.Cos(th_i_g), 0.0f, Mathf.Sin(th_i_g));


            Debug.Log(i + "th LED HeadDir:" + headDir.ToString("F4"));

            Vector3 ledPos = r_i * headDir ;

            m_BoidLEDArray[m_firstPartOfInnerChain + m_secondPartOfInnerChain + i].HeadDir = headDir;
            m_BoidLEDArray[m_firstPartOfInnerChain + m_secondPartOfInnerChain + i].Position = ledPos;


            float initScaleX = Random.Range(0.7f, 1.0f); // 0.5 ~ 1.0
            float initScaleY = Random.Range(0.9f * m_outerCylinderHeightScale, m_outerCylinderHeightScale);
            float initScaleZ = Random.Range(0.7f, 1.0f);

            m_BoidLEDArray[m_firstPartOfInnerChain + m_secondPartOfInnerChain + i].Scale 
                           = new Vector3(initScaleX, initScaleY, initScaleZ);
                 

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

    public void UpdateLEDResponseParameter(double[] electrodeData) // eight EEG amplitudes
    {
    }

    public  void UpdateColorBrightnessParameter(int[] approachVectors) // four approach vectors; for testing use only one
    {
    }


    void Update()
    {
        //  Debug.Log("I am updating the LED colors ih LEDColorGenController");

        //cf.    m_kernelIDLED = m_BoidComputeShader.FindKernel("SampleLEDColors");

        // Call a particular kernel "SampleLEDColors" in the m_BoidLEDComputeShader;

        // m_BoidBuffer is set by the dispatching  BoidComputeShader in SimpleBoidsTreeOfVoice;

        // Now set m_BoidLEDBuffer by dispatching BoidLEDCOmputeShader.

        m_BoidLEDComputeShader.Dispatch(m_kernelIDLED, m_threadGroupSize, 1, 1);

        //note:  m_BoidLEDComputeShader.SetBuffer(m_kernelIDLED, "_BoidLEDBuffer", m_BoidLEDBuffer);
        // note:   m_BoidLEDBuffer will be used  in:
        //  m_Boid m_boidLEDInstanceMaterial.SetBuffer("_BoidLEDBuffer", m_LEDColorGenController.m_BoidLEDBuffer);

        // Update is called once per frame
        //m_BoidLEDRenderDebugBuffer.GetData(m_ComputeBufferArray);
        //m_BoidLEDRenderDebugBuffer0.GetData(m_ComputeBufferArray0);

        m_BoidLEDBuffer.GetData(m_BoidLEDArray); // Get the boidLED data to send to the arduino

        // Debug.Log("BoidLEDRender Debug");



        //_BoidLEDRenderDebugBuffer0[pId][0] = (float)minIndex;
        //_BoidLEDRenderDebugBuffer0[pId][1] = minDist;
        //_BoidLEDRenderDebugBuffer0[pId][2] = neighborCnt;

        // for (int i = 0; i < m_totalNumOfLEDs; i++)


        //{
        // Debug.Log(i + "th boid LED ID =" + m_ComputeBufferArray[i].BoidLEDID);
        //Debug.Log(i + "th boid LED (min index) Nearest Neighbor ID=" + m_ComputeBufferArray[i].NearestBoidID);
        //Debug.Log(i + "th boid LED Neighbor Count=" + m_ComputeBufferArray[i].NeighborCount);
        //Debug.Log(i + "th boid LED Neighbor Radius=" + m_ComputeBufferArray[i].NeighborRadius);
        //Debug.Log(i + "th boid LED Nearest Neighbor Color=" + m_ComputeBufferArray[i].NearestBoidColor);
        //Debug.Log(i + "th boid LED Avg Color:" + m_ComputeBufferArray[i].AvgColor);

        //Debug.Log(i + "th boid LED min Index [ver0] =" + m_ComputeBufferArray0[i][0]);
        //Debug.Log(i + "th boid LED min Dist [ver0]=" + m_ComputeBufferArray0[i][1] );
        //Debug.Log(i + "th boid LED Neighbor Count [ver0]=" + m_ComputeBufferArray0[i][2]);

        //Debug.Log(i + "th boid LED Nearest Boid ID [m_BoidLEDBuffer] =" + m_BoidLEDArray[i].NearestBoidID);
        //Debug.Log(i + "th boid LED Nearest Boid Color [m_BoidLEDBuffer] =" + m_BoidLEDArray[i].Color);



        // }


        // Each thread group, e.g.  SV_GroupID = (0,0,0) will contain BLOCK_SIZE * 1 * 1 threads according to the
        // declaration "numthreads(BLOCK_SIZE, 1, 1)]" in the computeshader.

        //This call sets  m_BoidLEDBuffer, which is passed to the LED shader directly

        // LEDColorGenController.m_BoidLEDBuffer will be used to render the LED Branches by  _boidLEDInstanceMaterial.

        // Get the current values of the boidLEDs computed by BoidLEDComputeShader

        // debugging



        //m_BoidLEDBuffer.GetData(m_BoidLEDArray); // Get the boidLED data to send to the arduino

        ////public static float/iny Range(float min, float max);

        // Copy m_BoidLEDArray to m_LEDArray to send them to the master Arduino via serial communication.


        //Debug.Log("In Update() in LEDColorGenController:");

        for (int i = 0; i < m_totalNumOfLEDs; i++)
        {
            m_LEDArray[i * 3] =    (byte)(255 * m_BoidLEDArray[i].Color[0]); // Vector4 Color
            m_LEDArray[i * 3 + 1] = (byte)(255 * m_BoidLEDArray[i].Color[1]);
            m_LEDArray[i * 3 + 2] = (byte)(255 * m_BoidLEDArray[i].Color[2]);

       

                //Debug.Log(i + "th LED Position" + m_BoidLEDArray[i].Position);
                //Debug.Log(i + "th LED HeadDir" + m_BoidLEDArray[i].HeadDir);
                //Debug.Log(i + "th LED Color" + m_BoidLEDArray[i].Color);
                //Debug.Log(i + "th LED Color: NeighborCount" + m_BoidLEDArray[i].NeighborCount);

        



            //  Debug.Log(i + "th LED Position" + m_BoidLEDArray[i].Position);
            // Debug.Log(i + "th LED Color" + m_BoidLEDArray[i].Color);

            //Debug.Log(i + "th LED Color (value range check) from m_boids.m_boidArray" 
            //    + m_boids.m_boidArray[  m_BoidLEDArray[i].NearestBoidID ].Color );

            // for debugging, copy m_boidArray colors and positions and scales to m_BoidLEDArray
            //m_BoidLEDArray[i].Position = m_boids.m_boidArray[i].Position;
            //m_BoidLEDArray[i].Scale = m_boids.m_boidArray[i].Scale;
            //m_BoidLEDArray[i].Color = m_boids.m_boidArray[i].Color;

            //m_LEDArray[i * 3] = (byte)(255 * m_BoidLEDArray[i].Color[0]); // Vector4 Color
            //m_LEDArray[i * 3 + 1] = (byte)(255 * m_BoidLEDArray[i].Color[1]);
            //m_LEDArray[i * 3 + 2] = (byte)(255 * m_BoidLEDArray[i].Color[2]);
        }


        //m_BoidLEDBuffer.SetData( m_BoidLEDArray ); // LEDColorGenController.m_BoidLEDBuffer  is used

        // to rendeirng the boid LED cylinders in BoidRendererTreeOfVoice.

        //for (int i = 0; i < m_totalNumOfLEDs; i++)
        //{
        //    m_LEDArray[i * 3] = (byte)(255 * m_BoidLEDArray[i].Color[0]); // Vector4 Color
        //    m_LEDArray[i * 3 + 1] = (byte)(255 * m_BoidLEDArray[i].Color[1]);
        //    m_LEDArray[i * 3 + 2] = (byte)(255 * m_BoidLEDArray[i].Color[2]);
        //}


        Debug.Log("LED Data Send Event Handler called in LEDColorGenController");

        m_LEDSenderHandler.Invoke( m_LEDArray) ;

 
     } // Update()



} //  LEDColorGenController class
