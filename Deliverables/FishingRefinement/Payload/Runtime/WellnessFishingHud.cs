using UnityEngine;
namespace TheLastWatch.UI
{
    public sealed partial class WellnessFishing
    {
        static readonly Color Mint=new Color(.65f,.94f,.77f),Gold=new Color(1,.82f,.43f);
        void Meter(Rect r,float amount,Color tint)
        {Fill(r,new Color(.20f,.29f,.29f),r.height*.5f);if(amount>0)Fill(new Rect(r.x,r.y,Mathf.Max(3,r.width*Mathf.Clamp01(amount)),r.height),tint,r.height*.5f);}
        void DrawRound()
        {
            if(round.State==KoiFishingRound.Phase.Caught){DrawCatch();return;}
            if(round.State==KoiFishingRound.Phase.Casting||round.State==KoiFishingRound.Phase.Waiting||round.State==KoiFishingRound.Phase.Bite)
            {
                bool bite=round.State==KoiFishingRound.Phase.Bite;Rect pill=new Rect(440,578,400,bite?79:55);Fill(pill,Panel,16);Outline(pill,bite?Gold:new Color(.28f,.4f,.37f),16);
                Label(new Rect(453,589,374,30),bite?"Bite!   Space / Click":round.State==KoiFishingRound.Phase.Casting?"Casting…":"Waiting for a bite…",centered);
                if(bite)Meter(new Rect(462,633,356,6),round.BiteRemaining,Gold);return;
            }
            if(round.State==KoiFishingRound.Phase.Reeling)
            {
                Color accent=KoiFishingLoot.ColorFor(hookedEntry.Rarity);Rect panel=new Rect(1021,185,185,393);Fill(panel,Panel,18);Outline(panel,new Color(.29f,.41f,.39f),18);
                Label(new Rect(1041,203,145,27),"REEL",centered);RarityLabel(new Rect(1040,237,145,24),hookedEntry.Rarity,13);
                Rect track=new Rect(1054,277,70,225);Fill(track,new Color(.035f,.075f,.085f),15);Outline(track,new Color(.32f,.46f,.43f),15);
                Rect bar=new Rect(track.x+5,track.y+track.height*(1-round.Bar-KoiFishingRound.BarWidth),track.width-10,track.height*KoiFishingRound.BarWidth);
                Fill(bar,round.InBar?new Color(.36f,.72f,.54f):new Color(.27f,.43f,.38f),10);
                float y=track.y+track.height*(1-round.Fish);Fill(new Rect(track.x+16,y-6,31,12),accent,6);Fill(new Rect(track.x+42,y-9,10,18),accent,3);
                Fill(new Rect(1141,277,12,225),new Color(.20f,.29f,.29f),6);Fill(new Rect(1143,500-221*round.Progress,8,Mathf.Max(3,221*round.Progress)),Mint,4);
                Label(new Rect(1038,515,151,28),"Space / Click",centered);Label(new Rect(1040,547,145,20),"Esc  Cancel",ui.Hint);return;
            }
            Fill(new Rect(400,485,480,169),Panel,18);Label(new Rect(422,506,436,34),"The koi slipped away",ui.Body);
            if(Action(new Rect(422,572,211,48),"Cast again  ↵",true)){QueueRecast();return;}
            if(Action(new Rect(646,572,212,48),"Back to pond")){CloseModal();return;}
        }
        void DrawCatch()
        {
            if(lastCatch==null||catchLiftTime<KoiFishingCatchMotion.Duration)return;
            float arrival=Mathf.Clamp01((catchLiftTime-KoiFishingCatchMotion.Duration)/.25f),slide=16*(1-arrival)*(1-arrival);
            Rect panel=new Rect(73,364+slide,497,301);Fill(panel,Panel,20);Outline(panel,KoiFishingLoot.ColorFor(lastCatch.RarityName),20,2);
            Label(new Rect(96,384+slide,449,23),newDiscovery?"NEW SPECIES":"CAUGHT!",eyebrow);RarityLabel(new Rect(96,414+slide,449,26),lastCatch.RarityName,17);
            Label(new Rect(94,449+slide,451,49),FishName(lastCatch),bigNumber);
            Label(new Rect(96,507+slide,449,28),lastCatch.lengthCm.ToString("0.0")+" cm  ·  "+lastCatch.weightKg.ToString("0.00")+" kg",light);
            string badge=lastCatch.Perfect?"Perfect catch":personalBest?"Personal best":"Added to collection";
            Label(new Rect(96,544+slide,449,23),pendingCatch!=null?"Save pending · Retry in collection":badge,ui.Hint);
            if(Action(new Rect(96,595+slide,215,47),"Cast again  ↵",true)){QueueRecast();return;}
            if(Action(new Rect(326,595+slide,219,47),"Collection  I")){CloseModal();OpenInventory();return;}
        }
        void DrawInventory()
        {
            Rect panel=new Rect(70,28,1140,664);
            if(paintCapture==null&&Event.current.type==EventType.MouseDown&&Event.current.button==0)
            {
                if(filterMenuOpen&&!new Rect(439,172,183,341).Contains(Event.current.mousePosition)){filterMenuOpen=false;Event.current.Use();}
                else if(!filterMenuOpen&&!panel.Contains(Event.current.mousePosition)){CloseModal();Event.current.Use();return;}
            }
            Fill(panel,Panel,22);Outline(panel,new Color(.25f,.36f,.34f),22);Label(new Rect(98,48,700,22),"MINDSPACE   /   KOI COLLECTION",eyebrow);
            Label(new Rect(96,79,700,47),"Treasures from the pond",title38);
            Label(new Rect(99,124,745,25),CatchCount+" fish  ·  "+collection.Discoveries+" / "+Library.varieties.Length+" species  ·  Level "+collection.Level,hint);
            if(Action(new Rect(981,74,133,43),"Sound "+(fishingSounds?"on":"off")))fishingSounds=!fishingSounds;
            if(Action(new Rect(1128,74,53,43),"×")){CloseModal();return;}
            Line(99,154,1080);
            if(Action(new Rect(99,172,158,40),"Your catches",!journal)&&journal){journal=false;page=0;Select(visible.Count>0?visible[0]:-1);}
            if(Action(new Rect(270,172,156,40),"Species journal",journal)&&!journal){journal=true;page=0;journalSelected=0;filterMenuOpen=false;PreviewJournal();}
            if(!journal)
            {
                if(Action(new Rect(439,172,183,40),filter<0?"All fish  ▾":KoiFishingLoot.Tier(filter)+" only  ▾"))filterMenuOpen=!filterMenuOpen;
                if(Action(new Rect(634,172,113,40),sort==0?"Newest":sort==1?"Rarest":"Largest")){sort=(sort+1)%3;RebuildVisible();Select(visible.Count>0?visible[0]:-1);}
                DrawCatches();
            }
            else DrawJournal();
            Line(99,594,1080);int size=journal?12:6,count=journal?Library.varieties.Length:visible.Count;
            int first=count==0?0:page*size+1,last=Mathf.Min((page+1)*size,count);string noun=journal?"species":"fish";
            Label(new Rect(99,606,400,22),"Showing "+first+"–"+last+" of "+count+" "+noun+(filter>=0&&!journal?"  ·  "+CatchCount+" total":""),hint);
            if(page>0&&Action(new Rect(99,638,112,34),"Previous")){page--;if(journal){journalSelected=page*12;PreviewJournal();}else Select(visible[page*6]);return;}
            Label(new Rect(226,642,193,28),"Page "+(page+1)+" / "+Mathf.Max(1,Mathf.CeilToInt(count/(float)size)),centered);
            if((page+1)*size<count&&Action(new Rect(434,638,112,34),"Next")){page++;if(journal){journalSelected=page*12;PreviewJournal();}else Select(visible[page*6]);return;}
            if(filter>=0&&!journal&&Action(new Rect(559,638,188,34),"Show all fish")){SetFilter(-1);return;}
            if(pendingCatch!=null||inventory.PendingCount>0)
            {
                Label(new Rect(790,605,386,23),"Save pending · Keep the game open",hint);
                if(Action(new Rect(790,638,387,34),"Retry save",true)){TrySaveCatch();RefreshInventory();ResetCollectionView();Select(CatchCount>0?0:-1);}
            }
            else if(inventory.ReadIssueCount>0)Label(new Rect(790,608,386,60),"Some files need attention. Valid catches are retained; save files have not been deleted.",hint);
            else Label(new Rect(790,612,386,49),focus==null?"Saved locally  ·  Esc to close":"Seeking "+KoiFishingLoot.DisplayName(focus),hint);
            DrawFilter();
        }
        void DrawCatches()
        {
            if(visible.Count==0)
            {Fill(new Rect(99,238,648,338),Card,16);Label(new Rect(121,326,604,68),CatchCount==0?"Your first koi is waiting.\nCast at the pond or bridge.":"No fish match this filter.\nYour other catches are still saved.",wrapBody);}
            int start=page*6,end=Mathf.Min(start+6,visible.Count);
            for(int slot=start;slot<end;slot++)
            {
                int index=visible[slot];var fish=inventory.Fish[index];int local=slot-start;Rect card=new Rect(99+(local%2)*329,238+(local/2)*114,316,106);
                var accent=KoiFishingLoot.ColorFor(fish.RarityName);Fill(card,index==selected?new Color(.18f,.29f,.27f):Card,14);Outline(card,index==selected?accent:new Color(.25f,.34f,.33f),14,index==selected?2:1);
                Fill(new Rect(card.x+13,card.y+16,4,74),accent,2);Label(new Rect(card.x+28,card.y+10,272,30),FishName(fish),light);
                RarityLabel(new Rect(card.x+28,card.y+44,272,24),fish.RarityName,13);
                Label(new Rect(card.x+28,card.y+73,272,23),fish.lengthCm.ToString("0.0")+" cm"+(fish.id==inventory.EquippedId?"  ·  Equipped":fish.Perfect?"  ·  Perfect":""),hint);
                if(Hit(card)){Select(index);return;}
            }
            if(selected<0||selected>=CatchCount)return;var koi=inventory.Fish[selected];
            PreviewImage();Label(new Rect(805,442,357,34),FishName(koi),ui.Body);RarityLabel(new Rect(805,482,357,26),koi.RarityName);
            Label(new Rect(805,515,357,25),koi.lengthCm.ToString("0.0")+" cm  ·  "+koi.weightKg.ToString("0.00")+" kg",hint);
            if(Action(new Rect(805,552,357,32),koi.id==inventory.EquippedId?"Put koi away":"Equip koi",true))Equip(koi.id==inventory.EquippedId?null:koi.id);
        }
        void PreviewImage()
        {Fill(new Rect(790,238,387,346),Card,16);Fill(new Rect(804,252,359,178),new Color(.055f,.10f,.12f),12);Image(new Rect(804,252,359,178),views?.Preview);}
        void DrawJournal()
        {
            int start=page*12,end=Mathf.Min(start+12,Library.varieties.Length);
            for(int index=start;index<end;index++)
            {
                string variety=Library.varieties[index].name;var entry=KoiFishingLoot.ForVariety(variety);int local=index-start,n=collection.Count(variety);
                Rect card=new Rect(99+(local%4)*164,238+(local/4)*114,151,106);var accent=KoiFishingLoot.ColorFor(entry.Rarity);
                Fill(card,journalSelected==index?new Color(.18f,.29f,.27f):Card,13);Outline(card,journalSelected==index?accent:new Color(.25f,.34f,.33f),13);
                Label(new Rect(card.x+11,card.y+11,131,26),System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(variety),light);
                RarityLabel(new Rect(card.x+11,card.y+45,131,22),entry.Rarity,12);Label(new Rect(card.x+11,card.y+75,131,23),n>0?n+" caught":"Undiscovered",eyebrow);
                if(Hit(card)){journalSelected=index;PreviewJournal();return;}
            }
            string name=Library.varieties[journalSelected].name;var item=KoiFishingLoot.ForVariety(name);PreviewImage();
            Label(new Rect(805,440,357,32),KoiFishingLoot.DisplayName(name),ui.Body);RarityLabel(new Rect(805,475,357,24),item.Rarity,14);
            float odds=KoiFishingLoot.Odds(Library,name,focus);Label(new Rect(805,504,357,37),"Chance "+(odds*100).ToString("0.00")+"%  ·  "+(collection.Count(name)==0?"Not yet caught":"Best "+collection.Best(name).ToString("0.0")+" cm"),hint);
            if(Action(new Rect(805,552,357,32),focus==name?"Clear focus":"Seek this koi",true))focus=focus==name?null:name;
        }
    }
}
