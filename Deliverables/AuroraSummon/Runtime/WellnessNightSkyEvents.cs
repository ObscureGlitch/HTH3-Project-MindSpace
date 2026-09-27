using System;

namespace TheLastWatch.Environment
{
    /// <summary>Bounded, allocation-free night choreography in real seconds. No Unity,
    /// particles, physics, audio or wall-clock catch-up; also testable without the editor.</summary>
    public sealed class WellnessNightSkyEvents
    {
        public const int Capacity = 3;
        public const float AuroraNightChance = .45f;
        public const float SummonedAuroraDuration = 120;
        public struct Direction
        {
            public float x, y, z;
            public Direction(float x, float y, float z) { this.x=x; this.y=y; this.z=z; }
            public static Direction operator +(Direction a, Direction b) => new Direction(a.x+b.x,a.y+b.y,a.z+b.z);
            public static Direction operator *(Direction a, float b) => new Direction(a.x*b,a.y*b,a.z*b);
            public static Direction Cross(Direction a, Direction b) => new Direction(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
        }
        public struct Meteor
        {
            public Direction head, tangent, side;
            public float brightness, tail, width;
        }
        private struct Flight
        {
            public Direction origin, tangent;
            public double start;
            public float duration, travel, tail, width, brightness;
        }
        private struct AuroraComposition
        {
            public float azimuth, tilt, spread, palette, phase;
        }
        private readonly Random random, auroraRandom, summonRandom;
        private AuroraComposition naturalAurora, summonedAurora;
        private double summonStart;
        private bool summonActive;
        private float summonInitialStrength;
        private readonly Flight[] flights = new Flight[Capacity];
        private double clock, nextMeteor, auroraStart, auroraDuration, nextAurora;
        private bool meteorRunning, nightActive;
        public int EventCount { get; private set; }
        public int GroupCount { get; private set; }
        public int TripleCount { get; private set; }
        public float Seconds => (float)clock;
        private AuroraComposition CurrentAurora => summonActive ? summonedAurora : naturalAurora;
        public float AuroraAzimuth => CurrentAurora.azimuth;
        public float AuroraStrength { get; private set; }
        public float AuroraTilt => CurrentAurora.tilt;
        public float AuroraSpread => CurrentAurora.spread;
        public float AuroraPalette => CurrentAurora.palette;
        public float AuroraPhase => CurrentAurora.phase;
        public bool AuroraSummoned => summonActive;
        public float SummonSecondsRemaining => summonActive ? (float)Math.Max(0,SummonedAuroraDuration-(clock-summonStart)) : 0;
        public bool AuroraThisNight { get; private set; }
        public int NightCount { get; private set; }
        public int AuroraNightCount { get; private set; }
        public int AuroraDisplayCount { get; private set; }

        public WellnessNightSkyEvents(int seed) { random=new Random(seed);auroraRandom=new Random(seed^0x197A3);summonRandom=new Random(seed^0x4A913); }
        private float Range(float min, float max) => min+(max-min)*(float)random.NextDouble();
        private float AuroraRange(float min, float max) => min+(max-min)*(float)auroraRandom.NextDouble();
        private static float Smooth(float value) { value=Math.Max(0,Math.Min(1,value));return value*value*(3-2*value); }

        // One bounded display, independent of the natural lottery and meteor RNG.
        // Repeated clicks do not reposition or restart an already-visible curtain.
        public bool SummonAurora(float viewerYaw)
        {
            if(!nightActive||summonActive)return false;
            if(float.IsNaN(viewerYaw)||float.IsInfinity(viewerYaw))viewerYaw=0;
            summonInitialStrength=AuroraStrength;
            if(AuroraStrength>.001f)summonedAurora=naturalAurora;
            else
            {
                float turn=(float)Math.PI*2;
                float azimuth=(viewerYaw+((float)summonRandom.NextDouble()-.5f)*.8f)%turn;
                summonedAurora=new AuroraComposition {
                    azimuth=azimuth<0?azimuth+turn:azimuth,
                    tilt=.6f+(float)summonRandom.NextDouble()*.35f,
                    spread=1.15f+(float)summonRandom.NextDouble()*.25f,
                    palette=(float)summonRandom.NextDouble(),phase=(float)summonRandom.NextDouble()*1000
                };
            }
            summonStart=clock;summonActive=true;
            return true;
        }

        public void Tick(float seconds, bool visibleNight, bool meteors, bool aurora, float viewerYaw)
            => Tick(seconds, visibleNight, visibleNight, meteors, aurora, viewerYaw);

