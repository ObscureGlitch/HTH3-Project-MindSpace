using System;

namespace TheLastWatch.Integrations
{
    public static class PresageColourPixels
    {
        // Unity GetPixels32 is bottom-up. Measurements get upright, top-down, unmirrored RGBA.
        public static void Normalize(byte[] source,byte[] frame,int sw,int sh,int angle,bool verticallyMirrored)
        {
            if(angle!=0&&angle!=90&&angle!=180&&angle!=270)throw new ArgumentException("Unsupported camera rotation.");
            int w=angle%180==0?sw:sh,h=angle%180==0?sh:sw;
            if(angle==0)
            {
                for(int y=0;y<h;y++)Buffer.BlockCopy(source,(verticallyMirrored?y:h-1-y)*w*4,frame,y*w*4,w*4);
                return;
            }
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                int sx=angle==90?y:angle==180?sw-1-x:sw-1-y;
                int sy=angle==90?sh-1-x:angle==180?sh-1-y:x;
                if(!verticallyMirrored)sy=sh-1-sy;
                int a=(sy*sw+sx)*4,b=(y*w+x)*4;
                frame[b]=source[a];frame[b+1]=source[a+1];frame[b+2]=source[a+2];frame[b+3]=255;
            }
        }

        // Only the UI self-view is mirrored. Preserve every RGB channel without recolouring.
        public static void Preview(byte[] frame,int width,int height,byte[] preview,int pw,int ph)
        {
            for(int y=0;y<ph;y++)for(int x=0;x<pw;x++)
            {
                int a=((y*height/ph)*width+x*width/pw)*4;
                int b=((ph-1-y)*pw+pw-1-x)*3;
                preview[b]=frame[a];preview[b+1]=frame[a+1];preview[b+2]=frame[a+2];
            }
        }
    }
}
