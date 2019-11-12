using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System;
using System.Reflection;


using Random = UnityEngine.Random;

// This is it:
//https://stackoverflow.com/questions/9088227/using-getopenfilename-instead-of-openfiledialog
//https://docs.microsoft.com/en-us/previous-versions/dotnet/netframework-4.0/w5tyztk9(v=vs.100)?redirectedfrom=MSDN


// Color: struct in UnityEngine
// WOW: a Component is actually an instance of a class so the first step is to get a reference to the
// Component instance you want to work with. This is done by GetComponent<>(): e,g Rigidbody rb = GetComponent<RigidBody>();
// To get a reference to an instance of other Components attached to the same gameObject. The same gameobject refers to
// the gameobject to which the current script class which mention the other components.



// Canvas and Game View, Scene View:
//With an "overlay canvas" your UI will be stretched over the entire scene;
//    however, it will be scaled to the size of 1 UI pixel == 1 Unity Unit,
//    so it will appear huge in your scene. This will look correct in the game view.

//The Canvas is a Game Object with a Canvas component on it, 
//    and all UI elements must be children of such a Canvas.

//The Canvas area is shown as a rectangle in the Scene View.This makes it easy to position UI elements 
//    without needing to have the Game View visible at all times.

//Canvas on multiple gameviews:
//https://forum.unity.com/threads/multi-display-canvases-not-working-in-5-4-2.439429/#post-2844936
// On the other hand, I try to set WordSpace for using event camera for Display2. 
//but its only works on display 1 attached camera.

//https://forum.unity.com/threads/multiple-camera-with-multiple-canvas-ui-button-event-problem.747788/

//Does it work when you do a build?
//The editor does not have the same multiple display support as player builds.Unfortuantly the UI is unable to detect the display index when in the Editor and so input will go across all displays, this however should not happen in a player build.
//You could try the hack I provided here: https://forum.unity.com/threads/multi-display-canvases-not-working-in-5-4-2.439429/#post-4981778


/// <summary>
/// Canvas + Physics RayCaster
/// </summary>
//https://forum.unity.com/threads/using-physics-raycast-to-interact-with-canvas-in-worldspace-vr-and-other-environments.563848/
//All Raycaster components have to be attached to the Canvas that you want to receive the event through. With Physics raycasters, rays are parsed through the camera for the canvas to resolve 3D object within the view of the canvas.
// NO. Physics raycasters go on cameras. Graphics raycasters go on canvases.
public class SimpleBoidsTreeOfVoice : MonoBehaviour
{

    ActionPlanController m_actionPlanController; 

    const int BLOCK_SIZE = 1024; // The number of threads in a single thread group

    const int MAX_SIZE_OF_BUFFER = 10000;

    const float epsilon = 1e-2f;
    const float M_PI = 3.1415926535897932384626433832795f;

    float m_SceneStartTime; // = Time.time;
    float m_currTime;

    public float m_AnimationCycle = 390.0f; // 390 sec <=> Screen.width; 390sec/(Screen.Width *2)??
    
    public bool UseActionPlan = true;
    public bool Use3DMotion = false; // 0 => use 2D Motions

    //public float m_MeshNO = 1.0f; // The number for the mesh to be used
    //public float m_Scale = 1.0f; // the scale for the mesh size

    //public MeshSetting _meshSetting = new MeshSetting(1.0f, 1.0f);

    // public MeshSetting _meshSetting;

    // meshNo ==1 => circle mesh

    //DetermineParamValue("_MeshNo", currTime, ref _meshSetting.MeshNo);
    //DetermineParamValue("_Scale", currTime, ref _meshSetting.Scale);


    // 보이드의 수
    [Range(1000, 10000)]
    public float m_BoidsNum = 5000f;


    
    [Range(0.0f, 1.0f)]
    [SerializeField] protected float MinBoidRadius = 0.1f;
    [Range(0.0f, 1.0f)]
    [SerializeField] protected float MaxBoidRadius = 0.3f;


    [Range(0.0f, 3.0f)]
    [SerializeField] private float _minSpeed = 1.0f;
    [Range(0.0f, 5.0f)]
    [SerializeField] private float _maxSpeed = 3.0f;



    [Range(0.0f, 3.0f)]
    [SerializeField] private float _speedFactor = 1.0f;

    [Range(0.0f, 3.0f)]
    [SerializeField] private float _scaleFactor = 1.0f;



    // 질량
    //[SerializeField] private float _mass = 1f;

    // the neighborhood conditions and weights for the three flocking actions
    // 분리   

    [SerializeField] private BoidSetting _separate = new BoidSetting(0.6f, 0.5f);
    // 정렬
    [SerializeField] private BoidSetting _alignment = new BoidSetting(2f, 0.3f);
    // 응집
    [SerializeField] private BoidSetting _cohesion = new BoidSetting(2f, 0.2f);




    //[SerializeField] private float _groundFlockingWeight = 0.3f;
    //// 목표지점 추구
    //[SerializeField] private float _groundDivergeWeight = 0.2f;
    //// 중심회전
    //[SerializeField] private float _groundCirculationWeight = 0.5f;


    [SerializeField] private GroundWeight _groundWeight = new GroundWeight(0.3f, 0.2f, 0.5f);



    [SerializeField] private CeilingWeight _ceilingWeight = new CeilingWeight(0.2f, 0.3f, 0.5f);




    [Range(-1.0f, 1.0f)]
    [SerializeField] private float _groundMinHue = 0.0f;

