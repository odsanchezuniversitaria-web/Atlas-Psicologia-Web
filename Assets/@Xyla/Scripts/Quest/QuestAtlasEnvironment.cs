using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Xyla
{
    public sealed class QuestAtlasEnvironment : MonoBehaviour
    {
        QuestAtlasController owner;
        OVRManager manager;
        OVRPassthroughLayer passthrough;
        Camera[] cameras;
        GameObject decor;
        ParticleSystem motes;
        AudioSource ambient, cues;
        AudioClip confirm, listen;
        Material linesMaterial, dotsMaterial;
        Mesh lineMesh;
        bool hearing;
        float nextTick;
        public bool PassthroughEnabled {get;private set;}
        public bool EffectsEnabled {get;private set;}
        public bool SoundsEnabled {get;private set;}

        public void Initialize(QuestAtlasController atlas)
        {
            owner=atlas;
            if(AtlasWebPlatform.Active)cameras=new[]{Camera.main};
            else {
            var rig=FindFirstObjectByType<OVRCameraRig>();
            manager=rig.GetComponent<OVRManager>(); cameras=rig.GetComponentsInChildren<Camera>(true);
            passthrough=rig.GetComponent<OVRPassthroughLayer>();
            if(!passthrough) passthrough=rig.gameObject.AddComponent<OVRPassthroughLayer>();
            manager.isInsightPassthroughEnabled=true;
            passthrough.hidden=false;
            passthrough.overridePerLayerColorScaleAndOffset=true;
            passthrough.colorScale=new Vector4(.84f,.95f,1f,1f);
            passthrough.colorOffset=new Vector4(0f,.008f,.035f,0f);
            }
            foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if(root.name=="Suelo de referencia") root.GetComponent<MeshRenderer>().enabled=false;
            decor=new GameObject("Laboratorio holográfico · sin colisiones");
            decor.transform.position=owner.transform.position;
            linesMaterial=new Material(Resources.Load<Material>("AtlasHoloLab"));
            dotsMaterial=new Material(linesMaterial); dotsMaterial.SetFloat("_Dots",2);
            BuildLines(); BuildParticles(); BuildLabel();
            ambient=decor.AddComponent<AudioSource>(); ambient.playOnAwake=false; ambient.loop=true; ambient.spatialBlend=0;
            ambient.clip=Resources.Load<AudioClip>("AtlasLabAudio/Ambient"); ambient.volume=.065f;
            cues=gameObject.AddComponent<AudioSource>(); cues.playOnAwake=false; cues.spatialBlend=0; cues.volume=.19f;
            confirm=Resources.Load<AudioClip>("AtlasLabAudio/Confirm");
            listen=Resources.Load<AudioClip>("AtlasLabAudio/Listen");
            if(!ambient.clip||!confirm||!listen) throw new InvalidOperationException("Faltan sonidos del laboratorio.");
            SetPassthrough(PlayerPrefs.GetInt("Atlas.Passthrough",1)==1);
            SetEffects(PlayerPrefs.GetInt("Atlas.Effects",1)==1);
            SetSounds(PlayerPrefs.GetInt("Atlas.Sounds",1)==1);
        }
        public void SetPassthrough(bool enabled)
        {
            PassthroughEnabled=enabled;
            if(AtlasWebPlatform.Active){foreach(var camera in cameras){camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.004f,.012f,.026f,1);}return;}
            manager.isInsightPassthroughEnabled=enabled; passthrough.hidden=!enabled;
            foreach(var camera in cameras)
            {
                camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=enabled?new Color(0,0,0,0):new Color(.004f,.012f,.026f,1);
            }
            PlayerPrefs.SetInt("Atlas.Passthrough",enabled?1:0);
        }
        public void SetEffects(bool enabled)
        {
            EffectsEnabled=enabled;
            decor.GetComponent<MeshRenderer>().enabled=enabled;
            if(enabled) motes.Play(); else motes.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            PlayerPrefs.SetInt("Atlas.Effects",enabled?1:0);
        }
        public void SetSounds(bool enabled)
        {
            SoundsEnabled=enabled; owner.SetSelectionSounds(enabled);
            if(enabled && !ambient.isPlaying) ambient.Play(); else if(!enabled) {ambient.Stop();cues.Stop();}
            PlayerPrefs.SetInt("Atlas.Sounds",enabled?1:0);
        }
        public void PlayConfirm() {if(SoundsEnabled) cues.PlayOneShot(confirm,.8f);}
        void Update()
        {
            if(Time.unscaledTime<nextTick) return;
            nextTick=Time.unscaledTime+.05f;
            bool current=owner.Voice.HearingSpeech;
            if(current&&!hearing&&SoundsEnabled) cues.PlayOneShot(listen,.4f);
            hearing=current;
            float target=SoundsEnabled && !owner.Voice.Listening ? .065f : .008f;
            if(owner.Voice.Speaking) target=0;
            ambient.volume=Mathf.MoveTowards(ambient.volume,target,.012f);
        }
        void BuildLines()
        {
            var vertices=new List<Vector3>();var colors=new List<Color>();var indices=new List<int>();var uv=new List<Vector2>();
            Color cyan=new Color(.035f,.83f,.93f,.62f), dim=new Color(.035f,.50f,.74f,.38f);
            Action<Vector3,Vector3,float,Color> line=(a,b,width,color)=>{
                Vector3 direction=(b-a).normalized;
                Vector3 side=Vector3.Cross(direction,Mathf.Abs(direction.y)>.8f?Vector3.forward:Vector3.up).normalized*width*.5f;
                int start=vertices.Count;
                vertices.Add(a-side);vertices.Add(a+side);vertices.Add(b+side);vertices.Add(b-side);
                for(int i=0;i<4;i++) colors.Add(color);
                uv.Add(new Vector2(0,0));uv.Add(new Vector2(0,1));uv.Add(new Vector2(1,1));uv.Add(new Vector2(1,0));
                indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            };
            for(int ring=0;ring<3;ring++)
            {
                float r=.66f+ring*.10f;
                for(int i=0;i<96;i++)
                {
                    if(ring==1&&i%12>7) continue;
                    float a=i*Mathf.PI/48,b=(i+1)*Mathf.PI/48;
                    line(new Vector3(Mathf.Sin(a)*r,.006f,Mathf.Cos(a)*r),new Vector3(Mathf.Sin(b)*r,.006f,Mathf.Cos(b)*r),ring==0?.007f:.003f,ring==0?cyan:dim);
                }
            }
            for(int i=0;i<48;i++)
            {
                float a=i*Mathf.PI/24;var v=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));
                line(v*.89f+Vector3.up*.007f,v*(i%4==0?.95f:.92f)+Vector3.up*.007f,.003f,dim);
            }
            foreach(float x in new[]{-.68f,.68f})
            {
                line(new Vector3(x,.12f,.46f),new Vector3(x,1.82f,.46f),.003f,dim);
                for(int i=0;i<18;i++) line(new Vector3(x,i*.09f+.15f,.46f),new Vector3(x+(x<0?.035f:-.035f),i*.09f+.15f,.46f),.002f,dim);
            }
            // Sparse hexagonal lattice behind the model: open center and transparent cells.
            Color lattice=new Color(.055f,.57f,.90f,.20f);
            for(int row=0;row<9;row++)
                for(int col=-6;col<=6;col++)
                {
                    float x=col*.28f+(row%2)*.14f,y=.2f+row*.245f;
                    if(Mathf.Abs(x)<.83f&&y<2.08f)continue;
                    for(int edge=0;edge<6;edge++)
                    {
                        if((row+col+edge)%5==0)continue;
                        float a=edge*Mathf.PI/3f,b=(edge+1)*Mathf.PI/3f;
                        line(new Vector3(x+Mathf.Sin(a)*.155f,y+Mathf.Cos(a)*.155f,1.18f),
                             new Vector3(x+Mathf.Sin(b)*.155f,y+Mathf.Cos(b)*.155f,1.18f),.0025f,lattice);
                    }
                }
            lineMesh=new Mesh{name="Trazos del laboratorio"};lineMesh.SetVertices(vertices);lineMesh.SetColors(colors);lineMesh.SetUVs(0,uv);lineMesh.SetTriangles(indices,0);lineMesh.RecalculateBounds();
            decor.AddComponent<MeshFilter>().sharedMesh=lineMesh;
            var renderer=decor.AddComponent<MeshRenderer>();renderer.sharedMaterial=linesMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        void BuildParticles()
        {
            var go=new GameObject("Partículas holográficas · máximo 96");go.transform.SetParent(decor.transform,false);go.transform.localPosition=new Vector3(0,1.2f,1.05f);
            motes=go.AddComponent<ParticleSystem>();motes.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=motes.main;main.loop=true;main.maxParticles=96;main.startLifetime=10;main.startSpeed=.012f;
            main.startSize=new ParticleSystem.MinMaxCurve(.018f,.038f);main.startColor=new Color(.14f,.78f,1,.67f);
            main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=motes.emission;emission.rateOverTime=8;
            var shape=motes.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(3.5f,2.1f,.65f);
            var velocity=motes.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.y=new ParticleSystem.MinMaxCurve(.025f,.055f);velocity.x=new ParticleSystem.MinMaxCurve(-.018f,.018f);velocity.z=new ParticleSystem.MinMaxCurve(-.008f,.008f);
            var noise=motes.noise;noise.enabled=true;noise.strength=.035f;noise.frequency=.25f;noise.scrollSpeed=.04f;noise.quality=ParticleSystemNoiseQuality.Low;
            var rotation=motes.rotationOverLifetime;rotation.enabled=true;rotation.z=.16f;
            var lifetime=motes.colorOverLifetime;lifetime.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.85f,.2f),new GradientAlphaKey(.65f,.75f),new GradientAlphaKey(0,1)});
            lifetime.color=gradient;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=dotsMaterial;renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        void BuildLabel()
        {
            var go=new GameObject("Identidad laboratorio",typeof(RectTransform),typeof(Canvas));
            go.transform.SetParent(decor.transform,false);go.transform.localPosition=new Vector3(0,.08f,.86f);
            go.transform.localScale=Vector3.one*.001f;go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
            var textGo=new GameObject("MR LAB",typeof(RectTransform),typeof(Text));textGo.transform.SetParent(go.transform,false);
            var text=textGo.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=24;text.alignment=TextAnchor.MiddleCenter;
            text.text="A T L A S   /   LABORATORIO MR";text.color=new Color(.13f,.74f,.86f,.65f);text.raycastTarget=false;
            text.rectTransform.sizeDelta=new Vector2(780,46);
        }
        void OnDestroy()
        {
            if(decor) Destroy(decor);if(linesMaterial)Destroy(linesMaterial);if(dotsMaterial)Destroy(dotsMaterial);if(lineMesh)Destroy(lineMesh);
        }
    }
}