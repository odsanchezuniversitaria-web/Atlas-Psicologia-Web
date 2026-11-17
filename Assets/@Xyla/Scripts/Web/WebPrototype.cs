using UnityEngine;
using UnityEngine.InputSystem;

namespace Xyla.Web
{
    public sealed class WebPrototype : MonoBehaviour
    {
        private CharacterController player;
        private Transform head;
        private Transform target;
        private Renderer targetRenderer;
        private float pitch;
        private float fall;
        private string notice = "";
        private float noticeUntil;
        private bool TouchMode => Application.isMobilePlatform;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            BuildWorld();
        }

        private void BuildWorld()
        {
            var dark = Mat(new Color(.09f,.14f,.2f));
            var panel = Mat(new Color(.12f,.25f,.34f));
            var cyan = Mat(new Color(.05f,.75f,.9f));
            var violet = Mat(new Color(.6f,.2f,.9f));
            var orange = Mat(new Color(1f,.4f,.05f));
            var yellow = Mat(new Color(1f,.8f,.05f));

            Shape("Floor",PrimitiveType.Cube,new Vector3(0,-.25f,0),new Vector3(24,.5f,24),dark);
            Shape("BackLimit",PrimitiveType.Cube,new Vector3(0,1.5f,12),new Vector3(24,3,.4f),panel);
            Shape("LeftLimit",PrimitiveType.Cube,new Vector3(-12,1.5f,0),new Vector3(.4f,3,24),panel);
            Shape("RightLimit",PrimitiveType.Cube,new Vector3(12,1.5f,0),new Vector3(.4f,3,24),panel);
            Shape("Cubo",PrimitiveType.Cube,new Vector3(-5,1,2),Vector3.one*2,cyan);
            Shape("Esfera",PrimitiveType.Sphere,new Vector3(0,1.2f,3),Vector3.one*2.4f,violet);
            Shape("Capsula",PrimitiveType.Capsule,new Vector3(5,1.5f,2),Vector3.one*1.6f,orange);
            Shape("StandBack",PrimitiveType.Cube,new Vector3(0,2.1f,8.7f),new Vector3(9,4.2f,.35f),panel);
            Shape("StandRoof",PrimitiveType.Cube,new Vector3(0,4.2f,7.3f),new Vector3(9,.35f,3),cyan);
            Shape("StandCounter",PrimitiveType.Cube,new Vector3(0,.75f,6.3f),new Vector3(6,1.5f,1.1f),panel);
            Label("STAND DE PRUEBA",new Vector3(0,3.2f,8.45f),.3f);
            Label("Acercate al cubo amarillo y presiona E",new Vector3(0,2.5f,8.43f),.12f);

            target=Shape("InteractiveCube",PrimitiveType.Cube,new Vector3(0,1.85f,6.15f),Vector3.one*1.25f,yellow).transform;
            targetRenderer=target.GetComponent<Renderer>();

            var p=new GameObject("WebPlayer");
            p.transform.position=new Vector3(0,1,-7);
            player=p.AddComponent<CharacterController>();
            player.height=1.8f; player.radius=.35f; player.center=new Vector3(0,.9f,0);
            var c=new GameObject("Main Camera");
            c.tag="MainCamera"; c.transform.SetParent(p.transform,false); c.transform.localPosition=new Vector3(0,1.6f,0);
            c.AddComponent<Camera>().fieldOfView=70; c.AddComponent<AudioListener>(); head=c.transform;
            if(!TouchMode){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        }

        private static Material Mat(Color color)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")); m.color=color; return m;
        }

        private static GameObject Shape(string n,PrimitiveType t,Vector3 p,Vector3 s,Material m)
        {
            var g=GameObject.CreatePrimitive(t);g.name=n;g.transform.position=p;g.transform.localScale=s;
            g.GetComponent<Renderer>().sharedMaterial=m;return g;
        }

        private static void Label(string value,Vector3 p,float size)
        {
            var g=new GameObject("Label");g.transform.position=p;g.transform.rotation=Quaternion.identity;
            var t=g.AddComponent<TextMesh>();t.text=value;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;
            t.fontSize=64;t.characterSize=size;t.color=Color.white;
        }

        private void Update()
        {
            Vector2 move=KeyboardMove(),look=MouseLook();
            if(TouchMode) ReadTouch(ref move,ref look);
            transform.Rotate(0,look.x,0);pitch=Mathf.Clamp(pitch-look.y,-80,80);head.localRotation=Quaternion.Euler(pitch,0,0);
            if(player.isGrounded&&fall<0)fall=-2;fall+=-18*Time.deltaTime;
            var velocity=(transform.right*move.x+transform.forward*move.y)*4.5f+Vector3.up*fall;
            player.Move(velocity*Time.deltaTime);
            if(Keyboard.current!=null&&Keyboard.current.eKey.wasPressedThisFrame)Interact();
            if(!TouchMode&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            if(!TouchMode&&Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        }

        private static Vector2 KeyboardMove()
        {
            if(Keyboard.current==null)return Vector2.zero;
            float x=(Keyboard.current.dKey.isPressed?1:0)-(Keyboard.current.aKey.isPressed?1:0);
            float y=(Keyboard.current.wKey.isPressed?1:0)-(Keyboard.current.sKey.isPressed?1:0);
            return Vector2.ClampMagnitude(new Vector2(x,y),1);
        }

        private static Vector2 MouseLook()
        {
            return Mouse.current!=null&&Cursor.lockState==CursorLockMode.Locked?Mouse.current.delta.ReadValue()*.11f:Vector2.zero;
        }

        private static void ReadTouch(ref Vector2 move,ref Vector2 look)
        {
            if(Touchscreen.current==null)return;
            foreach(var t in Touchscreen.current.touches)
            {
                if(!t.press.isPressed)continue;
                var start=t.startPosition.ReadValue();var pos=t.position.ReadValue();
                if(start.x<Screen.width*.5f)move=Vector2.ClampMagnitude((pos-start)/90,1);
                else if(pos.y>Screen.height*.22f)look+=t.delta.ReadValue()*.07f;
            }
        }

        private void Interact()
        {
            if(Vector3.Distance(player.transform.position,target.position)>3.2f){Show("Acercate un poco mas al cubo amarillo.");return;}
            targetRenderer.material.color=new Color(.2f,1,.35f);
            Show("Interaccion correcta: la misma accion servira en PC, celular y VR.");
        }

        private void Show(string value){notice=value;noticeUntil=Time.time+4;}

        private void OnGUI()
        {
            var title=new GUIStyle(GUI.skin.label){fontSize=26,fontStyle=FontStyle.Bold,alignment=TextAnchor.UpperCenter};
            title.normal.textColor=Color.white;
            GUI.Label(new Rect(0,15,Screen.width,40),"OSEOVERSE WEB - PROTOTIPO MULTIPLATAFORMA",title);
            GUI.Box(new Rect(Screen.width*.2f,58,Screen.width*.6f,55),"PC: WASD + raton + E   |   Celular: arrastra a ambos lados");
            if(TouchMode)
            {
                float s=Mathf.Min(Screen.width,Screen.height)*.18f;
                GUI.Box(new Rect(25,Screen.height-s-25,s,s),"MOVER\n< >");
                if(GUI.Button(new Rect(Screen.width-s-25,Screen.height-s-25,s,s*.6f),"INTERACTUAR"))Interact();
            }
            if(Time.time<noticeUntil)GUI.Box(new Rect(Screen.width*.18f,Screen.height*.72f,Screen.width*.64f,65),notice);
        }
    }
}