    [Range(-1.0f, 1.0f)]
    [SerializeField] private float _groundMaxHue = 1.0f;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float _groundMinSaturation = 0.2f;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float _groundMaxSaturation = 1.0f;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float _groundMinValue = 0.2f;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float _groundMaxValue = 1.0f;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float _groundMinAlpha = 0.2f;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float _groundMaxAlpha = 1.0f;



    [Range(-1.0f, 1.0f)]
    [SerializeField] private float _ceilingMinHue = 0.0f;

    [Range(-1.0f, 1.0f)]
    [SerializeField] private float _ceilingMaxHue = 1.0f;


    [Range(0.0f, 1.0f)]
    [SerializeField] private float _ceilingMinSaturation = 0.2f;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float _ceilingMaxSaturation = 1.0f;


    [Range(0.0f, 1.0f)]
    [SerializeField] private float _ceilingMinValue = 0.2f;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float _ceilingMaxValue = 1.0f;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float _ceilingMinAlpha = 0.2f;

    [Range(0.0f, 1.0f)]
    [SerializeField] private float _ceilingMaxAlpha = 1.0f;





    // 벽의 크기
    public Vector3 GroundMinCorner = new Vector3(-10f, 0f, -10f);
    public Vector3 GroundMaxCorner = new Vector3(10f, 0f, 10f);

    public Vector3 CeilingMinCorner = new Vector3(-10f, 12f, -10f);
    public Vector3 CeilingMaxCorner = new Vector3(10f, 12f, 10f);

    // Depth of the Ground = +- alpha of the ground plane

    public float GroundPlaneDepth = 3f;
    public float CeilingPlaneDepth = 3f;


    public Vector3 RightWallMinCorner = new Vector3(10f, 0f, -10f);
    public Vector3 RightWallMaxCorner = new Vector3(10f, 12f, 10f);

    public float GroundRadius = 12f;
    public float CeilingRadius = 12f;
    // public float CeilingInnerRadius = 0.7f;

    public float CeilingInnerRadius = 0.3f;

    public int numOfWalls = 2; // ground, ceiling, front wall

    public int numOfWallGizmos = 3; // ground, ceiling, front wall



    // 컴퓨트 쉐이더
    // Mention another Component instance.
    [SerializeField] protected ComputeShader m_BoidComputeShader; // set in the inspector

    //https://www.reddit.com/r/Unity3D/comments/7ppldz/physics_simulation_on_gpu_with_compute_shader_in/
    // 보이드의 버퍼
    public ComputeBuffer m_BoidBuffer { get; protected set; } // null reference

    public ComputeBuffer m_BoidCountBuffer { get; protected set; } // null reference

    int m_BufferStartIndex, m_BufferEndIndex;

    protected int m_KernelIdGround, m_KernelIdCeiling, m_KernelIdCountBoids;


    public BoidData[] m_boidArray;

    int[] boidCountArray;

    int totalNumOfSimulations = 0;
    bool m_IsGizmoDrawn = false;
    
    //When you create a struct object using the new operator, it gets created and the appropriate constructor is called.
    //Unlike classes, structs can be instantiated without using the new operator. 
    //If you do not use new, the fields will remain unassigned and the object cannot be used until all of the fields are initialized.
       

    // 시뮬레이션 공간의 센터 
    public Vector3 RoomCenter
    {
        get { return (GroundMinCorner + CeilingMaxCorner) / 2f; }
    }

    // 시뮬레이션 공간의 크기
    public Vector3 RoomSize
    {
        get { return CeilingMaxCorner - GroundMinCorner; }
    }

    // Transforms that represent the 5 walls

    //Transform groundTransform = new Transform();
    //The Above Causes "Inaccessible  Errors":  you shouldn't be calling the constructor for Transform or any other Component. 
    //Transform is a component. You can't create components like that, 
    //since they don't exist by themselves; you can create a GameObject, and add components to it.

  
    public GameObject[] gameObjForWallTransforms;

    // In the above, Your code creates only the array of type GameOjbect, but neither of its items.
    // Basically, you need to store instances of Sample into this array.
    //
    // To put it simple, without any fancy LINQ etc.:
    // Sample[] samples = new Sample[100];
    // for (int i = 0; i<samples.Length; i++) samples[i] = new Sample();

    private bool wallTransformsDefined = false;

    private bool IsBoidsNumSet = false;

    //public class Action
    //{

    //    public List<float> T;
    //    public float V;


    //}

    List<string> m_actionKeys;
    int m_threadGroupSize;

    //public void Insert(int index, T item);
    //public int LastIndexOf(T item);
    //public int LastIndexOf(T item, int index);
    //public int LastIndexOf(T item, int index, int count);
    //public bool Remove(T item);
    // public void RemoveAt(int index);

    public Dictionary<String, List<ActionPlanController.Action>> m_actionPlan;

    public struct BoidData
    {
        // public Vector3  WallOrigin; // the reference position of the wall (the boid reference frame) on which the boid is 

        //public Vector3 EulerAngles; // the rotation of the boid reference frame
        public Vector3 Position; // the position of the  boid in the boid reference frame        

        public Vector3 Scale; // the scale factors
        public Vector3 HeadDir; // heading direction of the boid on the local plane
        public float Speed;            // the speed of a boid

        public float Radius; // the radius of the circle boid
        public Vector3 ColorHSV;
        public Vector4 Color;         // RGBA color
        public Vector2 SoundGrain; // soundGrain = (freq, amp)
        public float Duration;     // duration of a boid each frame
        public int WallNo;      // the number of the wall on which the boid lie. 0=> the ground
                                // 1 => the ceiling, 2 => left wall, 3=> right wall. 4=> front wall
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



