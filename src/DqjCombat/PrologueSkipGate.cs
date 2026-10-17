namespace DqjCombat
{
    // One native transaction per world. A missing/rejected reply must never cause a second request.
    internal sealed class PrologueSkipGate
    {
        internal bool Requested { get; private set; }
        internal bool TravelStarted { get; private set; }
        internal bool Failed { get; private set; }
        internal bool CompletionReady { get { return ready&&!Failed&&!TravelStarted; } }
        private bool replied,ready;
        private double requestedAt;
        internal static bool Eligible(int currentMap,int prologueMap,int targetMap,int currentChapter,int prologueChapter)
        {return prologueMap>0&&targetMap>0&&targetMap!=prologueMap&&currentMap==prologueMap&&currentChapter==prologueChapter;}
        internal bool Begin(double now)
        {if(Requested||Failed)return false;Requested=true;requestedAt=now;return true;}
        internal void Reply(bool success)
        {if(!Requested||replied||Failed||TravelStarted)return;replied=true;ready=success;if(!success)Failed=true;}
        internal bool TakeCompletion()
        {if(!ready||Failed||TravelStarted)return false;ready=false;TravelStarted=true;return true;}
        internal bool Timeout(double now)
        {if(!Requested||replied||Failed||now-requestedAt<30)return false;Failed=true;return true;}
        internal void Fail(){Failed=true;ready=false;}
    }
}
