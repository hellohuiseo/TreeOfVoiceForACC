using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System;
using System.Reflection;


using Random = UnityEngine.Random;

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
public class SimpleBoidLEDColors : MonoBehaviour
{


    SimpleBoidsTreeOfVoice _boids; // set in the inspector

    protected const int BLOCK_SIZE = 1024; // The number of threads in a single thread group

    protected const int MAX_SIZE_OF_BUFFER = 10000;

    const float epsilon = 1e-2f;
    const float M_PI = 3.1415926535897932384626433832795f;

    float m_SceneStartTime; // = Time.time;
    float m_SceneDuration = 390.0f; // 120 seconds


    // 보이드의 수
    public float m_BoidsNum = 1000f;

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
    public ComputeBuffer BoidBuffer { get; protected set; } // null reference

    public ComputeBuffer BoidLEDBuffer { get; protected set; } // null reference

    int BufferStartIndex, BufferEndIndex;

    protected int KernelIdLEDColor, KernelIdCeiling, KernelIdCountBoids;

    // for debugging
    BoidLEDData[] boidLEDArray;
    
  


    // Use this for initialization
    private void Awake()
    {




       
    }

    // Use this for initialization
    private void Start() {

        //  _meshSetting = new MeshSetting(m_MeshNo, m_Scale);


        // gameObjForWallTransforms = new GameObject[numOfWalls];

        Debug.Log("################################");
        Debug.Log("I am in Start()");
        Debug.Log("################################");

        m_SceneStartTime = Time.time; // set the current time in millisecond

        InitializeValues();
        InitializeBuffers();
    }

    // Update is called once per frame
    private void Update() {
        SampleBoidLEDColors();
    }

    void OnValidate() // called before the start() 
    {


    } // OnValidate()

//public static Color HSVToRGB(float H, float S, float V);
protected void InitializeValues()
    {
        
        BufferEndIndex = (int) m_BoidsNum;


        BoidLEDComputeShader.SetInt("_BoidsNum", (int) m_BoidsNum);

        KernelIdLEDColor = BoidLEDComputeShader.FindKernel("SampleLEDColors");

        BoidLEDComputeShader.SetBuffer(KernelIdLEDColor, "_BoidBuffer", _boids.BoidBuffer);
       
       
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


        BoidCountBuffer = new ComputeBuffer(numOfWalls, Marshal.SizeOf(typeof(int)));

        BoidBuffer = new ComputeBuffer(MAX_SIZE_OF_BUFFER,  Marshal.SizeOf(typeof(BoidData)));

        boidArray = new BoidData[MAX_SIZE_OF_BUFFER];
        boidCountArray = new int[numOfWalls]; // array of nulls




        for (int i = 0; i < numOfWalls; i++)
        {
            boidCountArray[i] = 0;
        }

        //var boidArray = new BoidData[BoidsNum];
        SetBoidArray(0, (int) m_BoidsNum);
        

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

        BoidBuffer.SetData(boidArray); // buffer is R or RW
        BoidCountBuffer.SetData(boidCountArray); // buffer is R or RW

        BoidComputeShader.SetBuffer(KernelIdGround, "_BoidBuffer", BoidBuffer);
        //BoidComputeShader.SetBuffer(KernelIdGround, "_BoidCountBuffer", BoidCountBuffer);
        BoidComputeShader.SetBuffer(KernelIdCeiling, "_BoidBuffer", BoidBuffer);
        //BoidComputeShader.SetBuffer(KernelIdCeiling, "_BoidCountBuffer", BoidCountBuffer);


        BoidComputeShader.SetBuffer(KernelIdCountBoids, "_BoidCountBuffer", BoidCountBuffer);
        BoidComputeShader.SetBuffer(KernelIdCountBoids, "_BoidBuffer", BoidBuffer);

    } // InitializeBuffers()

    
  

