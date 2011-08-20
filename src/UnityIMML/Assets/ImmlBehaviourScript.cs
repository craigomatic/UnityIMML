using UnityEngine;
using System.Collections;
using ImmlSpec = Imml;
using System.Net;
using System.Linq;
using Imml.Scene.Controls;
using Imml;
using System.IO;
using Imml.Scene.Container;

public class ImmlBehaviourScript : MonoBehaviour 
{
    private object _SyncWebController;
 	private HTTP.Request _HttpRequest;
	
    /// <summary>
    /// Loads the specified URI.
    /// </summary>
    /// <param name="uri">The URI.</param>
    void Load(string uri)
    {
        _HttpRequest = new HTTP.Request("GET", uri);
        _HttpRequest.Send();
    }

    private UnityEngine.PrimitiveType _GetType(ImmlSpec.PrimitiveType primitiveType)
    {
        switch(primitiveType)
        {
            case ImmlSpec.PrimitiveType.Box:
                return UnityEngine.PrimitiveType.Cube;
            case ImmlSpec.PrimitiveType.Cone:
                return UnityEngine.PrimitiveType.Capsule;
            case ImmlSpec.PrimitiveType.Cylinder:
                return UnityEngine.PrimitiveType.Cylinder;
            case ImmlSpec.PrimitiveType.Plane:
                return UnityEngine.PrimitiveType.Plane;
            case ImmlSpec.PrimitiveType.Sphere:
                return UnityEngine.PrimitiveType.Sphere;
        }

        return UnityEngine.PrimitiveType.Cube;
    }
	
	// Use this for initialization
	void Start () 
	{
		_SyncWebController = new object();
		this.Load("http://s3.amazonaws.com/vpa.qa/lights-camera-primitives.imml");
	}
	
	// Update is called once per frame
	void Update () 
	{
        lock (_SyncWebController)
        {
            if (_HttpRequest == null || (_HttpRequest != null && !_HttpRequest.isDone))
            {
                return;
            }

            ImmlDocument document = null;

            using (var ms = new MemoryStream(_HttpRequest.response.bytes))
            {

                _HttpRequest = null;

                var serialiser = new ImmlSpec.IO.ImmlSerialiser();
                document = serialiser.Read<ImmlSpec.Scene.Container.ImmlDocument>(ms);
            }

            Debug.Log(string.Format("Document loaded and contains: {0} elements", document.Elements.Count));

            _LoadLights(document);
            _LoadPrimitives(document);

            //setup the initial camera view
            var immlCamera = document.Elements.OfType<Imml.Scene.Controls.Camera>().Where(c => c.Name == document.Camera).FirstOrDefault();

            if (immlCamera != null)
            {
                this.transform.position = immlCamera.Position.ToUnityVector();
                this.transform.rotation = UnityEngine.Quaternion.Euler(immlCamera.Rotation.ToUnityVector());
            }
        }
	    //TODO: timeline implementation
	}

    private void _LoadLights(ImmlDocument document)
    {
        foreach (var immlLight in document.Elements.OfType<Imml.Scene.Controls.Light>())
        {
            var lightGameObject = new GameObject(immlLight.Name);
            lightGameObject.AddComponent<UnityEngine.Light>();
            lightGameObject.light.range = immlLight.Range;
            lightGameObject.light.color = immlLight.Diffuse.ToUnityColor(1);

            if (immlLight.CastShadows)
            {
                lightGameObject.light.shadows = LightShadows.Soft;
            }

            var rotation = immlLight.WorldRotation.Unify().ToUnityVector();

            switch (immlLight.Type)
            {
                case ImmlSpec.LightType.Directional:
                    {
                        lightGameObject.light.type = UnityEngine.LightType.Directional;
                        lightGameObject.transform.position = immlLight.WorldPosition.ToUnityVector();
                        lightGameObject.transform.rotation = UnityEngine.Quaternion.Euler(rotation);
                        break;
                    }
                case ImmlSpec.LightType.Point:
                    {
                        lightGameObject.light.type = UnityEngine.LightType.Point;
                        lightGameObject.transform.position = immlLight.WorldPosition.ToUnityVector();
                        lightGameObject.transform.rotation = UnityEngine.Quaternion.Euler(rotation);
                        break;
                    }
                case ImmlSpec.LightType.Spot:
                    {
                        lightGameObject.light.type = UnityEngine.LightType.Spot;
                        lightGameObject.transform.position = immlLight.WorldPosition.ToUnityVector();
                        lightGameObject.transform.rotation = UnityEngine.Quaternion.Euler(rotation);

                        if (immlLight.OuterCone > 0)
                        {
                            lightGameObject.light.spotAngle = immlLight.OuterCone;
                        }
                        break;
                    }
            }
        }
    }