    private void OnDrawGizmos()
    {
        // If these are public variables, changing them in the script won't affect the game. The value assigned in the script is the default. 
        //Once the script has been attached to an object, you need to tune them via the inspector.

        //if (m_IsGizmoDrawn)
        //{
        //    return;


        //}
        //else
        //{
        //    m_IsGizmoDrawn = true;
        //}


        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(RoomCenter, RoomSize);

        Vector3 wallOrigin, initEulerAngles;
        //const int maxNumOfWalls = 5;

        if (!wallTransformsDefined)  // if true, skip the creation of walls
        {
            wallTransformsDefined = true;


            gameObjForWallTransforms = new GameObject[numOfWallGizmos];

            GameObject gameObj = GameObject.Find("GroundWall");
            if (gameObj == null)
            {

                gameObj = new GameObject("GroundWall");
            }

            wallOrigin = new Vector3(0.0f, 0.0f, 0.0f);
            //   initEulerAngles = new Vector3(0.0f, 0.0f, 0.0f); // the rotation frame of the boid = the unity global frame 
            gameObj.transform.position = wallOrigin;
            ////
            // Summary:
            //     Returns the euler angle representation of the rotation.
            //  public Vector3 eulerAngles { get; set; }
            //    gameObjForWallTransforms[0].transform.rotation.eulerAngles= initEulerAngles;
            // OR transform.rotation = Quaternion.AngleAxis(xRot, transform.right);

            // OR https://forum.unity.com/threads/rotation-order.13469/
            // For example, in whichever order you like....
            //         rotation = Quaternion.AngleAxis(xAngle, Vector3.right) * Quaternion.AngleAxis(zAngle, Vector3.forward) *
            //                                   Quaternion.AngleAxis(yAngle, Vector3.up);

            //gameObjForWallTransforms[0].transform.eulerAngles = Mathf.Rad2Deg * initEulerAngles;

            //gameObjForWallTransforms[0].transform.rotation = Quaternion.identity;
            //Mathf.Rad2Deg * initEulerAngles;
            gameObj.transform.rotation = Quaternion.AngleAxis(0.5f * M_PI * Mathf.Rad2Deg, Vector3.right); // Z point upward
            gameObj.transform.localScale = Vector3.one;

            gameObjForWallTransforms[0] = gameObj;



            // wall 1
            gameObj = GameObject.Find("CeilingWall");
            if (gameObj == null)
            {
                gameObj = new GameObject("CeilingWall");
            }


            wallOrigin = new Vector3(0.0f, CeilingMaxCorner.y, 0.0f);

            //https://gamedev.stackexchange.com/questions/140579/euler-right-handed-to-quaternion-left-handed-conversion-in-unity/140581
            //initEulerAngles = new Vector3(M_PI, 0.0f, 0.0f); //  pitch  = 180: the x to the right, the z to the back, the y down

            gameObj.transform.position = wallOrigin;
            // gameObjForWallTransforms[1].transform.eulerAngles = Mathf.Rad2Deg * initEulerAngles;
            gameObj.transform.rotation = Quaternion.AngleAxis(-0.5f * M_PI * Mathf.Rad2Deg, Vector3.right); // Z point upward

            gameObj.transform.localScale = Vector3.one;

            gameObjForWallTransforms[1] = gameObj;


            // wall 2

            gameObj = GameObject.Find("FrontWall");
            if (gameObj == null)
            {
                gameObj = new GameObject("FrontWall");
            }

            wallOrigin = new Vector3(0.0f,
                                          CeilingMaxCorner.y / 2.0f, 0.0f); // front wall center

            // Euler Angles: R = Ry * Rx * Rz 
            //https://forum.unity.com/threads/roll-pitch-and-yaw-from-quaternion.63498/
            //  Unity uses the order Z-X-Y, which is fairly sensible given Unity's conventions. => Z = Roll, X = pitch, Y=Yaw
            //initEulerAngles = new Vector3(0.0f,-M_PI / 2f, -M_PI / 2f); // Rool=-90 => Pitch =0 => Yaw =-90

            gameObj.transform.position = wallOrigin;
            //gameObjForWallTransforms[2].transform.eulerAngles =-M_PI / 2f * initEulerAngles;
            //gameObjForWallTransforms[2].transform.rotation = Quaternion.AngleAxis(-M_PI / 2f * Mathf.Rad2Deg, Vector3.forward)
            //                                               * Quaternion.AngleAxis(-M_PI / 2f * Mathf.Rad2Deg, Vector3.up);

            gameObj.transform.rotation = Quaternion.identity;
            gameObj.transform.localScale = Vector3.one;
            gameObjForWallTransforms[2] = gameObj;


            // For debugging           

            for (int i = 0; i < numOfWallGizmos; i++)
            {
                Debug.Log("Wall Orientation = "); Debug.Log(i);

                Debug.Log("Euelr Angles="); Debug.Log(gameObjForWallTransforms[i].transform.eulerAngles);
                Debug.Log("Rotation ="); Debug.Log(gameObjForWallTransforms[i].transform.rotation);

                Debug.Log("right  ="); Debug.Log(gameObjForWallTransforms[i].transform.right);
                Debug.Log("forward ="); Debug.Log(gameObjForWallTransforms[i].transform.forward);
                Debug.Log("up  ="); Debug.Log(gameObjForWallTransforms[i].transform.up);
            }


        } // ! wallTransformsDefined


        // Walls are already created.
        //Draw the coordinate systems of each wall at the wallOrigin each frame.

        for (int i = 0; i < numOfWallGizmos; i++)
        {

            // Debug.Log("Wall Gizmos  are drawn: " + i); ;

            Gizmos.color = Color.red;

            Gizmos.DrawRay(gameObjForWallTransforms[i].transform.position, gameObjForWallTransforms[i].transform.right);

            Gizmos.color = Color.green;

            Gizmos.DrawRay(gameObjForWallTransforms[i].transform.position, gameObjForWallTransforms[i].transform.up);

            Gizmos.color = Color.blue;

            Gizmos.DrawRay(gameObjForWallTransforms[i].transform.position, gameObjForWallTransforms[i].transform.forward);

        }

    } //  private void OnDrawGizmos()

  

