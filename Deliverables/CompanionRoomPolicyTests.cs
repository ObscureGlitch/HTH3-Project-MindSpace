using System;
using TheLastWatch.Integrations;

public static class CompanionRoomPolicyTests
{
    private static int assertions;
    private static void Check(bool value,string message)
    {assertions++;if(!value)throw new Exception(message);}
    public static void Main()
    {
        var policy=new WellnessCompanionRoomPolicy();
        Check(!policy.CanLeave(true),"Must start waiting, even with an open door.");
        policy.Observe(true,true,60);
        Check(policy.OutsideSeconds==0&&!policy.CanLeave(true),"Time inside cannot unlock roaming.");
        policy.Observe(false,true,4.9f);
        Check(!policy.CanLeave(true),"Must wait more than five seconds.");
        policy.Reset();policy.Observe(false,true,5);
        Check(!policy.CanLeave(true),"Exactly five seconds is not more than five seconds.");
        policy.Observe(false,true,.01f);
        Check(policy.CanLeave(true),"May depart after five seconds through an open door.");
        Check(!policy.CanLeave(false),"Closed door must override elapsed time and follow intent.");
        Check(policy.CanLeave(true),"Reopening allows departure after the player has waited outside.");
        policy.Observe(true,false,0);
        Check(policy.OutsideSeconds==0&&!policy.CanLeave(true),"Returning inside resets the timer even while paused.");
        policy.Observe(false,true,3);policy.Observe(true,true,0);policy.Observe(false,true,3);
        Check(!policy.CanLeave(true),"Separate short trips must not accumulate.");
        policy.Observe(false,false,100);
        Check(policy.OutsideSeconds==3,"Menus and lost focus must not count as waiting outside.");
        policy.Observe(false,true,-1);policy.Observe(false,true,float.NaN);policy.Observe(false,true,float.PositiveInfinity);
        Check(policy.OutsideSeconds==3,"Invalid time steps must not alter the timer.");
        policy.Observe(false,true,2);Check(!policy.CanLeave(true),"Still waits at the exact threshold after resuming.");
        policy.Observe(false,true,.1f);Check(policy.CanLeave(true),"Normal gameplay time resumes the countdown.");
        policy.Reset();Check(!policy.CanLeave(true),"New selection/play session starts indoors again.");
        Check(!WellnessCompanionRoomPolicy.DoorAllowsPassage(false,0),"Closed door blocks passage.");
        Check(!WellnessCompanionRoomPolicy.DoorAllowsPassage(false,105),"Close intent blocks immediately, even before the door swings.");
        Check(!WellnessCompanionRoomPolicy.DoorAllowsPassage(true,20),"Door must physically open far enough.");
        Check(WellnessCompanionRoomPolicy.DoorAllowsPassage(true,65),"Open doorway permits crossing.");
        Check(WellnessCompanionRoomPolicy.ShouldWelcome(true,true,true),"Initial indoor voice consent should request the opening conversation.");
        Check(!WellnessCompanionRoomPolicy.ShouldWelcome(false,true,true),"No greeting session without voice consent.");
        Check(!WellnessCompanionRoomPolicy.ShouldWelcome(true,false,true),"No automatic repeated opening conversation.");
        Check(!WellnessCompanionRoomPolicy.ShouldWelcome(true,true,false),"No automatic welcome session outside.");
        Console.WriteLine("PASS: "+assertions+" room-start, strict five-second, re-entry, paused-time, door and consent policy assertions.");
    }
}
