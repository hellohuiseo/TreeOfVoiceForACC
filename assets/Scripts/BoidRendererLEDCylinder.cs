using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
 

public class BoidRendererLEDCylinder: MonoBehaviour
{

    // ComputeBuffer: GPU data buffer, mostly for use with compute shaders.
    // you can create & fill them from script code, and use them in compute shaders or regular shaders.


     // Declare other Component _boids; you can drag any gameobject that has that Component attached to it.
     // This will acess the Component directly rather than the gameobject itself.


    SimpleBoidsTreeOfVoice m_boids; // _boids.BoidBuffer is a ComputeBuffer
    LEDColorGenController m_LEDColorGenController;


    [SerializeField] Material m_boidLEDInstanceMaterial;

    int BoidsNum;

    // [SerializeField] protected Vector3 RoomMinCorner = new Vector3(-10f, 0f, -10f);
    // [SerializeField] protected Vector3 RoomMaxCorner = new Vector3(10f, 12f, 10f);


    // GPU Instancing
    // Graphics.DrawMeshInstancedIndirect
    // 인스턴스화 된 쉐이더를 사용하여 특정 시간동안 동일한 메시를 그릴 경우 사용
     CylinderMesh m_instanceMeshCylinder;
  

    Mesh m_boidInstanceMesh;
    public float m_scale = 1.0f; // the scale of the instance mesh

    //private ComputeBuffer colorBuffer;

    //     ArgsOffset      인스턴스 당 인덱스 수    (Index count per instance)
    //                     인스턴스 수              (Instance count)
    //                     시작 인덱스 위치         (Start index location)
    //                     기본 정점 위치           (Base vertex location)
    //                     시작 인스턴스 위치       (Start instance location)
    ComputeBuffer m_boidLEDArgsBuffer;
   

    uint[] m_boidLEDArgs = new uint[5] { 0, 0, 0, 0, 0 };


    uint numIndices;
    Vector3[] vertices3D;

    int[] indices;


    float height = 10; // m; scale = 0.1 ~ 0.3
    float radius = 0.1f; // 0.1 m =10cm

    // parameters for cylinder construction
    int nbSides = 18;
    int nbHeightSeg = 1;


    //// Create Vector2 vertices
    float unitRadius = 1f; // radius = 1m



    private void Awake()
    { // initialize me


        // check if the global component object is defined
        if (m_boidLEDInstanceMaterial == null)
        {
            Debug.LogError("The global Variable _boidLEDInstanceMaterial is not  defined in Inspector");
            // EditorApplication.Exit(0);
            return;
        }

        m_instanceMeshCylinder = new CylinderMesh(height, radius, nbSides, nbHeightSeg);

        m_boidInstanceMesh = m_instanceMeshCylinder.m_mesh;
        

        //_instanceMeshCircle.RecalculateNormals();
        //_instanceMeshCircle.RecalculateBounds();

        m_boidLEDArgsBuffer = new ComputeBuffer(
            1, // count
            m_boidLEDArgs.Length * sizeof(uint),

            ComputeBufferType.IndirectArguments
        );


       
    }
    void Start () 
	{   // initialize others

        // get the reference to SimpleBoidsTreeOfVoice

        m_boids = this.gameObject.GetComponent<SimpleBoidsTreeOfVoice>();
    

        if (m_boids == null)
        {
            Debug.LogError("SimpleBoidsTreeOfVoice component should be attached to CommHub");
            //EditorApplication.Exit(0);
            // Application.Quit();
            return;
            
        }

        //BoidLED rendering 

        m_LEDColorGenController = this.gameObject.GetComponent<LEDColorGenController>();


        if (m_LEDColorGenController == null)
        {
            Debug.LogError("m_LEDColorGenController should be added to CommHub");
            Application.Quit();
            // return; // nothing to render; 

        }


        // BOIDS Drawing

        numIndices = m_boidInstanceMesh ? m_boidInstanceMesh.GetIndexCount(0) : 0;
        //GetIndexCount(submesh = 0)



        m_boidLEDArgs[0] = numIndices;  //  (Index count per instance)

        m_boidLEDArgs[1] = (uint)m_LEDColorGenController.m_totalNumOfLEDs; //   (Instance count)

        m_boidLEDArgsBuffer.SetData(m_boidLEDArgs);


        m_boidLEDInstanceMaterial.SetVector("_Scale", new Vector3(m_scale, m_scale, m_scale) );

        m_boidLEDInstanceMaterial.SetVector("GroundMaxCorner", m_boids.GroundMaxCorner);
        m_boidLEDInstanceMaterial.SetVector("GroundMinCorner", m_boids.GroundMinCorner);

        m_boidLEDInstanceMaterial.SetVector("CeilingMaxCorner", m_boids.CeilingMaxCorner);
        m_boidLEDInstanceMaterial.SetVector("CeilingMinCorner", m_boids.CeilingMinCorner);

        m_boidLEDInstanceMaterial.SetBuffer("_BoidLEDBuffer", m_LEDColorGenController.m_BoidLEDBuffer); 
        // m_boids.BoidBuffer is ceated in SimpleBoids.cs



    } // Start()
	
	// Update is called once per frame
	public void Update () 
	{
		RenderInstancedMesh();              
       
    }

	private void OnDestroy()
	{
		if(m_boidLEDArgsBuffer == null) return;
		m_boidLEDArgsBuffer.Release();
		m_boidLEDArgsBuffer = null;


    }

	private void RenderInstancedMesh()
	{
        //"_BoidBuffer" is changed by SimpleBoidsTreeOfVoice
        // for debugging, comment out
        Graphics.DrawMeshInstancedIndirect(
            m_boidInstanceMesh,
            0,
            m_boidLEDInstanceMaterial, // This material defines the shader which receives instanceID
            new Bounds(m_boids.RoomCenter, m_boids.RoomSize),
            m_boidLEDArgsBuffer // this contains the information about the instances: see below
        );

        // _boidArgs = 
        ////                     인스턴스 당 인덱스 수    (Index count per instance)
        ////                     인스턴스 수              (Instance count)
        ////                     시작 인덱스 위치         (Start index location)
        ////                     기본 정점 위치           (Base vertex location)
        ////                     시작 인스턴스 위치       (Start instance location)
        //ComputeBuffer _argsBuffer;
        // uint[] _boidArgs = new uint[5] { 0, 0, 0, 0, 0 };


        // reading from the buffer written by regular shaders
        //https://gamedev.stackexchange.com/questions/128976/writing-and-reading-computebuffer-in-a-shader

        // _boids.BoidBuffer.GetData(boidArray);



    }//private void RenderInstancedMesh()

    //  public struct BoidData
    //  {
    //     public Vector2 Position; // the position of a boid center; float x and float y
    //     public Vector2 Scale; // the scale factors of x and z directions
    //     public float Angle; // the head angle of a boid: from 0 to 2 *PI
    //     public float Speed;            // the speed of a boid
    //     public Vector4 Color;         // RGBA color
    //     public Vector2 SoundGrain; // soundGrain = (freq, amp)
    //      public float Duration;     // duration of a boid each frame
    //      public int  WallNo;      // indicates whether the boid is on ground or on ceiling
    //   }

}