    private void OnDestroy()
    {
        if (m_BoidBuffer == null) return;
        m_BoidBuffer.Release();
        m_BoidBuffer = null;
    }



    //public static Color HSVToRGB(float H, float S, float V);
    protected void InitializeValues()
    {
     
        IsBoidsNumSet = true;         

        m_BufferEndIndex = (int)m_BoidsNum;


        m_BoidComputeShader.SetInt("_BoidsNum", (int)m_BoidsNum);
        m_BoidComputeShader.SetInt("_NumOfWalls", numOfWalls);


        //BoidComputeShader.SetFloat("_Mass", _mass);
                     
        m_BoidComputeShader.SetFloat("_SeparateRadius", _separate.Radius);
        m_BoidComputeShader.SetFloat("_SeparateWeight", _separate.Weight);
        m_BoidComputeShader.SetFloat("_AlignmentRadius", _alignment.Radius);
        m_BoidComputeShader.SetFloat("_AlignmentWeight", _alignment.Weight);
        m_BoidComputeShader.SetFloat("_CohesionRadius", _cohesion.Radius);
        m_BoidComputeShader.SetFloat("_CohesionWeight", _cohesion.Weight);


        m_BoidComputeShader.SetFloat("_MinSpeed", _minSpeed);
        m_BoidComputeShader.SetFloat("_MaxSpeed", _maxSpeed);

        m_BoidComputeShader.SetFloat("_SpeedFactor", _speedFactor);
        m_BoidComputeShader.SetFloat("_ScaleFactor", _scaleFactor);

        m_BoidComputeShader.SetFloat("_GroundFlockingWeight", _groundWeight.FlockingWeight);
        m_BoidComputeShader.SetFloat("_GroundDivergeWeight", _groundWeight.DivergeWeight);
        m_BoidComputeShader.SetFloat("_GroundCirculationWeight", _groundWeight.CirculationWeight);

        m_BoidComputeShader.SetFloat("_CeilingFlockingWeight", _ceilingWeight.FlockingWeight);
        m_BoidComputeShader.SetFloat("_CeilingConvergeWeight", _ceilingWeight.ConvergeWeight);
        m_BoidComputeShader.SetFloat("_CeilingCirculationWeight", _ceilingWeight.CirculationWeight);



        m_BoidComputeShader.SetFloat("_GroundMinHue", _groundMinHue);
        m_BoidComputeShader.SetFloat("_GroundMaxHue", _groundMaxHue);
        m_BoidComputeShader.SetFloat("_GroundMinSaturation", _groundMinSaturation);
        m_BoidComputeShader.SetFloat("_GroundMaxSaturation", _groundMaxSaturation);
        m_BoidComputeShader.SetFloat("_GroundMinValue", _groundMinValue);
        m_BoidComputeShader.SetFloat("_GroundMaxValue", _groundMaxValue);

        m_BoidComputeShader.SetFloat("_GroundMinAlpha", _groundMinAlpha);
        m_BoidComputeShader.SetFloat("_GroundMaxAlpha", _groundMaxAlpha);



        m_BoidComputeShader.SetFloat("_CeilingMinHue", _ceilingMinHue);
        m_BoidComputeShader.SetFloat("_CeilingMaxHue", _ceilingMaxHue);
        m_BoidComputeShader.SetFloat("_CeilingMinSaturation", _ceilingMinSaturation);
        m_BoidComputeShader.SetFloat("_CeilingMaxSaturation", _ceilingMaxSaturation);
        m_BoidComputeShader.SetFloat("_CeilingMinValue", _ceilingMinValue);
        m_BoidComputeShader.SetFloat("_CeilingMaxValue", _ceilingMaxValue);

        m_BoidComputeShader.SetFloat("_CeilingMinAlpha", _ceilingMinAlpha);
        m_BoidComputeShader.SetFloat("_CeilingMaxAlpha", _ceilingMaxAlpha);


        m_BoidComputeShader.SetVector("_GroundMaxCorner", GroundMaxCorner);
        m_BoidComputeShader.SetVector("_GroundMinCorner", GroundMinCorner);


        m_BoidComputeShader.SetVector("_CeilingMaxCorner", CeilingMaxCorner);
        m_BoidComputeShader.SetVector("_CeilingMinCorner", CeilingMinCorner);



        m_BoidComputeShader.SetFloat("_GroundPlaneDepth", GroundPlaneDepth);
        m_BoidComputeShader.SetFloat("_CeilingPlaneDepth", CeilingPlaneDepth);


        m_BoidComputeShader.SetFloat("_GroundRadius", GroundRadius);
        m_BoidComputeShader.SetFloat("_CeilinRadius", CeilingRadius);

        m_BoidComputeShader.SetFloat("_CeilingInnerRadius", CeilingInnerRadius);

        m_KernelIdGround = m_BoidComputeShader.FindKernel("SimulateCSGround");
        m_KernelIdCeiling = m_BoidComputeShader.FindKernel("SimulateCSCeiling");

        m_KernelIdCountBoids = m_BoidComputeShader.FindKernel("SimulateCSCountBoids");
    } //nitializeValues()


