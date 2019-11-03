using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
 

public class BoidLEDRendererTreeOfVoice : MonoBehaviour
{

    

// ComputeBuffer: GPU data buffer, mostly for use with compute shaders.
// you can create & fill them from script code, and use them in compute shaders or regular shaders.


// Declare other Component _boids; you can drag any gameobject that has that Component attached to it.
// This will acess the Component directly rather than the gameobject itself.


    SimpleBoidsTreeOfVoice m_boids; // _boids.BoidBuffer is a ComputeBuffer
    LEDColorGenController m_LEDColorGenController;

    [SerializeField] Material m_boidLEDInstanceMaterial;

    int BoidsNum;
    
    Mesh m_instanceMeshCylinder;

    public float m_scale = 1.0f; // the scale of the instance mesh
      
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


    private void Awake()
    {


        if (m_boidLEDInstanceMaterial == null)
        {
            Debug.LogError("The global Variable m_boidLEDInstanceMaterial is not  defined in Inspector");
            // EditorApplication.Exit(0);
            Application.Quit();
            //return;

        }
               
        MeshCreator meshCylinderCreator = new MeshCreator();

        m_instanceMeshCylinder = meshCylinderCreator.CreateCylinderMesh(height, radius, nbSides, nbHeightSeg);

        m_boidLEDArgsBuffer = new ComputeBuffer(
          1,
          m_boidLEDArgs.Length * sizeof(uint),
          ComputeBufferType.IndirectArguments
         );

    } // Awake()
    void Start () 
	{
                
        // get the reference to SimpleBoidsTreeOfVoice

        m_boids = this.gameObject.GetComponent<SimpleBoidsTreeOfVoice>();
    

        if (m_boids == null)
        {
            Debug.LogError("SimpleBoidsTreeOfVoice component should be attached to CommHub");
            //EditorApplication.Exit(0);

            Application.Quit();
            //return;

        }


        // check if _boids.BoidBuffer is not null
        if (m_boids.m_BoidBuffer is null)
        {

            Debug.LogError("m_boids.m_BoidBuffer is null");
            //EditorApplication.Exit(0);
            Application.Quit();

            //return; 
        }


        //BoidLED rendering 

        m_LEDColorGenController = this.gameObject.GetComponent<LEDColorGenController>();


        if (m_LEDColorGenController.m_BoidLEDBuffer == null)
        {
            Debug.LogError("m_LEDColorGenController.m_BoidLEDBuffer should be set in Awake() of  LEDColorGenController");
            Application.Quit();
           // return; // nothing to render; 

        }

        
        ///
        numIndices = m_instanceMeshCylinder ? m_instanceMeshCylinder.GetIndexCount(0) : 0;
        //GetIndexCount(submesh = 0)


        m_boidLEDArgs[0] = numIndices;  // the number of indices in the set of triangles

        m_boidLEDArgs[1] = (uint) m_LEDColorGenController.m_totalNumOfLEDs; // the number of instances

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
             m_instanceMeshCylinder,

            0,
            m_boidLEDInstanceMaterial, // This material defines the shader which receives instanceID
            new Bounds(m_boids.RoomCenter, m_boids.RoomSize),
            m_boidLEDArgsBuffer // this contains the information about the instances
        );




    }//private void RenderInstancedMesh()

}//class BoidLEDRendererTreeOfVoice 