    protected void SetBoidArray(int startIndex, int numberOfElements)
    {


    Vector3 initPos, initScale;
        float theta, phi, initSpeed, initRadiusX, initRadiusY, initRadiusZ;
    Vector4 initColor;
    Vector2 initSoundGrain;
    int wallNo;

    for (int i = startIndex; i < numberOfElements; i++)
        {
            // set the head direction of the boid

            if ( !Use3DMotion)
            // use 2D boids
            {

                // use 2D direction vector

                //  direction angle on xz plane
                theta = Random.Range(0, M_PI);


                boidArray[i].HeadDir = new Vector3(Mathf.Cos(theta), 0.0f, Mathf.Sin(theta));

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
                boidArray[i].HeadDir = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
            } // 3D

               
            


                initRadiusX = Random.Range(MinBoidRadius, MaxBoidRadius);
                initRadiusY = Random.Range(MinBoidRadius, MaxBoidRadius);
                initRadiusZ = Random.Range(MinBoidRadius, MaxBoidRadius);

                initScale = new Vector3(initRadiusX, initRadiusY, initRadiusZ);

                initSpeed = Random.Range(_minSpeed, _maxSpeed);

                boidArray[i].Radius = initRadiusX;

                boidArray[i].Scale = initScale;


                boidArray[i].Speed = initSpeed;

      

                wallNo = i % numOfWalls; // from 0 to 1
                // the number of the wall on which the boid lie. 0=> the ground
                // 1 => the ceiling, 2 => left wall, 3=> right wall. 4=> front   wall

              
                boidArray[i].WallNo = wallNo;

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

                    boidArray[i].Position = initPos; // the initial random position of the boid

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

                    boidArray[i].Position = initPos; // the initial random position of the boid

                } // if

                
        } // for  (int i = startIndex; i < numberOfElements; i++)


    } // SetBoidArray()


	void DetermineParamValue(  string name, float currTime, ref float paramValue)
	{
		// check if deltaTime is more than SceneDuration of  the action plan => reset SceneStartTime to
		// the current time.

		// ref float paramValue has a value before the function was alled
		float prevActionTime;
		float nextActionTime;
		float intervalBetweenActions;


		float elaspedTime = currTime - m_SceneStartTime; //  the elapsed time since the beginning of the
		//  scene (in seconds)

		List<Action> timedActions = actionPlan[name];

		if ( elaspedTime >= m_SceneDuration)
		{
			m_SceneStartTime = currTime;
			elaspedTime = 0f; // wrap to the beginning of actionPlan to repeat the scene

		}

		// find the interval in the timedActions to which deltaTime belongs
		for (int i=0; i < timedActions.Count - 1; i++)
		{
			prevActionTime = timedActions[i].Time;
			nextActionTime = timedActions[i + 1].Time;
			intervalBetweenActions = nextActionTime - prevActionTime;

			if ( elaspedTime >= prevActionTime &&  elaspedTime < nextActionTime)
			{
				float timeFromPrevAction = (elaspedTime - prevActionTime);

				if ( timeFromPrevAction < intervalBetweenActions * timedActions[i].Weight )
				{ // use the previous param value
					paramValue = timedActions[i].Value;
					return;

				}
				else // linearly interpolate from the value of the currDelta to the value of nextDelta
                // executed only when timedActions[o].Weight is not 1f
				{ // deltaTime >= nextDelta => interpolate between the current and the next
					float f = timeFromPrevAction / intervalBetweenActions;
					paramValue = timedActions[i].Value * (1 - f) + timedActions[i+1].Value * f;
					return;

				}

			} // if



		} // for

	} // DetermineParaValue()



    protected void Simulate()
	{

        // get the current time
        float currTime = Time.time; //  seconds

        //Time.time simply gives you a numeric value which is equal to the number of seconds
        //which have elapsed since the project started playing.
        // Time.time (and Time.deltaTime) only change their value once per frame.

        // Debug.Log("currTime = " + Time.time);
        // Debug.Log("delta Time  = " + Time.deltaTime);

        //var threadGroupSize = Mathf.CeilToInt( (float) BoidsNum / (float) BLOCK_SIZE);



        // Determine the values of the parameters according to actionPlan

        if (UseActionPlan)
        {
            DetermineParamValue("_BoidsNum", currTime, ref m_BoidsNum);
        }
        // ref BoidsNum has a value before calling  the function; ref = inout

        var threadGroupSize = Mathf.CeilToInt(m_BoidsNum / (float)BLOCK_SIZE);

        // check if the boids number changed => reset the boidsbuffer

        if (m_BoidsNum < BoidsNumPrev) // the current boids less than the previous
        {
            //add random boids to the at the of the buffer
            BufferEndIndex = (int)m_BoidsNum;
            BoidsNumPrev = (int)m_BoidsNum;
        }

        else
        if (m_BoidsNum > BoidsNumPrev)

        {  //add random boids to the at the of the buffer

            float numOfBoidsToAdd = m_BoidsNum - BoidsNumPrev;

            SetBoidArray(BufferEndIndex, (int)numOfBoidsToAdd);

            BufferEndIndex = (int)m_BoidsNum;
            BoidsNumPrev = (int)m_BoidsNum;

            BoidBuffer.SetData(boidArray); // buffer is R or RW

        }

  

        BoidComputeShader.SetInt("_BoidsNum", (int) m_BoidsNum);


        if (UseActionPlan)
        {
            //DetermineParamValue("_MeshNo", currTime, ref _meshSetting.MeshNo);
            //DetermineParamValue("_Scale", currTime, ref _meshSetting.Scale);

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

            //DetermineParamValue("_RightWallFlockingWeight", currTime, ref _rightWallWeight.FlockingWeight);
            //DetermineParamValue("_RightWallUpMoveWeight", currTime, ref _rightWallWeight.UpMoveWeight);




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


            //DetermineParamValue("_RightWallMinHue", currTime, ref _rightWallMinHue);
            //DetermineParamValue("_RightWallMaxHue", currTime, ref _rightWallMaxHue);
            //DetermineParamValue("_RightWallMinSaturation", currTime, ref _rightWallMinSaturation);
            //DetermineParamValue("_RightWallMaxSaturation", currTime, ref _rightWallMaxSaturation);
            //DetermineParamValue("_RightWallMinValue", currTime, ref _rightWallMinValue);
            //DetermineParamValue("_RightWallMaxValue", currTime, ref _rightWallMaxValue);

            //DetermineParamValue("_RightWallMinAlpha", currTime, ref _rightWallMinAlpha);
            //DetermineParamValue("_RightWallMaxAlpha", currTime, ref _rightWallMaxAlpha);

        }

        // apply the current values of the parameters to the compute shader

        BoidComputeShader.SetFloat("_SpeedFactor", _speedFactor);
        BoidComputeShader.SetFloat("_ScaleFactor", _scaleFactor);


        BoidComputeShader.SetFloat("_SeparateRadius", _separate.Radius);
        BoidComputeShader.SetFloat("_SeparateWeight", _separate.Weight);

        BoidComputeShader.SetFloat("_AlignmentRadius", _alignment.Radius);
        BoidComputeShader.SetFloat("_AlignmentWeight", _alignment.Weight);

        BoidComputeShader.SetFloat("_CohesionRadius", _cohesion.Radius);
        BoidComputeShader.SetFloat("_CohesionWeight", _cohesion.Weight);

        BoidComputeShader.SetFloat("_GroundFlockingWeight", _groundWeight.FlockingWeight);
        BoidComputeShader.SetFloat("_GroundDivergeWeight", _groundWeight.DivergeWeight);
        BoidComputeShader.SetFloat("_GroundCirculationWeight", _groundWeight.CirculationWeight);

        BoidComputeShader.SetFloat("_CeilingFlockingWeight", _ceilingWeight.FlockingWeight);
        BoidComputeShader.SetFloat("_CeilingConvergeWeight", _ceilingWeight.ConvergeWeight);
        BoidComputeShader.SetFloat("_CeilingCirculationWeight", _ceilingWeight.CirculationWeight);

        //BoidComputeShader.SetFloat("_RightWallFlockingWeight", _rightWallWeight.FlockingWeight);
        //BoidComputeShader.SetFloat("_RightWallUpMoveWeight", _rightWallWeight.UpMoveWeight);




        BoidComputeShader.SetFloat("_GroundMinHue", _groundMinHue);
        BoidComputeShader.SetFloat("_GroundMaxHue", _groundMaxHue);
        BoidComputeShader.SetFloat("_GroundMinSaturation", _groundMinSaturation);
        BoidComputeShader.SetFloat("_GroundMaxSaturation", _groundMaxSaturation);
        BoidComputeShader.SetFloat("_GroundMinValue", _groundMinValue);
        BoidComputeShader.SetFloat("_GroundMaxValue", _groundMaxValue);

        BoidComputeShader.SetFloat("_GroundMinAlpha", _groundMinAlpha);
        BoidComputeShader.SetFloat("_GroundMaxAlpha", _groundMaxAlpha);



        BoidComputeShader.SetFloat("_CeilingMinHue", _ceilingMinHue);
        BoidComputeShader.SetFloat("_CeilingMaxHue", _ceilingMaxHue);
        BoidComputeShader.SetFloat("_CeilingMinSaturation", _ceilingMinSaturation);
        BoidComputeShader.SetFloat("_CeilingMaxSaturation", _ceilingMaxSaturation);
        BoidComputeShader.SetFloat("_CeilingMinValue", _ceilingMinValue);
        BoidComputeShader.SetFloat("_CeilingMaxValue", _ceilingMaxValue);

        BoidComputeShader.SetFloat("_CeilingMinAlpha", _ceilingMinAlpha);
        BoidComputeShader.SetFloat("_CeilingMaxAlpha", _ceilingMaxAlpha);


        //BoidComputeShader.SetFloat("_RightWallMinHue", _rightWallMinHue);
        //BoidComputeShader.SetFloat("_RightWallMaxHue", _rightWallMaxHue);
        //BoidComputeShader.SetFloat("_RightWallMinSaturation", _rightWallMinSaturation);
        //BoidComputeShader.SetFloat("_RightWallMaxSaturation", _rightWallMaxSaturation);
        //BoidComputeShader.SetFloat("_RightWallMinValue", _rightWallMinValue);
        //BoidComputeShader.SetFloat("_RightWallMaxValue", _rightWallMaxValue);

        //BoidComputeShader.SetFloat("_RightWallMinAlpha", _rightWallMinAlpha);
        //BoidComputeShader.SetFloat("_RightWallMaxAlpha", _rightWallMaxAlpha);


      

    
        BoidComputeShader.SetFloat("_DeltaTime", Time.deltaTime);

        //https://msdn.microsoft.com/en-us/library/windows/desktop/ff471566(v=vs.85).aspx
        //https://stackoverflow.com/questions/19860586/compute-shader-with-numthreads-1-1-1-runs-extremly-slow
        // (1) The disptach call invokes threadGroupSize(256) * 1 * 1 Thread Groups in undefined order


        BoidComputeShader.Dispatch(KernelIdCountBoids, 1, 1, 1);

        BoidComputeShader.Dispatch(KernelIdGround, threadGroupSize, 1, 1);
        BoidComputeShader.Dispatch(KernelIdCeiling, threadGroupSize, 1, 1);

        // Each thread group, e.g.  SV_GroupID = (2,0,0) will contain BLOCK_SIZE * 1 * 1 threads according to the
        // declaration "numthreads(BLOCK_SIZE, 1, 1)]" in the computeshader.
        // E.G., Thread ID  SV_GroupThreadID = (7,0,0) refers to a particular thread in a given thread group
        // This Thread ID is also represented by the global index SV_DispatchThreadID
        // = ( [2,0,0] * (BLOCK_SIZE,1,1] + [7,0,0] ) =(BoidID, 0,0)

        // for debugging
        // totalNumOfSimulations++;

        // Debug.Log("Iteration Num of Simuation:");
        // Debug.Log(totalNumOfSimulations);



        //BoidBuffer.GetData(boidArray);

        // for (int i = 0; i < BoidsNum; i++)
        //{

        //     Debug.Log("boidNo = "); Debug.Log(i);
        //     Debug.Log("position = = ");
        //     Debug.Log(boidArray[i].Position);

        //     Debug.Log("wall no = = ");
        //     Debug.Log(boidArray[i].WallNo);

        //    Debug.Log("color = = "); 
        //    Debug.Log(boidArray[i].Color);

        //  Color color = Color.HSVToRGB(boidArray[i].Color.x, boidArray[i].Color.y, boidArray[i].Color.z);

        // boidArray[i].Color = new Vector4(color.r, color.g, color.b, boidArray[i].Color.w);


        // }

        // BoidBuffer.SetData(boidArray); // buffer is R or RW

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

            GameObject gameObj = new GameObject("GroundWall");

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
                gameObj.transform.rotation = Quaternion.AngleAxis( 0.5f * M_PI * Mathf.Rad2Deg, Vector3.right); // Z point upward
                gameObj.transform.localScale = Vector3.one;

                gameObjForWallTransforms[0] = gameObj;
            


            // wall 1

            gameObj = new GameObject("CeilingWall");

        
                wallOrigin = new Vector3(0.0f, CeilingMaxCorner.y, 0.0f);

                //https://gamedev.stackexchange.com/questions/140579/euler-right-handed-to-quaternion-left-handed-conversion-in-unity/140581
                //initEulerAngles = new Vector3(M_PI, 0.0f, 0.0f); //  pitch  = 180: the x to the right, the z to the back, the y down

                gameObj.transform.position = wallOrigin;
                // gameObjForWallTransforms[1].transform.eulerAngles = Mathf.Rad2Deg * initEulerAngles;
                gameObj.transform.rotation = Quaternion.AngleAxis( -0.5f * M_PI * Mathf.Rad2Deg, Vector3.right); // Z point upward

                gameObj.transform.localScale = Vector3.one;

                gameObjForWallTransforms[1] = gameObj;


            // wall 2

            gameObj = new GameObject("FrontWall");

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
                gameObjForWallTransforms[2] =  gameObj; 
            

                  

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
        //Draw the coordinate systems of each wall at the wallOrigin

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

    public struct BoidData
	{
       // public Vector3  WallOrigin; // the reference position of the wall (the boid reference frame) on which the boid is 

        //public Vector3 EulerAngles; // the rotation of the boid reference frame
        public Vector3 Position; // the position of the  boid in the boid reference frame        
       
        public Vector3 Scale; // the scale factors
        public Vector3 HeadDir; // heading direction of the boid on the local plane
        public float Speed;            // the speed of a boid

        public float Radius; // the radius of the circle boid
        public Vector4 Color;         // RGBA color
        public Vector2 SoundGrain; // soundGrain = (freq, amp)
        public float Duration;     // duration of a boid each frame
        public int   WallNo;      // the number of the wall on which the boid lie. 0=> the ground
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

} // class SimpleBoids