    float findAngleForVector(Vector3 vec)
    {
        float theta = Mathf.Atan2(vec.z, vec.x); // theta ranges (0,pi) or (0 -pi)

        if (theta < 0)
        { // negative theta means that vec (x,y) is in 3rd or 4th quadrant, measuring in the clockwise direction
            return (2 * M_PI + theta); // angle measured in the counterclockwise direction
        }
        else
        {
            return theta;
        }
    }


    float randomSign()
    {
        //  When defining Random.Range(1.0f, 3.0f) we will get results from 1.0 to 3.0;
        //When defining Random.Range(1,3) we will get results from 1 to 2

        return Random.Range(0, 2) == 0 ? -1f : 1f;
    }


    protected void InitializeBuffers()
    {
        // 버퍼의 초기화
        //https://stackoverflow.com/questions/21596373/compute-shaders-input-3d-array-of-floats
        //https://forum.unity.com/threads/possible-for-a-compute-buffer-to-pass-a-struct-containing-a-vector3-array.370329/
        //https://forum.unity.com/threads/size-of-computebuffer-for-mesh-vertices.446972/
        //Also by checking the decompiled file of Vector3 on github, I confirmed that Vector3 indeed only consists of 3 floats


        m_BoidCountBuffer = new ComputeBuffer(numOfWalls, Marshal.SizeOf(typeof(int)));

        m_BoidBuffer = new ComputeBuffer(MAX_SIZE_OF_BUFFER, Marshal.SizeOf(typeof(BoidData)));

        m_boidArray = new BoidData[MAX_SIZE_OF_BUFFER];
        boidCountArray = new int[numOfWalls]; // array of nulls




        for (int i = 0; i < numOfWalls; i++)
        {
            boidCountArray[i] = 0;
        }

        //var boidArray = new BoidData[BoidsNum];
        SetBoidArray(m_boidArray,(int)m_BoidsNum);


        // for debugging

        //  Debug.Log("Boids Initialization");

        //   for (int i=0; i < BoidsNum; i++)
        //   {

        //    Debug.Log("boidNo = "); Debug.Log(i);
        //    Debug.Log("position = = "); 
        //    Debug.Log(boidArray[i].Position);

        //    Debug.Log("color = = ");
        //   Debug.Log(boidArray[i].Color);

        // }

        //
        //For each kernel we are setting the buffers that are used by the kernel, so it would read and write to those buffers

        // For the part of boidArray that is set by data are filled by null. 
        // When the array boidArray is created each element is set by null.

        m_BoidBuffer.SetData(m_boidArray); // buffer is R or RW
        m_BoidCountBuffer.SetData(boidCountArray); // buffer is R or RW

        m_BoidComputeShader.SetBuffer(m_KernelIdGround, "_BoidBuffer", m_BoidBuffer);
     
        m_BoidComputeShader.SetBuffer(m_KernelIdCeiling, "_BoidBuffer", m_BoidBuffer);
     


        m_BoidComputeShader.SetBuffer(m_KernelIdCountBoids, "_BoidCountBuffer", m_BoidCountBuffer);
        m_BoidComputeShader.SetBuffer(m_KernelIdCountBoids, "_BoidBuffer", m_BoidBuffer);

    } // InitializeBuffers()




