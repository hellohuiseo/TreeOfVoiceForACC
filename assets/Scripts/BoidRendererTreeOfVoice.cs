using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
 

public class BoidRendererTreeOfVoice : MonoBehaviour
{

    // ComputeBuffer: GPU data buffer, mostly for use with compute shaders.
    // you can create & fill them from script code, and use them in compute shaders or regular shaders.


     // Declare other Component _boids; you can drag any gameobject that has that Component attached to it.
     // This will acess the Component directly rather than the gameobject itself.


    [SerializeField]  SimpleBoidsTreeOfVoice m_boids; // _boids.BoidBuffer is a ComputeBuffer
    [SerializeField] Material _instanceMaterial;

    // [SerializeField] protected Vector3 RoomMinCorner = new Vector3(-10f, 0f, -10f);
    // [SerializeField] protected Vector3 RoomMaxCorner = new Vector3(10f, 12f, 10f);


    // 보이드의 수
    // public int BoidsNum = 256;


    protected Vector3  GroundMinCorner;
    protected Vector3  GroundMaxCorner;

    protected Vector3  CeilingMinCorner;
    protected Vector3  CeilingMaxCorner;

   
    
    // 보이드의 수
    int BoidsNum;

    // GPU Instancing
    // Graphics.DrawMeshInstancedIndirect
    // 인스턴스화 된 쉐이더를 사용하여 특정 시간동안 동일한 메시를 그릴 경우 사용
     Mesh _instanceMeshCircle;

    [SerializeField]  Mesh _instanceMeshSphere;

    public bool m_useCircleMesh = true;
   
    //[SerializeField]  Mesh _instanceMeshHuman1;
    //[SerializeField]  Mesh _instanceMeshHuman2;
    //[SerializeField]  Mesh _instanceMeshHuman3;
    //[SerializeField]  Mesh _instanceMeshHuman4;

   // public MeshSetting _meshSetting = new MeshSetting(1.0f, 1.0f);

   // int _meshNo; // set from SimpleBoids (used to be)

    Mesh _instanceMesh;
    public float m_scale = 1.0f; // the scale of the instance mesh

    //private ComputeBuffer colorBuffer;

    //     ArgsOffset      인스턴스 당 인덱스 수    (Index count per instance)
    //                     인스턴스 수              (Instance count)
    //                     시작 인덱스 위치         (Start index location)
    //                     기본 정점 위치           (Base vertex location)
    //                     시작 인스턴스 위치       (Start instance location)
    ComputeBuffer _argsBuffer;
    readonly uint[] _args = new uint[5] { 0, 0, 0, 0, 0 };

    uint numIndices;
    Vector3[] vertices3D;

    int[] indices;
    


    // //Display.Length will always be 1 in the Editor. We dont have an implementation of display detection in editor as it works different. We are using Editor windows, not actual displays.
    // //You can still use multiple display in the editor you just dont need to activate the displays like you do in a release.

    // //https://stackoverflow.com/questions/43066541/unity-multiple-displays-not-working

    // void InitializeCameras()
    // {


    //     // multiple camera tutorial: http://blog.theknightsofunity.com/using-multiple-unity-cameras-why-this-may-be-important/
    //     //Did you notice that the default Unity 5 scene camera clears buffers to Skybox?
    //     ////This is Main Camera in the scene
    //     //Camera m_MainCamera;
    //     //This is the second Camera and is assigned in inspector
    //     // public Camera m_CameraTwo;

    //     // Finding multiple cameras: https://answers.unity.com/questions/15801/finding-cameras.html
    //     //(1) In your code use the following sysntax:
    //     //Camera myCamera = GameObject.FindWithTag("MainCamera").GetComponent <> Camera > ();
    //     //
    //     // (2): 1) Simply drag a reference of the desired camera to a variable of type Camera in your script.

    //     //2) use Camera.main if the camera you want is the only active one right now.

    //     //3) If you have multiple cameras named uniquely, 
    //     //check foreach (Camera c in Camera.allCameras) and  c.gameObject.name == "DesiredCamera" 
    //     //then that is the camera you want.
    //     //.ner, far, fieldOfView
    //     // //Start the Camera field of view at 60
    //     //m_FieldOfView = 60.0f; This is the vertical field of view; 
    //     // .aspect = width / height
    //     //.name


    //     // Script inherits from MonoBehavior => Behavior => Component => object
    //     // Camera component inherits from Behavior => Component => object
    //     // Behaviours are Components that can be enabled or disabled.
    //     // For example, Rigidbody cannot be enabled/disabled. This is why it inherits from the Component class instead of Behaviour.
    //     // MonoBehaviour is the base class from which every Unity script derives.
    //     //MonoBehaviour:
    //     //The most important thing to note about MonoBehaviour is that you need it when you have to use corutines, Invoking,
    //     //or any Unity callback functions such as physics OnCollisionEnter function, Start, OnEnable, OnDisable, etc.
    //     //MonoBehaviour inherits from Behaviour so that your scripts can be enabled / disabled.
    //     //Note that Behaviour and Component are used by Unity for internal stuff. You should not try to inherit your script from these.