        public void Tick(float seconds, bool isNight, bool visibleNight, bool meteors, bool aurora, float viewerYaw)
        {
            // Long background/editor stalls never queue missed showers on return.
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<0)return;
            clock+=Math.Min(seconds,.25f);
            if(float.IsNaN(viewerYaw)||float.IsInfinity(viewerYaw))viewerYaw=0;
            bool runMeteors=isNight&&visibleNight&&meteors;
            if(runMeteors&&!meteorRunning)nextMeteor=clock+Range(5,12);
            if(!runMeteors&&meteorRunning)Array.Clear(flights,0,Capacity);
            meteorRunning=runMeteors;
            if(runMeteors&&clock>=nextMeteor)
            {
                Spawn(nextMeteor,viewerYaw);
                nextMeteor+=Range(18,40);
            }
            // One roll at astronomical dusk, independent of cloud visibility,
            // comfort toggles or the viewer. A clearing storm is not a new night.
            if(isNight&&!nightActive)
            {
                NightCount++;
                AuroraThisNight=auroraRandom.NextDouble()<AuroraNightChance;
                if(AuroraThisNight)AuroraNightCount++;
                nextAurora=clock+AuroraRange(12,30);auroraDuration=0;
            }
            nightActive=isNight;
            AuroraStrength=0;
            if(!isNight){auroraDuration=0;AuroraThisNight=false;summonActive=false;return;}
            if(AuroraThisNight&&clock>=nextAurora)
            {
                auroraStart=nextAurora;auroraDuration=AuroraRange(95,155);
                nextAurora=auroraStart+auroraDuration+AuroraRange(50,95);
                AuroraDisplayCount++;
                // New world-space composition only while fully faded out. Most
                // displays arch overhead; some stay closer to the distant hills.
                naturalAurora.azimuth=AuroraRange(0,(float)Math.PI*2);
                naturalAurora.tilt=AuroraRange(0,1)<.7f?AuroraRange(.55f,1.04f):AuroraRange(.12f,.42f);
                naturalAurora.spread=AuroraRange(.95f,1.40f);
                naturalAurora.palette=AuroraRange(0,1);
                naturalAurora.phase=AuroraRange(0,1000);
            }
            double age=clock-auroraStart;
            if(visibleNight&&aurora&&AuroraThisNight&&auroraDuration>0&&age>=0&&age<auroraDuration)
                AuroraStrength=Smooth((float)age/22)*Smooth((float)(auroraDuration-age)/28);
            if(summonActive)
            {
                double summonAge=clock-summonStart;
                if(!aurora||summonAge>=SummonedAuroraDuration)
                {
                    summonActive=false;AuroraStrength=0;
                    // End in a quiet gap instead of snapping to a differently positioned
                    // natural display that may have started behind the manual occurrence.
                    auroraDuration=0;
                }
                else
                {
                    float fadeIn=Smooth((float)summonAge/6);
                    AuroraStrength=visibleNight ? (summonInitialStrength+(1-summonInitialStrength)*fadeIn)
                        *Smooth((float)(SummonedAuroraDuration-summonAge)/20) : 0;
                }
            }
        }

        private void Spawn(double start, float viewerYaw)
        {
            float chance=Range(0,1);int count=chance<.08f?3:chance<.33f?2:1;
            EventCount++;if(count>1)GroupCount++;if(count==3)TripleCount++;
            // A little more likely in the current vista, with genuine world-space paths.
            float azimuth=viewerYaw+Range(-1.25f,1.25f), elevation=Range(.56f,1.04f);
            float lateral=Range(0,1)<.5f?-1:1;
            for(int i=0;i<Capacity;i++)
            {
                if(i>=count){flights[i]=default(Flight);continue;}
                float az=azimuth+Range(-.10f,.10f), el=elevation+Range(-.04f,.04f);
                float sa=(float)Math.Sin(az),ca=(float)Math.Cos(az),se=(float)Math.Sin(el),ce=(float)Math.Cos(el);
                var origin=new Direction(sa*ce,se,ca*ce);
                var right=new Direction(ca,0,-sa);
                var up=new Direction(-sa*se,ce,-ca*se);
                float slope=Range(.28f,.52f);
                flights[i]=new Flight {
                    origin=origin,tangent=right*(lateral*(float)Math.Cos(slope))+up*(-(float)Math.Sin(slope)),
                    start=start+i*Range(.28f,.44f),duration=Range(1.25f,1.95f),travel=Range(.34f,.56f),
                    tail=Range(.11f,.19f),width=Range(.00060f,.0010f),brightness=Range(1.05f,1.5f)
                };
            }
        }
        public Meteor Sample(int index)
        {
            if(index<0||index>=Capacity)throw new ArgumentOutOfRangeException(nameof(index));
            Flight f=flights[index];double age=clock-f.start;
            if(!meteorRunning||f.duration<=0||age<=0||age>=f.duration)return default(Meteor);
            float progress=(float)(age/f.duration),angle=f.travel*progress;
            float s=(float)Math.Sin(angle),c=(float)Math.Cos(angle);
            var head=f.origin*c+f.tangent*s;var tangent=f.origin*(-s)+f.tangent*c;
            return new Meteor {
                head=head,tangent=tangent,side=Direction.Cross(head,tangent),
                brightness=f.brightness*Smooth((float)age/.15f)*Smooth((float)(f.duration-age)/.45f),
                tail=f.tail*Smooth((float)age/.26f),width=f.width
            };
        }
    }
}