    protected void SetBoidArray(BoidData[] m_boidArray, int numberOfElements)
    {


        Vector3 initPos, initScale;
        float theta, phi, initSpeed, initRadiusX, initRadiusY, initRadiusZ;
        Vector4 initColor;
        Vector2 initSoundGrain;
        int wallNo;

        for (int i=0; i < numberOfElements; i++)
        {
            // set the head direction of the boid

            if (!Use3DMotion)
            // use 2D boids
            {

                // use 2D direction vector

                //  direction angle on xz plane
                theta = Random.Range(0, M_PI);


                m_boidArray[i].HeadDir = new Vector3(Mathf.Cos(theta), 0.0f, Mathf.Sin(theta));

                //use spherical coordinates to represent a direction of the boid

                //phi = Random.Range(0, 2 * M_PI);
                // phi = 90


                // boidArray[i].HeadDir = new Vector3(Mathf.Cos(theta), 0.0f, Mathf.Sin(theta));
                // Sin(phi) = Sin(90) = 1: theta on xz plane, phi between y axis and the radius vector
            }
            else
            {

                //  direction angle on xz plane ; inclination
                theta = Random.Range(0, M_PI);
                phi = Random.Range(0, 2 * M_PI); // azimuth
                m_boidArray[i].HeadDir = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
            } // 3D





            initRadiusX = Random.Range(MinBoidRadius, MaxBoidRadius); // 0.1 ~ 0.3
            initRadiusY = Random.Range(MinBoidRadius, MaxBoidRadius);
            initRadiusZ = Random.Range(MinBoidRadius, MaxBoidRadius);

            initScale = new Vector3(initRadiusX, initRadiusY, initRadiusZ);

            initSpeed = Random.Range(_minSpeed, _maxSpeed);

            m_boidArray[i].Radius = initRadiusX;

            m_boidArray[i].Scale = initScale;


            m_boidArray[i].Speed = initSpeed;



            wallNo = i % numOfWalls; // from 0 to 1
                                     // the number of the wall on which the boid lie. 0=> the ground
                                     // 1 => the ceiling, 2 => left wall, 3=> right wall. 4=> front   wall


            m_boidArray[i].WallNo = wallNo;

            // set the position of the boid either on the ceiling or the ground
            if (wallNo == 0)
            {  // the boid is on the ground. The y axis point upward; THe z axis points to the front, The x axis points to the right

                // wallOrigin = new Vector3(0.0f, 0.0f, 0.0f);
                //  initEulerAngles = new Vector3(0.0f, 0.0f, 0.0f); // the rotation frame of the boid = the unity global frame 
                // boidArray[i].EulerAngles = initEulerAngles;
                // boidArray[i].WallOrigin = wallOrigin;

                // The local position of the boid  on the wall reference frame of the boid

                if (!Use3DMotion)
                {


                    initPos = new Vector3(Random.Range(GroundMinCorner.x, GroundMaxCorner.x),
                                           0.0f, Random.Range(GroundMinCorner.z, GroundMaxCorner.z));
                }
                else
                {

                    initPos = new Vector3(Random.Range(GroundMinCorner.x, GroundMaxCorner.x),
                                          Random.Range(0f, GroundPlaneDepth), Random.Range(GroundMinCorner.z, GroundMaxCorner.z));
                }

                m_boidArray[i].Position = initPos; // the initial random position of the boid

                // set the transform for the wall


            }
            if (wallNo == 1)
            {  // the boid is on the ceiling  => The y axis of the boid looks down
               // The z axis points to the back , the axis x points to the right

                // wallOrigin = new Vector3(0.0f, CeilingMaxCorner.y, 0.0f


                // Unity's documentation states that the rotation order is ZXY, 
                // R = Ry * Rx * Rz, that is, Roll => Pitch => Yaw order; This order is also assumed in shader code

                // initEulerAngles = new Vector3( M_PI, 0.0f, 0.0f); //  pitch  = 180: the x to the right, the z to the back, the y down


                // The local position of the boid  on the wall reference frame

                if (!Use3DMotion)
                {
                    initPos = new Vector3(Random.Range(CeilingMinCorner.x, CeilingMaxCorner.x),
                                          0.0f,
                                          Random.Range(CeilingMinCorner.z, CeilingMaxCorner.z));
                }
                else
                {
                    initPos = new Vector3(Random.Range(CeilingMinCorner.x, CeilingMaxCorner.x),
                                           Random.Range(0f, CeilingPlaneDepth),
                                           Random.Range(CeilingMinCorner.z, CeilingMaxCorner.z));
                }

                m_boidArray[i].Position = initPos; // the initial random position of the boid

            } // if


        } // for  (int i = 0; i < numberOfElements; i++)


    } // SetBoidArray()


    void DetermineParamValue(string name, float currTime, ref float paramValue)
    {
        // check if deltaTime is more than SceneDuration of  the action plan => reset SceneStartTime to
        // the current time.

        // ref float paramValue has a value before the function was alled
      

        float deltaT = currTime - m_SceneStartTime; //  the delta time since the beginning of the
                                                    // current animation cycle
        //Debug.Log("DeltaT=");
        //Debug.Log(deltaT);

        List<ActionPlanController.Action> timedActions = m_actionPlan[name];

        if ( deltaT >= m_AnimationCycle)
        {
            m_SceneStartTime = currTime;
            deltaT = 0f; // wrap to the beginning of actionPlan to repeat the scene

        }

        // find the interval in the timedActions to which deltaTime belongs
        for (int j = 0; j < timedActions.Count; j++) // actionPlan is a piecewise linear function of each parameter
        {
                
            // deltaT satisfies one of the following conditions always
            if ( deltaT < (timedActions[0].T[1] + timedActions[0].T[1]) / 2)
            {
                paramValue = timedActions[0].V;
                return;
            }

            else if ( deltaT >= (timedActions[timedActions.Count - 1].T[0] + timedActions[timedActions.Count - 1].T[1]) / 2)
            { // the ith action is found for the current parameter
                paramValue = timedActions[timedActions.Count - 1].V;
                return;
            }

            else
            {
                for ( int k =1; k< timedActions.Count-1; k++ )
                {
                    if (deltaT >= (timedActions[k - 1].T[0] + timedActions[k - 1].T[1]) / 2
                         && deltaT < (timedActions[k].T[0] + timedActions[k].T[1]) / 2)
                    {
                        float t = (deltaT - (timedActions[k - 1].T[0] + timedActions[k - 1].T[1]) / 2)
                                   /

                                   (  (timedActions[k].T[0] + timedActions[k].T[1]) / 2
                                      -
                                      (timedActions[k - 1].T[0] + timedActions[k - 1].T[1]) / 2
                                    );
                                       
                        paramValue = timedActions[k-1].V * (1-t) * timedActions[k].V * t;
                        return;
                    }
                }

            }
              

        } // for all actions of the current parameter

    } // DetermineParaValue()



    private void Awake()
    {

        m_actionPlanController = this.gameObject.GetComponent<ActionPlanController>();

        if ( m_actionPlanController is null)
        {
            Debug.Log("ActionPlanController component should be added to CommHub");
        }

        m_threadGroupSize = Mathf.CeilToInt(m_BoidsNum / (float)BLOCK_SIZE);
  

        Debug.Log("################################");
        Debug.Log("I am in Awake() in SimpleBoids initializing for the simulation of boids");
        Debug.Log("################################");



        InitializeValues();
        InitializeBuffers();


    }//  private void Awake()