    // Use this for initialization
    void Start () 
	{
        // get the reference to SimpleBoidsTreeOfVoice

        m_boids = this.gameObject.GetComponent<SimpleBoidsTreeOfVoice>();

        if (m_boids == null)
        {
            Debug.LogError("SimpleBoidsTreeOfVoice component should be attached to CommHub");
            //EditorApplication.Exit(0);
            Application.Quit();
            
        }

        

        // check if the global component object is defined
        if (_instanceMaterial == null)
        {
            Debug.LogError("The global Variable _instanceMaterial is not  defined in Inspector");
            EditorApplication.Exit(0);

        }

        GroundMaxCorner = m_boids.GroundMaxCorner;
        GroundMinCorner = m_boids.GroundMinCorner;

        CeilingMaxCorner = m_boids.CeilingMaxCorner;
        CeilingMinCorner = m_boids.CeilingMinCorner;


        //Screen.SetResolution( sum of width of all displays, height, false)

       
        // InitializeCameras();



        // Create Vector2 vertices
        float unitRadius = 1f; // radius = 1m
        Vector2[] vertices = Triangulator.FindPointsOnCircle( unitRadius); // 2D circle on xz plane

        // Use the triangulator to get indices for creating triangles
        Triangulator tr = new Triangulator(vertices);
        indices = tr.Triangulate();

        vertices3D = new Vector3[vertices.Length];

        for (int i=0; i < vertices.Length; i++)
        {

            vertices3D[i] = new Vector3(vertices[i].x, 0.0f, vertices[i].y);

        }

        _instanceMeshCircle = new Mesh();

        _instanceMeshCircle.vertices = vertices3D;
        _instanceMeshCircle.triangles = indices;
        _instanceMeshCircle.RecalculateNormals();
        _instanceMeshCircle.RecalculateBounds();




        // for debugging

        // _instanceMesh = GetComponent < Mesh > (); // get the Mesh component for the current gameboject

        _argsBuffer = new ComputeBuffer(
			1, 
			_args.Length * sizeof(uint), 
			ComputeBufferType.IndirectArguments
		);


        //https://answers.unity.com/questions/979080/how-to-pass-an-array-to-a-shader.html
        //_instanceMaterial.SetFloatArray("GroundMaxCorner", GroundMaxCornerF);
        //_instanceMaterial.SetFloatArray("GroundMinCorner", GroundMinCornerF);

        // _instanceMaterial.SetFloatArray("CeilingMaxCorner", CeilingMaxCornerF);
        // _instanceMaterial.SetFloatArray("CeilingMinCorner", CeilingMinCornerF);

        // // Shader vectors are always Vector4s.
        // But the value here is converted to a Vector3.
        //Vector3 value = Vector3.one;
        //Renderer renderer = GetComponent<Renderer>();
        //renderer.material.SetVector("_SomeVariable", value);

        // Use SetVector() rather than setFloatArray
        _instanceMaterial.SetVector("GroundMaxCorner",  GroundMaxCorner);
        _instanceMaterial.SetVector("GroundMinCorner", GroundMinCorner);

         _instanceMaterial.SetVector("CeilingMaxCorner", CeilingMaxCorner);
         _instanceMaterial.SetVector("CeilingMinCorner", CeilingMinCorner);

       // _instanceMaterial.SetFloat("Use3DBoids", Use3DBoids);

        _instanceMaterial.SetBuffer("_BoidBuffer", m_boids.BoidBuffer); // m_boids.BoidBuffer is ceated in SimpleBoids.cs
    } // Start()
	
	// Update is called once per frame
	public void Update () 
	{
		RenderInstancedMesh();              
       
    }

	private void OnDestroy()
	{
		if(_argsBuffer == null) return;
		_argsBuffer.Release();
		_argsBuffer = null;
	}

	private void RenderInstancedMesh()
	{
        //var boidArray = new BoidData[BoidsNum];

        //_meshNo = (int) _boids._meshSetting.MeshNo;
        //_scale = _boids._meshSetting.Scale;

        if ( m_useCircleMesh ) // use 2D boids => creat the mesh in a script
        {
           
            _instanceMesh = _instanceMeshCircle;

            _instanceMaterial.SetVector("_Scale", new Vector3(m_scale, m_scale, m_scale) );
        }

        else 
        {
            _instanceMesh = _instanceMeshSphere;

            _instanceMaterial.SetVector("_Scale", new Vector3(m_scale, m_scale, m_scale));




            //  _instanceMesh.RecalculateNormals();
            // _instanceMesh.RecalculateBounds();


        }
//        else {
//            Debug.LogError("useCircleMesh or useSphereMesh should be checked");
//            //If we are running in a standalone build of the game
//            #if UNITY_STANDALONE
//            //Quit the application
//            Application.Quit();
//#endif

//            //If we are running in the editor
//            #if UNITY_EDITOR
//            //Stop playing the scene
//            // UnityEditor.EditorApplication.isPlaying = false;
//            //Setting isPlaying delays the result until after all script code has completed for this frame.

//            EditorApplication.Exit(0);
//            #endif
//        }



        //Debug.Log("number of indices=");
        //Debug.Log(_instanceMesh.GetIndexCount(0));


        // Indirect 
        numIndices = _instanceMesh ? _instanceMesh.GetIndexCount(0) : 0;
        //GetIndexCount(submesh = 0)



        // check if _boids.BoidBuffer is not null
        if (m_boids.BoidBuffer == null) return; // nothing to render; 


        // _boids.BoidBuffer.GetData(boidArray);
        // https://unity3d.com/kr/learn/tutorials/topics/graphics/gentle-introduction-shaders

        //Debug.Log("Current Num of Boids=");
        //Debug.Log(_boids.BoidsNum);

        _args[0] = numIndices;
        _args[1] = (uint)m_boids.m_BoidsNum;

        _argsBuffer.SetData(_args);



        Graphics.DrawMeshInstancedIndirect(
			_instanceMesh,
			0,
			_instanceMaterial,
			new Bounds(m_boids.RoomCenter, m_boids.RoomSize), 
			_argsBuffer
		);


        // reading from the buffer written by regular shaders
        //https://gamedev.stackexchange.com/questions/128976/writing-and-reading-computebuffer-in-a-shader
       
        // _boids.BoidBuffer.GetData(boidArray);




    }

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
