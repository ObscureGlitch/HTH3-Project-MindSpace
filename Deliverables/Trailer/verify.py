from pathlib import Path
import subprocess,numpy as np
from PIL import Image,ImageDraw
p=Path(r'D:\Hack the Hill\Deliverables\Trailer');ff=str(p/'tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe');v=p/'MindSpace-30s-Trailer.mp4'
r=subprocess.run([ff,'-hide_banner','-i',str(v)],capture_output=True,text=True);print(r.stderr)
raw=subprocess.check_output([ff,'-v','error','-i',str(v),'-vf','fps=1,scale=384:216','-an','-pix_fmt','rgb24','-f','rawvideo','-'])
a=np.frombuffer(raw,np.uint8).reshape(-1,216,384,3)
s=Image.new('RGB',(1920,1452),'#101820');d=ImageDraw.Draw(s)
for i,frame in enumerate(a):
 x=i%5*384;y=i//5*242;s.paste(Image.fromarray(frame),(x,y));d.text((x+8,y+219),f'{i+0.5:.1f}s',fill='white')
s.save(p/'review/final-contact.jpg')
subprocess.run([ff,'-v','error','-i',str(v),'-f','null','-'],check=True)
print('Decode OK; preview samples:',len(a),'File bytes:',v.stat().st_size)