    private void _LoadPrimitives(ImmlDocument document)
    {
        foreach (var immlPrimitive in document.Elements.OfType<Primitive>())
        {
            var primType = _GetType(immlPrimitive.Type);

            var prim = UnityEngine.GameObject.CreatePrimitive(primType);
            prim.transform.position = immlPrimitive.WorldPosition.ToUnityVector();
            prim.transform.rotation = UnityEngine.Quaternion.Euler(immlPrimitive.WorldRotation.Unify().ToUnityVector());

            switch (immlPrimitive.Type)
            {
                case Imml.PrimitiveType.Plane:
                    {
                        //unity has an inconsistent starting scale for plane primitives
                        prim.transform.localScale = immlPrimitive.Size.ToUnityVector() / 10;
                        prim.transform.position = new Vector3(immlPrimitive.WorldPosition.X, immlPrimitive.WorldPosition.Y - 0.5f, immlPrimitive.WorldPosition.Z);
                        break;
                    }
                case ImmlSpec.PrimitiveType.Cylinder:
                case ImmlSpec.PrimitiveType.Cone:
                    {
                        var scaled = new Vector3(immlPrimitive.WorldSize.X, immlPrimitive.WorldSize.Y / 2, immlPrimitive.WorldSize.Z);
                        prim.transform.localScale = scaled;
                        break;
                    }
                default:
                    {
                        prim.transform.localScale = immlPrimitive.Size.ToUnityVector();
                        break;
                    }
            }

            //materials
            var material = immlPrimitive.GetMaterialGroup(-1).GetMaterial();
            var alpha = material.Opacity;

            prim.renderer.material.shader = UnityEngine.Shader.Find("VertexLit");

            prim.renderer.material.SetColor("_Color", material.Diffuse.ToUnityColor(alpha));
            prim.renderer.material.SetColor("_SpecColor", material.Specular.ToUnityColor(alpha));
            prim.renderer.material.SetColor("_Emission", material.Emissive.ToUnityColor(alpha));
            prim.renderer.material.SetColor("_ReflectColor", material.Ambient.ToUnityColor(alpha));
            prim.renderer.castShadows = immlPrimitive.CastShadows;
            prim.renderer.receiveShadows = true;

            //physics
            var immlPhysics = immlPrimitive.GetPhysics();

            if (immlPhysics != null && immlPhysics.Enabled)
            {
                prim.AddComponent<Rigidbody>();

                if (immlPhysics.Movable)
                {
                    prim.rigidbody.mass = immlPhysics.Weight;
                }
                else
                {
                    prim.rigidbody.isKinematic = true;
                }

                prim.rigidbody.centerOfMass = immlPhysics.Centre.ToUnityVector();
                
                switch(immlPhysics.Bounding)
                {                    
                    case BoundingType.Box:
                        {
                            prim.AddComponent<BoxCollider>();
                            break;
                        }
                    case BoundingType.ConvexHull:
                        {
                            prim.AddComponent<MeshCollider>();
                            break;
                        }
                    case BoundingType.Cylinder:
                        {
                            prim.AddComponent<CapsuleCollider>();
                            break;
                        }
                    case BoundingType.Sphere:
                        {
                            prim.AddComponent<SphereCollider>();
                            break;
                        }

                }                
            }
        }
    }
}
