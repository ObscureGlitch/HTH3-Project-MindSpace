from PIL import Image,ImageDraw
from pathlib import Path
p=Path(r'D:\Hack the Hill\Deliverables\Trailer\review')
f=sorted(p.glob('frame-*.jpg'))
for b in range(0,len(f),20):
 s=Image.new('RGB',(1920,1320),'#101820');d=ImageDraw.Draw(s)
 for j,x in enumerate(f[b:b+20]):
  im=Image.open(x);xx=j%4*480; yy=j//4*264;s.paste(im,(xx,yy));d.text((xx+8,yy+235),f'{(b+j)*5:03d}s',fill='white')
 s.save(p/f'sheet-{b//20}.jpg')
print(len(f))
