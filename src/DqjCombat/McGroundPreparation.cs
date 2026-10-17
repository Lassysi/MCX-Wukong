using System;

namespace DqjCombat
{
    // The MC collision consumer resolves height. The host accepts only a bounded
    // acknowledgement for this exact request; it never moves the world anchor.
    internal sealed class McGroundPreparation
    {
        internal readonly string Epoch;
        internal readonly BridgePoint Requested;
        internal BridgePoint Resolved {get;private set;}
        private readonly double started;
        internal McGroundPreparation(string epoch,BridgePoint requested,double now)
        {Epoch=epoch;Requested=Resolved=requested;started=now;}
        internal bool Expired(double now){return now-started>=8;}
        internal bool Ready(string epoch,bool ready,BridgePoint declared,BridgePoint actual,bool anchoredSupport=false)
        {
            if(epoch!=Epoch||!ready||!declared.Finite||!actual.Finite
                // Preview99: MC may place the player up to 1.5 m aside when MC blocks fill the spawn column.
                ||Math.Sqrt((declared.X-Requested.X)*(declared.X-Requested.X)+(declared.Z-Requested.Z)*(declared.Z-Requested.Z))>1.6
                ||declared.Y-Requested.Y>(anchoredSupport?2.00001:1.26001)||Requested.Y-declared.Y>.60001||declared.Y< -50||declared.Y>300
                ||Math.Abs(actual.X-declared.X)>.15||Math.Abs(actual.Z-declared.Z)>.15||Math.Abs(actual.Y-declared.Y)>.1)return false;
            Resolved=declared;return true;
        }
    }
}
