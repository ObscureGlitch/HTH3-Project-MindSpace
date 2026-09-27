using System;

namespace TheLastWatch.UI
{
    public sealed class WellnessMainMenuFlow
    {
        public enum Page { Home, Settings, Help, Credits, Exit }
        public Page Current {get;private set;}
        public int Selection {get;private set;}
        public bool Entering {get;private set;}
        public bool Entered {get;private set;}
        private double elapsed;
        private const double PopDuration=.24;
        private double selectionElapsed=PopDuration;
        public float Fade => (float)Math.Min(1,elapsed/.6);
        // A single gentle pulse, never an idle wobble. Repeated hover does not restart it.
        public float SelectionScale => (float)(1+.025*Math.Sin(Math.PI*Math.Min(1,selectionElapsed/PopDuration)));
        public bool Select(int index)
        {
            if(Current!=Page.Home||Entering||Entered||index<0||index>=5||index==Selection)return false;
            Selection=index;selectionElapsed=0;return true;
        }
        public void Move(int delta){Select(((Selection+delta)%5+5)%5);}
        // Input System screen coordinates start at the bottom left. Match the
        // centered IMGUI canvas exactly, including ultrawide/4:3 letterboxing.
        public static int OptionAt(double screenX,double screenY,int width,int height)
        {
            if(width<=0||height<=0||double.IsNaN(screenX)||double.IsNaN(screenY)||double.IsInfinity(screenX)||double.IsInfinity(screenY))return -1;
            double scale=Math.Min(width/1672d,height/941d);
            double x=(screenX-(width-1672*scale)*.5)/scale;
            double y=(height-screenY-(height-941*scale)*.5)/scale;
            if(x<93||x>=483||y<338)return -1;
            int index=(int)Math.Floor((y-338)/71);
            return index<5&&y<338+index*71+70?index:-1;
        }
        public void Open(Page page){if(!Entering&&!Entered)Current=page;}
        public void Back(){if(!Entering&&!Entered)Current=Current==Page.Home?Page.Exit:Page.Home;}
        public void Enter(){if(Current==Page.Home&&!Entering&&!Entered){Entering=true;elapsed=0;}}
        public bool Tick(double seconds)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<=0)return false;
            double step=Math.Min(seconds,.1);
            selectionElapsed=Math.Min(PopDuration,selectionElapsed+step);
            if(!Entering||Entered)return false;
            elapsed+=step;
            if(elapsed<.6-1e-8)return false;
            Entered=true;Entering=false;return true;
        }
        public static string Checks()
        {
            var f=new WellnessMainMenuFlow();f.Move(-1);if(f.Selection!=4)throw new Exception("Menu wrap.");f.Move(1);
            foreach(Page p in new[]{Page.Settings,Page.Help,Page.Credits,Page.Exit})
            {f.Open(p);f.Enter();if(f.Entering||f.Select(3))throw new Exception("Subpage must not enter game or accept hover.");f.Back();if(f.Current!=Page.Home)throw new Exception("Back navigation.");}
            f.Back();if(f.Current!=Page.Exit)throw new Exception("Escape must confirm exit.");f.Back();
            if(f.Select(-1)||f.Select(5)||f.Selection!=0)throw new Exception("Invalid hover selection.");
            foreach(var size in new[]{new[]{1280,720},new[]{1920,1080},new[]{2560,1440},new[]{3440,1440},new[]{1024,768},new[]{800,600}})
            {
                double scale=Math.Min(size[0]/1672d,size[1]/941d),ox=(size[0]-1672*scale)*.5,oy=(size[1]-941*scale)*.5;
                for(int i=0;i<5;i++)foreach(double x in new[]{94d,288d,482d})foreach(double y in new[]{339+i*71d,373+i*71d,407+i*71d})
                    if(OptionAt(ox+x*scale,size[1]-(oy+y*scale),size[0],size[1])!=i)throw new Exception("Full-row hover mapping.");
                foreach(var point in new[]{new[]{92d,373d},new[]{484d,373d},new[]{288d,337d},new[]{288d,408.5d},new[]{288d,693d}})
                    if(OptionAt(ox+point[0]*scale,size[1]-(oy+point[1]*scale),size[0],size[1])!=-1)throw new Exception("Hover outside fixed row.");
            }
            if(OptionAt(double.NaN,0,1920,1080)!=-1||OptionAt(0,0,0,0)!=-1)throw new Exception("Invalid hover coordinates.");
            foreach(int fps in new[]{30,60,120})
            {
                f=new WellnessMainMenuFlow();f.Select(2);float peak=1;
                for(int i=0;i<fps;i++)
                {
                    f.Tick(1d/fps);float before=f.SelectionScale;
                    if(f.Select(2)||f.SelectionScale!=before)throw new Exception("Stationary hover restarts animation.");
                    peak=Math.Max(peak,before);if(before<1||before>1.02501f)throw new Exception("Pop overshoot.");
                }
                if(peak<1.024f||Math.Abs(f.SelectionScale-1)>1e-6)throw new Exception("Pop must settle at all frame rates.");
                f.Move(1);if(f.Selection!=3)throw new Exception("Keyboard after mouse selection.");
                f.Enter();f.Open(Page.Settings);if(f.Current!=Page.Home||f.Select(1))throw new Exception("Transition input lock.");
                if(f.Tick(double.NaN)||f.Tick(-1)||f.Tick(0)||f.Fade!=0)throw new Exception("Invalid frame time.");
                int completions=0;for(int i=0;i<fps*2;i++)if(f.Tick(1d/fps))completions++;
                if(completions!=1||!f.Entered||f.Fade!=1)throw new Exception("Enter must complete exactly once.");
            }
            return "PASS: menu navigation, full-row hover at six resolutions, bounded 240 ms selection pop without hover retrigger, subpage isolation, exit confirmation, transition input lock, invalid time and exactly-once entry at 30/60/120 FPS.\n";
        }
    }
}
