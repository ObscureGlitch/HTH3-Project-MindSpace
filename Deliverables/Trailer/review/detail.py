from pathlib import Path
import subprocess, numpy as np
from PIL import Image,ImageDraw
p=Path(r'D:\Hack the Hill\Deliverables\Trailer'); ff=str(p/'tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe')
src=r'C:\Users\obscu\Videos\Captures\HTH3 Project - TherapyRoom - Windows, Mac, Linux - Unity 6.6 (6000.6.3f1) _DX11_ 2026-09-27 16-55-54.mp4'
for start in [96,123,139,172,181,223,229,280]:
 data=subprocess.check_output([ff,'-v','error','-ss',str(start),'-i',src,'-t','7','-vf','crop=1380:776:260:152,scale=320:180,fps=10','-pix_fmt','rgb24','-f','rawvideo','-an','-'])
 a=np.frombuffer(data,np.uint8).reshape(-1,180,320,3)
 s=Image.new('RGB',(1280,408),'#101820'); d=ImageDraw.Draw(s)
 for j in range(7):
  xx=j%4*320; yy=j//4*204;s.paste(Image.fromarray(a[min(j*10,len(a)-1)]),(xx,yy));d.text((xx+8,yy+182),str(start+j)+'s',fill='white')
 s.save(p/f'review/detail-{start}.jpg')
 dif=np.abs(np.diff(a.astype(np.float32),axis=0)).mean(axis=(1,2,3))
 print(start, 'low-motion frames',round(float((dif<.6).mean()),2),'median delta',round(float(np.median(dif)),2),flush=True)
