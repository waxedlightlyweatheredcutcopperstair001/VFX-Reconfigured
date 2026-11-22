using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace VolumetricExplosionFX.Rendering
{
    internal enum UiRowKind { Section, Toggle, Slider, Choice, Action, Info }
    internal sealed class UiRow
    {
        internal UiRowKind Kind; internal string Label, Hint;
        internal Func<bool> GetBool; internal Action<bool> SetBool;
        internal Func<float> GetValue; internal Action<float> SetValue;
        internal float Min, Max, Step; internal Func<float,string> Format;
        internal string[] Options; internal Func<int> GetChoice; internal Action<int> SetChoice;
        internal string Button; internal Action Click; internal Func<string> Text;
        internal Func<bool> Available;
    }
    internal sealed class UiPage { internal string Title; internal readonly List<UiRow> Rows=new List<UiRow>(); }
    internal sealed class UiModel
    {
        internal string Title, Reset;
        internal readonly List<UiPage> Pages=new List<UiPage>();
        internal Action Close, ResetAll, Changed;
        internal Func<string> Status;
    }
    internal sealed class UiWindowDrag : MonoBehaviour, IDragHandler
    {
        internal Action<Vector2> Move;
        public void OnDrag(PointerEventData e)
        { if(e.button==PointerEventData.InputButton.Left&&Move!=null) Move(e.delta); }
    }
    // Procedural art for the settings window (no image files): rounded boxes, a soft shadow, the toolbar icon.
    internal static class UiArt
    {
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        static readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        internal static void Release()
        {
            foreach(UnityEngine.Object o in owned) if(o!=null) UnityEngine.Object.Destroy(o);
            owned.Clear(); sprites.Clear();
        }
        static Sprite Make(string key,int size,float border,Func<float,float,Color> pixel)
        {
            Sprite s;
            if(sprites.TryGetValue(key,out s)&&s!=null) return s;
            var t=new Texture2D(size,size,TextureFormat.RGBA32,false);
            t.name="VFXR settings art "+key; t.wrapMode=TextureWrapMode.Clamp; t.filterMode=FilterMode.Bilinear;
            var c=new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++) c[y*size+x]=pixel(x+0.5f,y+0.5f);
            t.SetPixels(c); t.Apply(false,true);
            s=Sprite.Create(t,new Rect(0,0,size,size),new Vector2(0.5f,0.5f),100,0,SpriteMeshType.FullRect,new Vector4(border,border,border,border));
            owned.Add(t); owned.Add(s); sprites[key]=s; return s;
        }
        static float Box(float x,float y,int n,float inset,float radius)
        {
            float qx=Mathf.Abs(x-n*0.5f)-(n*0.5f-inset-radius), qy=Mathf.Abs(y-n*0.5f)-(n*0.5f-inset-radius);
            float a=Mathf.Max(qx,0), b=Mathf.Max(qy,0);
            return Mathf.Sqrt(a*a+b*b)+Mathf.Min(Mathf.Max(qx,qy),0)-radius;
        }
        internal static Sprite Fill(int radius)
        {
            radius=Mathf.Max(radius,0);
            int n=2*radius+4;
            return Make("fill"+radius,n,radius+2,(x,y)=>new Color(1,1,1,Mathf.Clamp01(0.5f-Box(x,y,n,0,radius))));
        }
        internal static Sprite Shadow()
        {
            const int n=96; const float spread=30;
            return Make("shadow",n,46,(x,y)=>{
                float d=Box(x,y,n,spread,10);
                float a=Mathf.Exp(-Mathf.Max(d,0)*Mathf.Max(d,0)/(2*9f*9f));
                return new Color(1,1,1,a); });
        }
        static bool Flame(float x,float y)
        {
            if(x*x+(y+0.32f)*(y+0.32f)<0.36f) return true;
            if(y<-0.32f||y>0.95f) return false;
            float h=(y+0.32f)/1.27f, w=0.6f*Mathf.Pow(1-h,1.15f), bend=0.2f*Mathf.Sin(h*3.4f)*h;
            return Mathf.Abs(x-bend)<w;
        }
        // The toolbar button's flame.
        internal static Texture2D Icon(int size)
        {
            var t=new Texture2D(size,size,TextureFormat.RGBA32,false); t.name="VFXR icon"; t.wrapMode=TextureWrapMode.Clamp;
            var c=new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                int outer=0, inner=0;
                for(int j=0;j<4;j++) for(int i=0;i<4;i++)
                {
                    float u=((x+(i+0.5f)/4)/size)*2.3f-1.15f, v=((y+(j+0.5f)/4)/size)*2.3f-1.15f;
                    if(Flame(u,v)) outer++;
                    if(Flame(u/0.5f,(v+0.4f)/0.5f)) inner++;
                }
                Color fire=Color.Lerp(new Color(0.95f,0.25f,0.05f),new Color(1,0.78f,0.25f),(float)y/size);
                Color col=Color.Lerp(fire,new Color(1,0.97f,0.85f),inner/16f); col.a=outer/16f; c[y*size+x]=col;
            }
            t.SetPixels(c); t.Apply(false,false); return t;
        }
    }
    // VFX Reconfigured: the settings window, one plain design matching BDArmory Lite (near-black panels, grey controls,
    // white text, orange accents): the title, text tabs along the top, sections of rows (label and short hint on the
    // left, the control on the right), and a footer with the defaults button and the save status. uGUI built at run time.
    internal sealed class SettingsPanel : IDisposable
    {
        static Color C(float r,float g,float b,float a=1) { return new Color(r,g,b,a); }
        static readonly Color Ink=C(0.902f,0.902f,0.902f), Soft=C(0.604f,0.627f,0.651f), Faint=C(0.420f,0.439f,0.463f),
            Accent=C(0.941f,0.639f,0.235f), OnAccent=C(0.106f,0.110f,0.122f), Body=C(0.106f,0.110f,0.122f,0.97f),
            Line=C(0.204f,0.212f,0.231f), Track=C(0.204f,0.216f,0.235f), Hover=C(0.251f,0.267f,0.290f,0.7f), ShadowColour=C(0,0,0,0.45f);
        const int Radius=4, Control=3, LabelSize=13, HintSize=11;
        const float Width=680, Height=580, ContentLeft=24, ContentRight=30, ContentTop=96, RightColumn=210, SwitchW=38;
        readonly float contentWidth=Width-ContentLeft-ContentRight;
        readonly UiModel model; readonly Font font; readonly Camera camera;
        readonly CanvasScaler scaler;
        readonly List<RectTransform> pages=new List<RectTransform>();
        readonly List<Text> tabLabels=new List<Text>();
        readonly List<Vector2> tabSpans=new List<Vector2>();
        readonly List<List<Action>> updates=new List<List<Action>>();
        readonly ScrollRect scroll; readonly Text status;
        readonly RectTransform marker;
        internal readonly GameObject Root; internal readonly RectTransform Window;
        internal static Vector2 Position;
        int page=-1, screenW=-1, screenH=-1; float preferredScale, appliedScale;
        bool visible;
        internal bool Visible
        {
            get { return visible; }
            set
            {
                if(value==visible&&Root.activeSelf==value) return;
                if(!value) ReleaseFocus();
                visible=value; Root.SetActive(value);
                if(value) { Fit(); Refresh(); }
            }
        }
        internal int Page { get { return page; } set { Select(Mathf.Clamp(value,0,pages.Count-1)); } }
        internal SettingsPanel(UiModel model,Font font,Camera camera,float scale)
        {
            this.model=model; this.font=font; this.camera=camera; preferredScale=scale;
            Root=new GameObject("VFXR settings",typeof(RectTransform));
            var canvas=Root.AddComponent<Canvas>();
            canvas.renderMode=camera!=null?RenderMode.ScreenSpaceCamera:RenderMode.ScreenSpaceOverlay;
            if(camera!=null) { canvas.worldCamera=camera; canvas.planeDistance=1; }
            canvas.sortingOrder=60;
            scaler=Root.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize; scaler.referencePixelsPerUnit=100;
            Root.AddComponent<GraphicRaycaster>();
            Window=Rect("window",Root.transform); Window.anchorMin=Window.anchorMax=Window.pivot=new Vector2(0.5f,0.5f);
            Window.sizeDelta=new Vector2(Width,Height); Window.anchoredPosition=Position;
            Stretch(Img("shadow",Window,UiArt.Shadow(),ShadowColour).rectTransform,-30,-38,-30,-24);
            Stretch(Img("border",Window,UiArt.Fill(Radius),Line).rectTransform,-1,-1,-1,-1);
            Stretch(Img("body",Window,UiArt.Fill(Radius),Body).rectTransform,0,0,0,0);
            var grip=Img("drag",Window,null,Color.clear); grip.raycastTarget=true; Top(grip.rectTransform,0,48,0,0);
            grip.gameObject.AddComponent<UiWindowDrag>().Move=delta=>{ Window.anchoredPosition+=delta/Mathf.Max(appliedScale,0.01f); ClampPosition(); };
            // Title and close.
            At(Label("title",Window,model.Title,15,FontStyle.Bold,Ink).rectTransform,ContentLeft,-16,Width-120,22);
            Button close=Button("close",Window,"",()=>{ if(model.Close!=null) model.Close(); },UiArt.Fill(Control),Color.clear);
            At(close.GetComponent<RectTransform>(),Width-44,-12,30,30);
            var cross=Label("x",close.transform,"×",19,FontStyle.Normal,Soft); cross.alignment=TextAnchor.MiddleCenter; Stretch(cross.rectTransform,0,1,0,0);
            // Tabs: text, an orange underline under the chosen one.
            float x=ContentLeft-10;
            for(int i=0;i<model.Pages.Count;i++)
            {
                int index=i; string title=model.Pages[i].Title;
                float w=TextWidth(title,13,FontStyle.Bold)+24;
                Button tab=Button("tab "+title,Window,"",()=>Select(index),UiArt.Fill(Control),Color.clear);
                At(tab.GetComponent<RectTransform>(),x,-48,w,32);
                var label=Label("label",tab.transform,title,13,FontStyle.Bold,Soft); label.alignment=TextAnchor.MiddleCenter; Stretch(label.rectTransform,0,0,0,0);
                tabLabels.Add(label); tabSpans.Add(new Vector2(x+10,w-20));
                x+=w+4;
            }
            Top(Img("rule",Window,null,Line).rectTransform,83,1,0,0);
            marker=Img("marker",Window,null,Accent).rectTransform;
            marker.anchorMin=marker.anchorMax=new Vector2(0,1); marker.pivot=new Vector2(0,1);
            // Content.
            var view=Rect("viewport",Window); Stretch(view,ContentLeft,48,ContentRight-8,ContentTop);
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color=Color.clear;
            scroll=view.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.vertical=true;
            scroll.movementType=ScrollRect.MovementType.Clamped; scroll.inertia=true; scroll.decelerationRate=0.12f;
            scroll.scrollSensitivity=30; scroll.viewport=view;
            for(int i=0;i<model.Pages.Count;i++) BuildPage(model.Pages[i],view);
            // Scroll bar.
            var bar=Rect("scrollbar",Window); bar.anchorMin=new Vector2(1,0); bar.anchorMax=new Vector2(1,1); bar.pivot=new Vector2(1,0.5f);
            bar.offsetMin=new Vector2(-14,52); bar.offsetMax=new Vector2(-8,-ContentTop);
            var handleArea=Rect("handle area",bar); Stretch(handleArea,0,0,0,0);
            var handle=Img("handle",handleArea,UiArt.Fill(2),Faint); Stretch(handle.rectTransform,0,0,0,0);
            var scrollbar=bar.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect=handle.rectTransform; scrollbar.targetGraphic=handle;
            scrollbar.direction=Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar=scrollbar; scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            // Footer: defaults and the status.
            Top(Img("footer rule",Window,null,Line).rectTransform,Height-48,1,0,0);
            if(model.Reset!=null)
            {
                Button reset=Button("reset",Window,model.Reset,()=>{ if(model.ResetAll!=null) model.ResetAll(); Changed(); },UiArt.Fill(Control),Track);
                RectTransform r=reset.GetComponent<RectTransform>(); r.anchorMin=r.anchorMax=r.pivot=Vector2.zero;
                r.anchoredPosition=new Vector2(ContentLeft,10); r.sizeDelta=new Vector2(Mathf.Max(110,TextWidth(model.Reset,12,FontStyle.Normal)+32),28);
            }
            status=Label("status",Window,"",11,FontStyle.Normal,Faint); status.alignment=TextAnchor.MiddleRight;
            var sr=status.rectTransform; sr.anchorMin=sr.anchorMax=sr.pivot=new Vector2(1,0); sr.sizeDelta=new Vector2(contentWidth-160,28); sr.anchoredPosition=new Vector2(-ContentRight+8,10);
            Select(0); SetLayer(Root.transform,5); Fit(); Root.SetActive(false);
        }
        internal void SetScale(float scale) { if(Mathf.Abs(preferredScale-scale)<0.001f) return; preferredScale=scale; screenW=-1; Fit(); }
        void Fit()
        {
            int w=camera!=null?camera.pixelWidth:Screen.width, h=camera!=null?camera.pixelHeight:Screen.height;
            if(w==screenW&&h==screenH) return;
            screenW=w; screenH=h;
            appliedScale=Mathf.Max(0.1f,Mathf.Min(Mathf.Clamp(preferredScale,0.35f,2.5f),Mathf.Min((w-24)/Width,(h-24)/Height)));
            scaler.scaleFactor=appliedScale; ClampPosition();
        }
        void ClampPosition()
        {
            float x=Mathf.Max(0,(screenW-24)/appliedScale*0.5f-Width*0.5f), y=Mathf.Max(0,(screenH-24)/appliedScale*0.5f-Height*0.5f);
            Vector2 p=Window.anchoredPosition; p.x=Mathf.Clamp(p.x,-x,x); p.y=Mathf.Clamp(p.y,-y,y);
            Window.anchoredPosition=p; Position=p;
        }
        void Changed() { if(model.Changed!=null) model.Changed(); Refresh(); }
        void Select(int index)
        {
            if(index<0||index>=pages.Count) return;
            page=index;
            for(int i=0;i<pages.Count;i++) { pages[i].gameObject.SetActive(i==page); tabLabels[i].color=i==page?Ink:Soft; }
            Vector2 span=tabSpans[page]; marker.anchoredPosition=new Vector2(span.x,-81); marker.sizeDelta=new Vector2(span.y,2);
            scroll.content=pages[page]; pages[page].anchoredPosition=Vector2.zero; scroll.velocity=Vector2.zero;
            Refresh();
        }
        void Refresh()
        {
            if(page>=0&&page<updates.Count) foreach(Action update in updates[page]) update();
            if(status!=null&&model.Status!=null) TextIfChanged(status,model.Status());
        }
        internal void Tick(float dt) { if(visible&&Root.activeSelf) { Fit(); Refresh(); } }
        void BuildPage(UiPage p,RectTransform view)
        {
            var content=Rect("page "+p.Title,view);
            content.anchorMin=new Vector2(0,1); content.anchorMax=new Vector2(1,1); content.pivot=new Vector2(0.5f,1);
            var refresh=new List<Action>(); updates.Add(refresh);
            float y=4; bool first=true;
            foreach(UiRow row in p.Rows)
            {
                if(row.Kind==UiRowKind.Section) { y=Section(content,row.Label??"",y,first); first=false; continue; }
                if(!first) Top(Img("hairline",content,null,Line).rectTransform,y,1,0,0);
                y+=Row(row,content,y,refresh); first=false;
            }
            content.sizeDelta=new Vector2(0,y+8); pages.Add(content);
        }
        // A section heading: an orange label in capitals and a line running from it.
        float Section(RectTransform inner,string title,float y,bool first)
        {
            if(!first) y+=10;
            string text=title.ToUpperInvariant();
            Top(Label("section",inner,text,11,FontStyle.Bold,Accent).rectTransform,y+8,14,0,0);
            Top(Img("section line",inner,null,Line).rectTransform,y+15,1,TextWidth(text,11,FontStyle.Bold)+10,0);
            return y+28;
        }
        // A row: the label and its hint on the left, the control on the right; wide choices go under it.
        float Row(UiRow row,RectTransform group,float y,List<Action> refresh)
        {
            float width=contentWidth;
            var band=Rect("row "+row.Label,group);
            bool side=row.Kind==UiRowKind.Toggle||row.Kind==UiRowKind.Slider||row.Kind==UiRowKind.Choice&&FitsBeside(row,RightColumn);
            float buttonW=row.Kind==UiRowKind.Action?Mathf.Max(92,TextWidth(row.Button??"",12,FontStyle.Bold)+24):0;
            bool choices=row.Options!=null&&row.Options.Length>0;
            float textW=row.Kind==UiRowKind.Action&&!choices?width-buttonW-16:!side?width:row.Kind==UiRowKind.Toggle?width-SwitchW-16:width-(RightColumn+16);
            const float top=10; float labelH=0, hintH=0;
            if(!string.IsNullOrEmpty(row.Label))
            {
                var label=Label("label",band,row.Label,LabelSize,FontStyle.Normal,Ink); label.horizontalOverflow=HorizontalWrapMode.Wrap;
                At(label.rectTransform,0,-top,textW,18); labelH=Mathf.Max(18,Mathf.Ceil(label.preferredHeight));
                label.rectTransform.sizeDelta=new Vector2(textW,labelH);
            }
            if(!string.IsNullOrEmpty(row.Hint))
            {
                var hint=Label("hint",band,row.Hint,HintSize,FontStyle.Normal,Soft); hint.horizontalOverflow=HorizontalWrapMode.Wrap;
                At(hint.rectTransform,0,-(top+labelH+1),textW,14); hintH=Mathf.Ceil(hint.preferredHeight)+1;
                hint.rectTransform.sizeDelta=new Vector2(textW,hintH);
            }
            float textBottom=top+labelH+hintH;
            float height=Mathf.Max(40,textBottom+top);
            var selectables=new List<Selectable>();
            switch(row.Kind)
            {
                case UiRowKind.Toggle: Switch(row,band,width,height*0.5f,refresh,selectables); break;
                case UiRowKind.Slider:
                {
                    float mid=(height-22)*0.5f;
                    var value=Label("value",band,"",12,FontStyle.Normal,Soft); value.alignment=TextAnchor.MiddleRight;
                    At(value.rectTransform,width-54,-mid,54,22);
                    Slider(row,band,width-RightColumn,mid,RightColumn-62,value,refresh,selectables); break;
                }
                case UiRowKind.Choice:
                    if(side) Chips(row,band,width-RightColumn,(height-26)*0.5f,RightColumn,true,refresh,selectables);
                    else { float at=textBottom+(labelH>0||hintH>0?8:-2); Chips(row,band,0,at,width,false,refresh,selectables); height=at+26+top; }
                    break;
                case UiRowKind.Action:
                {
                    float below=choices?textBottom+8:(height-26)*0.5f;
                    if(choices) Chips(row,band,0,below,width-buttonW-10,false,refresh,selectables);
                    Button b=Button("action",band,row.Button,()=>{ if(Available(row)&&row.Click!=null) row.Click(); },UiArt.Fill(Control),Accent);
                    var bl=b.GetComponentInChildren<Text>(); bl.color=OnAccent; bl.fontStyle=FontStyle.Bold;
                    At(b.GetComponent<RectTransform>(),width-buttonW,-below,buttonW,26); selectables.Add(b);
                    if(choices) height=below+26+top;
                    break;
                }
                default:
                {
                    var text=Label("info",band,row.Text!=null?row.Text():"",HintSize,FontStyle.Normal,Accent); text.horizontalOverflow=HorizontalWrapMode.Wrap;
                    float at=top+labelH+1; At(text.rectTransform,0,-at,width,14);
                    float h=Mathf.Ceil(text.preferredHeight)+1; text.rectTransform.sizeDelta=new Vector2(width,h); height=at+h+top;
                    break;
                }
            }
            Top(band,y,height,0,0);
            var fade=band.gameObject.AddComponent<CanvasGroup>(); bool on=!Available(row);
            Action availability=()=>{ bool now=Available(row); if(now==on) return; on=now; fade.alpha=now?1:0.4f; foreach(Selectable s in selectables) s.interactable=now; };
            availability(); refresh.Add(availability);
            return height;
        }
        bool FitsBeside(UiRow row,float width)
        {
            if(row.Options==null||row.Options.Length==0||row.Options.Length>3) return false;
            float total=0;
            foreach(string o in row.Options) total+=TextWidth(o,12,FontStyle.Normal)+22;
            return total+6*(row.Options.Length-1)<=width;
        }
        float TextWidth(string text,int size,FontStyle fontStyle)
        {
            if(font==null||string.IsNullOrEmpty(text)) return 0;
            font.RequestCharactersInTexture(text,size,fontStyle);
            float w=0; CharacterInfo info;
            foreach(char ch in text) if(font.GetCharacterInfo(ch,out info,size,fontStyle)) w+=info.advance;
            return w;
        }
        static bool Available(UiRow row) { return row.Available==null||row.Available(); }
        // A switch: a grey pill that turns orange, with a white knob.
        void Switch(UiRow row,RectTransform band,float right,float mid,List<Action> refresh,List<Selectable> selectables)
        {
            const float h=20;
            var track=Img("checkbox",band,UiArt.Fill(Control),Track); At(track.rectTransform,right-SwitchW,-(mid-h*0.5f),SwitchW,h); track.raycastTarget=true;
            var glow=Img("on",track.transform,UiArt.Fill(Control),Accent); Stretch(glow.rectTransform,0,0,0,0);
            var knob=Img("knob",track.transform,UiArt.Fill(2),Color.white);
            var kr=knob.rectTransform; kr.anchorMin=kr.anchorMax=new Vector2(0,0.5f); kr.pivot=new Vector2(0,0.5f); kr.sizeDelta=new Vector2(h-6,h-6);
            var toggle=track.gameObject.AddComponent<Toggle>(); toggle.targetGraphic=track; toggle.toggleTransition=Toggle.ToggleTransition.None;
            ColorBlock c=toggle.colors; c.normalColor=Color.white; c.highlightedColor=Color.white; c.pressedColor=new Color(0.9f,0.9f,0.9f);
            c.selectedColor=Color.white; c.disabledColor=Color.white; c.fadeDuration=0.1f; toggle.colors=c;
            Action<bool> place=v=>{ glow.enabled=v; kr.anchoredPosition=new Vector2(v?SwitchW-(h-6)-3:3,0); knob.color=v?Color.white:C(0.8f,0.82f,0.84f); };
            toggle.SetIsOnWithoutNotify(row.GetBool()); place(toggle.isOn);
            toggle.onValueChanged.AddListener(v=>{ place(v); if(toggle.IsActive()&&Available(row)&&v!=row.GetBool()) { row.SetBool(v); Changed(); } });
            refresh.Add(()=>{ bool v=row.GetBool(); if(v!=toggle.isOn) { toggle.SetIsOnWithoutNotify(v); place(v); } });
            selectables.Add(toggle);
        }
        // A slider: a grey track, filled orange up to an orange knob.
        void Slider(UiRow row,RectTransform band,float left,float top,float width,Text value,List<Action> refresh,List<Selectable> selectables)
        {
            var r=Rect("slider",band); At(r,left-6,-top,width+12,22);
            r.gameObject.AddComponent<Image>().color=Color.clear;
            var track=Img("track",r,UiArt.Fill(1),Track); Stretch(track.rectTransform,8,9,8,9);
            var fillArea=Rect("fill area",r); Stretch(fillArea,8,9,8,9);
            var fill=Img("fill",fillArea,UiArt.Fill(1),Accent); Stretch(fill.rectTransform,0,0,0,0);
            var handleArea=Rect("handle area",r);
            handleArea.anchorMin=new Vector2(0,0.5f); handleArea.anchorMax=new Vector2(1,0.5f);
            handleArea.offsetMin=new Vector2(8,-7); handleArea.offsetMax=new Vector2(-8,7);
            var handle=Img("handle",handleArea,UiArt.Fill(2),Accent); handle.raycastTarget=true;
            handle.rectTransform.anchorMin=handle.rectTransform.anchorMax=new Vector2(0,0.5f);
            handle.rectTransform.sizeDelta=new Vector2(10,0);
            var slider=r.gameObject.AddComponent<Slider>(); slider.minValue=row.Min; slider.maxValue=row.Max;
            slider.fillRect=fill.rectTransform; slider.handleRect=handle.rectTransform; slider.targetGraphic=handle;
            slider.direction=UnityEngine.UI.Slider.Direction.LeftToRight;
            ColorBlock c=slider.colors; c.normalColor=Color.white; c.highlightedColor=new Color(1.1f,1.1f,1.1f); c.pressedColor=new Color(0.85f,0.85f,0.85f);
            c.selectedColor=Color.white; c.disabledColor=Color.white; c.fadeDuration=0.1f; slider.colors=c;
            slider.SetValueWithoutNotify(row.GetValue());
            slider.onValueChanged.AddListener(v=>{
                if(!slider.IsActive()||!Available(row)) return;
                if(row.Step>0) v=row.Min+Mathf.Round((v-row.Min)/row.Step)*row.Step;
                v=Mathf.Clamp(v,row.Min,row.Max);
                if(Mathf.Abs(v-row.GetValue())>0.0001f) { row.SetValue(v); Changed(); }
            });
            float last=float.NaN;
            refresh.Add(()=>{
                float v=row.GetValue(); if(v==last) return; last=v;
                slider.SetValueWithoutNotify(v); TextIfChanged(value,row.Format!=null?row.Format(v):v.ToString("0.##"));
            });
            selectables.Add(slider);
        }
        // Choices side by side; the chosen one filled orange.
        void Chips(UiRow row,RectTransform band,float left,float top,float width,bool beside,List<Action> refresh,List<Selectable> selectables)
        {
            if(row.Options==null||row.Options.Length==0) return;
            int n=row.Options.Length; const float gap=6;
            var widths=new float[n]; float natural=0;
            for(int i=0;i<n;i++) { widths[i]=TextWidth(row.Options[i],12,FontStyle.Normal)+22; natural+=widths[i]; }
            natural+=gap*(n-1);
            float grow=beside?1:Mathf.Clamp((width-gap*(n-1))/Mathf.Max(natural-gap*(n-1),1),1,1.5f);
            float used=gap*(n-1); for(int i=0;i<n;i++) { widths[i]*=grow; used+=widths[i]; }
            float x=beside?left+width-used:left;
            var faces=new Image[n]; var labels=new Text[n];
            for(int i=0;i<n;i++)
            {
                int index=i;
                Button b=Button("option "+row.Options[i],band,row.Options[i],()=>{
                    if(Available(row)&&row.GetChoice()!=index) { row.SetChoice(index); Changed(); }
                },UiArt.Fill(Control),Track);
                At(b.GetComponent<RectTransform>(),x,-top,widths[i],26); selectables.Add(b);
                faces[i]=b.GetComponent<Image>(); labels[i]=b.GetComponentInChildren<Text>();
                x+=widths[i]+gap;
            }
            int lastChoice=int.MinValue;
            refresh.Add(()=>{
                int chosen=row.GetChoice(); if(chosen==lastChoice) return; lastChoice=chosen;
                for(int i=0;i<n;i++)
                {
                    bool on=i==chosen;
                    var b=faces[i].GetComponent<Button>(); ColorBlock c=b.colors; c.normalColor=on?Accent:Track; c.selectedColor=c.normalColor;
                    c.highlightedColor=on?Accent:Hover; b.colors=c;
                    labels[i].color=on?OnAccent:Ink; labels[i].fontStyle=on?FontStyle.Bold:FontStyle.Normal;
                }
            });
        }
        Button Button(string name,Transform parent,string text,Action action,Sprite sprite,Color colour)
        {
            var face=Img(name,parent,sprite,Color.white); face.raycastTarget=true;
            var b=face.gameObject.AddComponent<Button>(); b.targetGraphic=face;
            ColorBlock c=b.colors; c.normalColor=colour;
            Color hover=colour.a<0.01f?Hover:new Color(Mathf.Min(1,colour.r*1.1f+0.03f),Mathf.Min(1,colour.g*1.1f+0.03f),Mathf.Min(1,colour.b*1.1f+0.03f),colour.a);
            c.highlightedColor=hover; c.selectedColor=colour; c.pressedColor=new Color(hover.r*0.85f,hover.g*0.85f,hover.b*0.85f,Mathf.Max(hover.a,0.08f));
            c.disabledColor=colour; c.colorMultiplier=1; c.fadeDuration=0.08f; b.colors=c;
            if(!string.IsNullOrEmpty(text))
            {
                var label=Label("label",face.transform,text,12,FontStyle.Normal,Ink); label.alignment=TextAnchor.MiddleCenter; Stretch(label.rectTransform,4,0,4,0);
            }
            b.onClick.AddListener(()=>action()); return b;
        }
        static void TextIfChanged(Text x,string value) { if(x.text!=(value??"")) x.text=value??""; }
        static void SetLayer(Transform x,int layer) { x.gameObject.layer=layer; for(int i=0;i<x.childCount;i++) SetLayer(x.GetChild(i),layer); }
        static RectTransform Rect(string name,Transform parent)
        { var go=new GameObject(name,typeof(RectTransform)); var r=(RectTransform)go.transform; r.SetParent(parent,false); return r; }
        static Image Img(string name,Transform parent,Sprite sprite,Color colour)
        {
            var r=Rect(name,parent); var i=r.gameObject.AddComponent<Image>(); i.sprite=sprite;
            i.type=sprite!=null&&sprite.border!=Vector4.zero?Image.Type.Sliced:Image.Type.Simple; i.color=colour; i.raycastTarget=false; return i;
        }
        Text Label(string name,Transform parent,string text,int size,FontStyle fontStyle,Color colour)
        {
            var r=Rect(name,parent); var x=r.gameObject.AddComponent<Text>(); x.font=font; x.fontSize=size; x.fontStyle=fontStyle;
            x.color=colour; x.text=text??""; x.raycastTarget=false; x.supportRichText=false; x.alignment=TextAnchor.UpperLeft;
            x.horizontalOverflow=HorizontalWrapMode.Overflow; x.verticalOverflow=VerticalWrapMode.Overflow; return x;
        }
        static void At(RectTransform r,float x,float y,float w,float h)
        { r.anchorMin=r.anchorMax=new Vector2(0,1); r.pivot=new Vector2(0,1); r.anchoredPosition=new Vector2(x,y); r.sizeDelta=new Vector2(w,h); }
        static void Stretch(RectTransform r,float left,float bottom,float right,float top)
        { r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.pivot=new Vector2(0.5f,0.5f); r.offsetMin=new Vector2(left,bottom); r.offsetMax=new Vector2(-right,-top); }
        static void Top(RectTransform r,float from,float height,float left,float right)
        { r.anchorMin=new Vector2(0,1); r.anchorMax=new Vector2(1,1); r.pivot=new Vector2(0.5f,1); r.offsetMin=new Vector2(left,-from-height); r.offsetMax=new Vector2(-right,-from); }
        internal bool Contains(Vector2 screen)
        { return visible&&Root.activeSelf&&RectTransformUtility.RectangleContainsScreenPoint(Window,screen,camera); }
        internal bool HasKeyboardFocus
        {
            get
            {
                GameObject selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
                return visible&&selected!=null&&selected.transform.IsChildOf(Root.transform);
            }
        }
        internal void ReleaseFocus()
        { if(EventSystem.current!=null) { GameObject s=EventSystem.current.currentSelectedGameObject; if(s!=null&&s.transform.IsChildOf(Root.transform)) EventSystem.current.SetSelectedGameObject(null); } }
        public void Dispose() { if(Root!=null) UnityEngine.Object.Destroy(Root); }
    }
}
