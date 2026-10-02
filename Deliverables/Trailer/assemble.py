from pathlib import Path
from PIL import Image
import subprocess,shutil
p=Path(r'D:\Hack the Hill\Deliverables\Trailer');ff=str(p/'tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe')
src=r'C:\Users\obscu\Videos\Captures\HTH3 Project - TherapyRoom - Windows, Mac, Linux - Unity 6.6 (6000.6.3f1) _DX11_ 2026-09-27 16-55-54.mp4'
shutil.copyfile(r'C:\Windows\Fonts\segoeui.ttf',p/'review/font.ttf')
shutil.copyfile(r'C:\Windows\Fonts\segoeuib.ttf',p/'review/font-bold.ttf')
im=Image.new('RGBA',(1280,720))
for y in range(500,720):
 alpha=int(205*((y-500)/219)**.7)
 im.paste((7,18,18,alpha),(0,y,1280,y+1))
im.save(p/'review/gradient.png')
cmd=[ff,'-y','-hide_banner','-loglevel','error','-filter_complex_threads','2']
for i in range(6):cmd+=['-i',str(p/f'review/shot-{i}.mp4')]
cmd+=['-ss','223.3','-i',src,'-loop','1','-i',str(p/'review/gradient.png')]
f=[]
for i in range(6):f.append(f'[{i}:v]setpts=PTS-STARTPTS,fps=30,settb=1/30[v{i}]')
prev='v0'
for i,offset in enumerate([5,10,15,18,22],1):
 f.append(f'[{prev}][v{i}]xfade=transition=fade:duration=0.3:offset={offset},fps=30[x{i}]');prev=f'x{i}'
f.append(f'[{prev}][7:v]overlay=0:0:shortest=1[base]')
texts=[(0.3,4.7,'MindSpace',52,583,True),(0.3,4.7,'A little room to reconnect.',25,650,False),(5.4,9.7,'Find your space.',36,632,False),(10.4,14.7,'Talk with an AI companion.',36,632,False),(15.4,17.8,'Choose your companion.',36,632,False),(18.4,21.8,'Explore at your own pace.',36,632,False),(23.5,29.5,'MindSpace',52,583,True),(23.5,29.5,'Take a moment for yourself.',25,650,False)]
prev='base'
for i,(a,b,txt,size,y,bold) in enumerate(texts):
 font='review/font-bold.ttf' if bold else 'review/font.ttf'
 alpha=f"if(lt(t,{a}),0,if(lt(t,{a+.3}),(t-{a})/.3,if(lt(t,{b-.3}),1,if(lt(t,{b}),({b}-t)/.3,0))))"
 f.append(f"[{prev}]drawtext=fontfile={font}:text='{txt}':fontsize={size}:fontcolor=0xF6F5EA:x=52:y={y}:alpha='{alpha}'[t{i}]");prev=f't{i}'
f.append(f'[{prev}]fade=t=in:st=0:d=0.25,fade=t=out:st=29.5:d=0.5,format=yuv420p[outv]')
f.append('[6:a]atrim=duration=30,asetpts=PTS-STARTPTS,afade=t=in:d=0.7,afade=t=out:st=28.5:d=1.5,alimiter=limit=0.95[outa]')
(p/'review/final-filter.txt').write_text(';\n'.join(f))
cmd+=['-filter_complex_script',str(p/'review/final-filter.txt'),'-map','[outv]','-map','[outa]','-t','30','-r','30','-c:v','libx264','-preset','slow','-crf','18','-threads','4','-c:a','aac','-b:a','192k','-movflags','+faststart','-metadata','title=MindSpace | 30 Second Gameplay Trailer',str(p/'MindSpace-30s-Trailer.mp4')]
subprocess.run(cmd,cwd=p,check=True)
print('TRAILER_COMPLETE',flush=True)


