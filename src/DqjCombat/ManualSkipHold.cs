using System;

namespace DqjCombat
{
    // One deliberate hold per press in one movie/cut. Carrying E from inventory or a previous cut
    // cannot skip the next scene; focus loss and release cancel incomplete intent.
    internal sealed class ManualSkipHold
    {
        private object movie;
        private int sequence;
        private bool armed,previousDown,fired;
        private double started=double.NaN;
        internal bool Update(object current,int currentSequence,bool focused,bool down,double now,double seconds)
        {
            if(!ReferenceEquals(movie,current)||sequence!=currentSequence){
                movie=current;sequence=currentSequence;armed=focused&&!down;previousDown=down;fired=false;started=double.NaN;
                return false;
            }
            if(current==null||!focused){armed=false;previousDown=down;started=double.NaN;return false;}
            if(!down){armed=true;previousDown=false;fired=false;started=double.NaN;return false;}
            if(!armed||fired){previousDown=true;return false;}
            if(!previousDown)started=now;
            previousDown=true;
            if(double.IsNaN(started)||now-started<seconds)return false;
            fired=true;return true;
        }
        internal void Clear(){movie=null;sequence=0;armed=false;previousDown=false;fired=false;started=double.NaN;}
    }
}
