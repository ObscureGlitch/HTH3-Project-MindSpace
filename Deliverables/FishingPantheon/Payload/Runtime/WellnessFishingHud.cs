using UnityEngine;
namespace TheLastWatch.UI
{
    public sealed partial class WellnessFishing
    {
        static readonly Color Mint=new Color(.65f,.94f,.77f),Gold=new Color(1,.82f,.43f);
        void Meter(Rect rect,float amount,Color tint)
        {ui.Outline(rect,new Color(.95f,1,.96f,.48f),rect.height*.5f);if(amount>0)ui.Rounded(new Rect(rect.x+2,rect.y+2,Mathf.Max(3,(rect.width-4)*Mathf.Clamp01(amount)),rect.height-4),tint,rect.height*.5f);}
        void DrawRound()
        {
            if(round.State==KoiFishingRound.Phase.Caught){DrawCatch();return;}
            if(round.State==KoiFishingRound.Phase.Casting||round.State==KoiFishingRound.Phase.Waiting||round.State==KoiFishingRound.Phase.Bite)
            {
                bool bite=round.State==KoiFishingRound.Phase.Bite;
                Label(new Rect(76,526,560,24),"MINDSPACE  /  THE KOI POND",eyebrow);
                Label(new Rect(76,559,620,46),bite?"A ripple. A bite. Your moment.":round.State==KoiFishingRound.Phase.Casting?"A little patience, a little wonder.":"Something is following your float…",ui.Body);
                if(bite){Meter(new Rect(78,612,430,9),round.BiteRemaining,Gold);Label(new Rect(78,634,550,27),"Space or click to hook  ·  React quickly for a clean hook",hint);}
                else Label(new Rect(78,612,570,28),"Watch the float  ·  Esc to return to the pond",hint);
                return;
            }
            if(round.State==KoiFishingRound.Phase.Reeling)
            {
                Color accent=KoiFishingLoot.ColorFor(hookedEntry.Rarity);
                Label(new Rect(475,99,430,24),"MINDSPACE  /  FOLLOW THE CURRENT",eyebrow);
                Label(new Rect(475,138,430,53),hookedEntry.Rank>=4?"A rare encounter":"Find your rhythm",title38);
                RarityLabel(new Rect(475,202,360,28),hookedEntry.Rarity);
                Label(new Rect(475,238,302,29),hookedEntry.Behavior,light);
                Label(new Rect(475,281,280,68),"Keep the koi inside the mint bar.\nHold Space or click to rise.\nRelease to drift down.",hint);
                if(round.PerfectHook)Label(new Rect(475,369,271,28),"CLEAN HOOK  + starting progress",eyebrow);
                Label(new Rect(475,411,281,29),round.Flow>.85f?"IN THE FLOW":round.Intent,light);
                Meter(new Rect(475,450,252,8),round.Flow,Mint);
                Label(new Rect(475,476,300,27),Mathf.RoundToInt(round.Accuracy*100)+"% tracking accuracy",hint);
                Rect track=new Rect(801,238,78,275);ui.Outline(track,new Color(.94f,1,.95f,.75f),20);
                Rect bar=new Rect(track.x+5,track.y+track.height*(1-round.Bar-KoiFishingRound.BarWidth),track.width-10,track.height*KoiFishingRound.BarWidth);
                ui.Rounded(bar,round.InBar?new Color(.44f,.86f,.66f,.82f):new Color(.65f,.78f,.69f,.52f),14);
                float y=track.y+track.height*(1-round.Fish);ui.Rounded(new Rect(track.x+20,y-7,33,14),accent,7);
                ui.Rounded(new Rect(track.x+45,y-10,11,20),accent,3);
                ui.Outline(new Rect(899,238,12,275),new Color(.95f,1,.97f,.45f),6);
                ui.Rounded(new Rect(901,511-271*round.Progress,8,Mathf.Max(3,271*round.Progress)),Mint,4);
                Label(new Rect(792,524,135,45),Mathf.RoundToInt(round.Progress*100)+"%",centered);
                Line(475,574,440);Label(new Rect(475,593,460,29),"No rush. Follow its movement.  ·  Esc to stop",hint);
                return;
            }
            Label(new Rect(77,469,540,54),"The pond keeps a little mystery.",title38);
            Label(new Rect(79,536,560,35),"This koi slipped away. Nothing in your collection is lost.",hint);
            if(Action(new Rect(78,595,237,49),"Cast again  ↵",true)){QueueRecast();return;}
            if(Action(new Rect(332,595,218,49),"Back to the pond")){CloseModal();return;}
        }
        void DrawCatch()
        {
            if(lastCatch==null)return;
            if(catchLiftTime<KoiFishingCatchMotion.Duration)
            {Label(new Rect(78,568,580,33),"A little treasure from the pond…",light);RarityLabel(new Rect(78,611,300,28),lastCatch.RarityName);return;}
            Label(new Rect(78,307,590,25),newDiscovery?"NEW DISCOVERY  /  YOUR JOURNAL IS GROWING":"ANOTHER STORY FROM THE POND",eyebrow);
            RarityLabel(new Rect(78,351,500,34),lastCatch.RarityName,24);
            Label(new Rect(76,397,580,60),FishName(lastCatch),bigNumber);
            Label(new Rect(79,469,590,29),KoiRarityVfx.Signature(lastCatch),light);
            Label(new Rect(79,510,550,29),lastCatch.lengthCm.ToString("0.0")+" cm  ·  "+lastCatch.weightKg.ToString("0.00")+" kg"+(personalBest?"  ·  PERSONAL BEST":""),light);
            Label(new Rect(79,548,590,30),(lastCatch.Perfect?"PERFECT CATCH  ·  ":"")+Mathf.RoundToInt(lastCatch.accuracy*100)+"% accuracy  ·  "+collection.Discoveries+" / "+Library.varieties.Length+" discovered",hint);
            if(Action(new Rect(78,595,211,48),"Cast again  ↵",true)){QueueRecast();return;}
            if(Action(new Rect(303,595,216,48),"View collection  I")){CloseModal();OpenInventory();return;}
            Label(new Rect(79,661,600,25),pendingCatch==null?"Saved locally  ·  "+sessionCatches+" catches this visit  ·  Esc to stop":"NOT SAVED YET  ·  Open I to retry before closing the game",eyebrow);
        }
        void DrawInventory()
        {
            Rect panel=new Rect(70,28,1140,664);
            if(Event.current.type==EventType.MouseDown&&Event.current.button==0&&!panel.Contains(Event.current.mousePosition)){CloseModal();Event.current.Use();return;}
            Label(new Rect(99,48,550,24),"MINDSPACE  /  SMALL WONDERS",eyebrow);
            Label(new Rect(95,81,725,56),"The koi collection",title38);
            Label(new Rect(99,141,760,28),collection.Discoveries+" / "+Library.varieties.Length+" species  ·  "+CatchCount+" caught  ·  "+collection.PerfectCatches+" perfect",hint);
            if(Action(new Rect(990,87,115,42),"Sound "+(fishingSounds?"on":"off")))fishingSounds=!fishingSounds;
            if(Action(new Rect(1120,87,62,42),"×")){CloseModal();return;}
            Line(99,184,1080);
            if(Action(new Rect(99,201,161,40),"Your catches",!journal)&&journal){journal=false;page=0;Select(visible.Count>0?visible[0]:-1);}
            if(Action(new Rect(272,201,157,40),"Species journal",journal)&&!journal){journal=true;page=journalSelected/12;PreviewJournal();}
            if(!journal)
            {
                if(Action(new Rect(445,201,147,40),filter<0?"All rarities":KoiFishingLoot.Tier(filter))){filter++;if(filter>5)filter=-1;RebuildVisible();Select(visible.Count>0?visible[0]:-1);}
                if(Action(new Rect(604,201,144,40),sort==0?"Newest":sort==1?"Rarest":"Largest")){sort=(sort+1)%3;RebuildVisible();Select(visible.Count>0?visible[0]:-1);}
                DrawCatches();
            }
            else DrawJournal();
            Label(new Rect(790,203,387,37),"Level "+collection.Level+"  ·  "+collection.Title,light);
            int levelStart=(collection.Level-1)*(collection.Level-1)*90,nextLevel=collection.Level*collection.Level*90;
            Meter(new Rect(790,236,385,5),(collection.Experience-levelStart)/(float)(nextLevel-levelStart),Mint);
            Line(99,601,1080);
            int size=journal?12:6,count=journal?Library.varieties.Length:visible.Count;
            if(page>0&&Action(new Rect(99,621,113,42),"Previous")){page--;if(journal){journalSelected=page*12;PreviewJournal();}else Select(visible[page*6]);return;}
            Label(new Rect(238,632,300,27),"Page "+(page+1)+" / "+Mathf.Max(1,Mathf.CeilToInt(count/(float)size)),hint);
            if((page+1)*size<count&&Action(new Rect(605,621,139,42),"Next")){page++;if(journal){journalSelected=page*12;PreviewJournal();}else Select(visible[page*6]);return;}
            if(pendingCatch!=null&&Action(new Rect(790,621,385,42),"Retry saving your catch",true)){TrySaveCatch();RebuildVisible();journal=false;Select(CatchCount>0?0:-1);}
            else Label(new Rect(791,627,388,39),focus==null?"No focus  ·  Every koi can be encountered":"Seeking "+KoiFishingLoot.DisplayName(focus),hint);
        }
        void DrawCatches()
        {
            if(visible.Count==0)Label(new Rect(104,302,615,93),CatchCount==0?"Your first koi is waiting.\nVisit the pond or bridge and cast with a click.":"No catches in this rarity yet.\nChoose another rarity or explore the journal.",wrapBody);
            int start=page*6,end=Mathf.Min(start+6,visible.Count);
            for(int slot=start;slot<end;slot++)
            {
                int index=visible[slot];var fish=inventory.Fish[index];int local=slot-start;Rect card=new Rect(99+(local%2)*329,264+(local/2)*107,316,94);
                var accent=KoiFishingLoot.ColorFor(fish.RarityName);ui.Outline(card,index==selected?accent:new Color(.94f,.98f,.93f,.28f),15);
                ui.Rounded(new Rect(card.x+10,card.y+13,3,68),accent,2);
                Label(new Rect(card.x+24,card.y+10,285,28),FishName(fish)+(fish.id==inventory.EquippedId?" · Held":""),light);
                RarityLabel(new Rect(card.x+24,card.y+40,279,24),fish.RarityName,13);
                Label(new Rect(card.x+24,card.y+65,279,23),fish.lengthCm.ToString("0.0")+" cm"+(fish.Perfect?"  ·  Perfect":""),hint);
                if(GUI.Button(card,GUIContent.none,ui.TextButton)){Select(index);return;}
            }
            if(selected<0||selected>=CatchCount)return;var koi=inventory.Fish[selected];
            PreviewImage();Label(new Rect(790,451,385,35),FishName(koi),ui.Body);RarityLabel(new Rect(791,491,378,28),koi.RarityName);
            Label(new Rect(790,525,385,26),koi.lengthCm.ToString("0.0")+" cm  ·  "+koi.weightKg.ToString("0.00")+" kg",hint);
            if(Action(new Rect(790,557,385,35),koi.id==inventory.EquippedId?"Put koi away":"Equip this koi",true))Equip(koi.id==inventory.EquippedId?null:koi.id);
        }
        void PreviewImage(){if(views.Preview!=null)GUI.DrawTexture(new Rect(782,253,400,192),views.Preview,ScaleMode.ScaleToFit);}
        void DrawJournal()
        {
            int start=page*12,end=Mathf.Min(start+12,Library.varieties.Length);
            for(int index=start;index<end;index++)
            {
                string variety=Library.varieties[index].name;var entry=KoiFishingLoot.ForVariety(variety);int local=index-start,n=collection.Count(variety);
                Rect card=new Rect(99+(local%4)*164,264+(local/4)*107,151,94);var accent=KoiFishingLoot.ColorFor(entry.Rarity);
                ui.Outline(card,journalSelected==index?accent:new Color(.95f,1,.96f,.25f),13);
                Label(new Rect(card.x+11,card.y+11,132,26),System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(variety),light);
                RarityLabel(new Rect(card.x+11,card.y+41,133,22),entry.Rarity,12);
                Label(new Rect(card.x+11,card.y+66,133,24),n>0?n+" caught":"Undiscovered",eyebrow);
                if(GUI.Button(card,GUIContent.none,ui.TextButton)){journalSelected=index;PreviewJournal();return;}
            }
            string name=Library.varieties[journalSelected].name;var item=KoiFishingLoot.ForVariety(name);PreviewImage();
            Label(new Rect(790,447,385,35),KoiFishingLoot.DisplayName(name),ui.Body);
            Label(new Rect(791,485,385,26),item.Signature+"  ·  "+item.Behavior,hint);
            float odds=KoiFishingLoot.Odds(Library,name,focus);
            Label(new Rect(791,514,385,27),"Chance "+(odds*100).ToString("0.00")+"%  ·  "+(collection.Count(name)==0?"Not yet caught":"Best "+collection.Best(name).ToString("0.0")+" cm"),hint);
            if(Action(new Rect(790,557,385,35),focus==name?"Clear focus":"Seek this koi  ·  free focus",true))focus=focus==name?null:name;
        }
    }
}