    private void Start()
    {
        //"initialize my connections to others, which have been initialized by their own Awake()

        m_actionPlan =  m_actionPlanController.m_actionPlan;

        m_SceneStartTime = Time.time; // set the current time in millisecond

        Debug.Log("Start Time=" + m_SceneStartTime);


    }


    // Update is called once per frame
    private void Update()
    {

        //Debug.Log("Start Time=" + m_SceneStartTime);

        //Debug.Log("currTime = " + Time.time);
        //Debug.Log("delta Time  = " + Time.deltaTime);

        // get the current time
        m_currTime = Time.time; //  seconds

        //Time.time simply gives you a numeric value which is equal to the number of seconds
        //which have elapsed since the project started playing.
        // Time.time (and Time.deltaTime) only change their value once per frame.


        Simulate(m_currTime);
    }



    protected void Simulate(float currTime) // called from Update()
    {


        //var threadGroupSize = Mathf.CeilToInt( (float) BoidsNum / (float) BLOCK_SIZE);

        // Determine the values of the parameters according to actionPlan

        if (UseActionPlan)
        {
            //DetermineParamValue("_MeshNo", currTime, ref _meshSetting.MeshNo);
            //DetermineParamValue("_Scale", currTime, ref _meshSetting.Scale);

            //DetermineParamValue("_BoidsNum", currTime, ref m_BoidsNum);
        

            DetermineParamValue("_SpeedFactor", currTime, ref _speedFactor);
            DetermineParamValue("_ScaleFactor", currTime, ref _scaleFactor);


            DetermineParamValue("_SeparateRadius", currTime, ref _separate.Radius);
            DetermineParamValue("_SeparateWeight", currTime, ref _separate.Weight);

            DetermineParamValue("_AlignmentRadius", currTime, ref _alignment.Radius);
            DetermineParamValue("_AlignmentWeight", currTime, ref _alignment.Weight);

            DetermineParamValue("_CohesionRadius", currTime, ref _cohesion.Radius);
            DetermineParamValue("_CohesionWeight", currTime, ref _cohesion.Weight);

            DetermineParamValue("_GroundFlockingWeight", currTime, ref _groundWeight.FlockingWeight);
            DetermineParamValue("_GroundDivergeWeight", currTime, ref _groundWeight.DivergeWeight);
            DetermineParamValue("_GroundCirculationWeight", currTime, ref _groundWeight.CirculationWeight);

            DetermineParamValue("_CeilingFlockingWeight", currTime, ref _ceilingWeight.FlockingWeight);
            DetermineParamValue("_CeilingConvergeWeight", currTime, ref _ceilingWeight.ConvergeWeight);
            DetermineParamValue("_CeilingCirculationWeight", currTime, ref _ceilingWeight.CirculationWeight);


            DetermineParamValue("_GroundMinHue", currTime, ref _groundMinHue);
            DetermineParamValue("_GroundMaxHue", currTime, ref _groundMaxHue);
            DetermineParamValue("_GroundMinSaturation", currTime, ref _groundMinSaturation);
            DetermineParamValue("_GroundMaxSaturation", currTime, ref _groundMaxSaturation);
            DetermineParamValue("_GroundMinValue", currTime, ref _groundMinValue);
            DetermineParamValue("_GroundMaxValue", currTime, ref _groundMaxValue);

            DetermineParamValue("_GroundMinAlpha", currTime, ref _groundMinAlpha);
            DetermineParamValue("_GroundMaxAlpha", currTime, ref _groundMaxAlpha);



            DetermineParamValue("_CeilingMinHue", currTime, ref _ceilingMinHue);
            DetermineParamValue("_CeilingMaxHue", currTime, ref _ceilingMaxHue);
            DetermineParamValue("_CeilingMinSaturation", currTime, ref _ceilingMinSaturation);
            DetermineParamValue("_CeilingMaxSaturation", currTime, ref _ceilingMaxSaturation);
            DetermineParamValue("_CeilingMinValue", currTime, ref _ceilingMinValue);
            DetermineParamValue("_CeilingMaxValue", currTime, ref _ceilingMaxValue);

            DetermineParamValue("_CeilingMinAlpha", currTime, ref _ceilingMinAlpha);
            DetermineParamValue("_CeilingMaxAlpha", currTime, ref _ceilingMaxAlpha);


            // apply the current values of the parameters to the compute shader

            m_BoidComputeShader.SetFloat("_SpeedFactor", _speedFactor);
            m_BoidComputeShader.SetFloat("_ScaleFactor", _scaleFactor);


            m_BoidComputeShader.SetFloat("_SeparateRadius", _separate.Radius);
            m_BoidComputeShader.SetFloat("_SeparateWeight", _separate.Weight);

            m_BoidComputeShader.SetFloat("_AlignmentRadius", _alignment.Radius);
            m_BoidComputeShader.SetFloat("_AlignmentWeight", _alignment.Weight);

            m_BoidComputeShader.SetFloat("_CohesionRadius", _cohesion.Radius);
            m_BoidComputeShader.SetFloat("_CohesionWeight", _cohesion.Weight);

            m_BoidComputeShader.SetFloat("_GroundFlockingWeight", _groundWeight.FlockingWeight);
            m_BoidComputeShader.SetFloat("_GroundDivergeWeight", _groundWeight.DivergeWeight);
            m_BoidComputeShader.SetFloat("_GroundCirculationWeight", _groundWeight.CirculationWeight);

            m_BoidComputeShader.SetFloat("_CeilingFlockingWeight", _ceilingWeight.FlockingWeight);
            m_BoidComputeShader.SetFloat("_CeilingConvergeWeight", _ceilingWeight.ConvergeWeight);
            m_BoidComputeShader.SetFloat("_CeilingCirculationWeight", _ceilingWeight.CirculationWeight);



            m_BoidComputeShader.SetFloat("_GroundMinHue", _groundMinHue);
            m_BoidComputeShader.SetFloat("_GroundMaxHue", _groundMaxHue);
            m_BoidComputeShader.SetFloat("_GroundMinSaturation", _groundMinSaturation);
            m_BoidComputeShader.SetFloat("_GroundMaxSaturation", _groundMaxSaturation);
            m_BoidComputeShader.SetFloat("_GroundMinValue", _groundMinValue);
            m_BoidComputeShader.SetFloat("_GroundMaxValue", _groundMaxValue);

            m_BoidComputeShader.SetFloat("_GroundMinAlpha", _groundMinAlpha);
            m_BoidComputeShader.SetFloat("_GroundMaxAlpha", _groundMaxAlpha);



            m_BoidComputeShader.SetFloat("_CeilingMinHue", _ceilingMinHue);
            m_BoidComputeShader.SetFloat("_CeilingMaxHue", _ceilingMaxHue);
            m_BoidComputeShader.SetFloat("_CeilingMinSaturation", _ceilingMinSaturation);
            m_BoidComputeShader.SetFloat("_CeilingMaxSaturation", _ceilingMaxSaturation);
            m_BoidComputeShader.SetFloat("_CeilingMinValue", _ceilingMinValue);
            m_BoidComputeShader.SetFloat("_CeilingMaxValue", _ceilingMaxValue);

            m_BoidComputeShader.SetFloat("_CeilingMinAlpha", _ceilingMinAlpha);
            m_BoidComputeShader.SetFloat("_CeilingMaxAlpha", _ceilingMaxAlpha);



            m_BoidComputeShader.SetFloat("_DeltaTime", Time.deltaTime);

            //https://msdn.microsoft.com/en-us/library/windows/desktop/ff471566(v=vs.85).aspx
            //https://stackoverflow.com/questions/19860586/compute-shader-with-numthreads-1-1-1-runs-extremly-slow

            // The disptach call invokes threadGroupSize(256) * 1 * 1 Thread Groups in undefined order

           
            m_BoidComputeShader.Dispatch(m_KernelIdCountBoids, 1, 1, 1);

            //cf.    m_KernelIdGround = m_BoidComputeShader.FindKernel("SimulateCSGround");
            m_BoidComputeShader.Dispatch(m_KernelIdGround, m_threadGroupSize, 1, 1);
            m_BoidComputeShader.Dispatch(m_KernelIdCeiling, m_threadGroupSize, 1, 1);

            // Each thread group, e.g.  SV_GroupID = (2,0,0) will contain BLOCK_SIZE * 1 * 1 threads according to the
            // declaration "numthreads(BLOCK_SIZE, 1, 1)]" in the computeshader.
            // E.G., Thread ID  SV_GroupThreadID = (7,0,0) refers to a particular thread in a given thread group
            // This Thread ID is also represented by the global index SV_DispatchThreadID
            // = ( [2,0,0] * (BLOCK_SIZE,1,1] + [7,0,0] ) =(BoidID, 0,0)


        } // if (UseActionPlan)


        // for debugging
        // totalNumOfSimulations++;

        // Debug.Log("Iteration Num of Simuation:");
        // Debug.Log(totalNumOfSimulations);


        //// m_boids.m_BoidBuffer
        //m_BoidBuffer.GetData(m_boidArray); // used in LEDColorGenController

        //for (int i = 0; i < (int) m_BoidsNum/100; i++)
        //{

        //    Debug.Log("boidNo = "); Debug.Log(i);
        //    Debug.Log("position = = ");
        //    Debug.Log(m_boidArray[i].Position);

        //    Debug.Log("wall no = = ");
        //    Debug.Log(m_boidArray[i].WallNo);

        //    Debug.Log("color (HSV) = = ");
        //    Debug.Log(m_boidArray[i].ColorHSV); // h,s,v ranges from 0 to 1

        //    Debug.Log("color (RGB by my API) = = ");
        //    Debug.Log(m_boidArray[i].Color);

        //    Color color = Color.HSVToRGB(m_boidArray[i].ColorHSV.x, m_boidArray[i].ColorHSV.y, m_boidArray[i].ColorHSV.z);


        //    Debug.Log("color (RGB by Unity API) = = ");
        //    Debug.Log(color);
            
        //    //m_boidArray[i].Color = new Vector4(color.r, color.g, color.b, m_boidArray[i].Color.w);


        //}

        //BoidBuffer.SetData(m_boidArray); // buffer is R or RW

        /*
        BoidCountBuffer.GetData(boidCountArray);

        int numOfBoidsWithinBound = 0;
        for (int i = 0; i < numOfWalls; i++)
        {
            Debug.LogFormat("WallNo = {0}, num of boids within bound =  {1} , ratio over the total= {2}",
                           i, boidCountArray[i], boidCountArray[i] / (float)m_BoidsNum);


            numOfBoidsWithinBound += boidCountArray[i];


        }

        Debug.LogFormat("total num within bound= {0}, ratio over the tatal =  {1}",
                              numOfBoidsWithinBound, numOfBoidsWithinBound / (float)m_BoidsNum);
       */

        //Color.HSVToRGB(h,s,v)

    } // Simulate()


} // class SimpleBoids
