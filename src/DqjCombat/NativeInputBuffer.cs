using System.Collections.Generic;

namespace DqjCombat
{
    // Free-pointer motion may coalesce. Held dragging must visit every received
    // slot position for native MC quickcraft; preserve press/move/release order.
    internal sealed class NativeInputBuffer
    {
        internal struct Input
        {
            internal string Name;
            internal bool Down;
            internal int X,Y;
        }
        private readonly List<Input> pending=new List<Input>();
        private int heldButtons;
        internal bool Overflow {get;private set;}
        internal void Add(string name,bool down,int x,int y)
        {
            lock(pending){
                if(name==null&&heldButtons==0&&pending.Count>0&&pending[pending.Count-1].Name==null){
                    pending[pending.Count-1]=new Input{Name=null,X=x,Y=y};return;
                }
                if(pending.Count>=1024){Overflow=true;return;}
                int button=name=="LeftMouseButton"?1:name=="RightMouseButton"?2:name=="MiddleMouseButton"?4:0;
                if(button!=0){if(down)heldButtons|=button;else heldButtons&=~button;}
                pending.Add(new Input{Name=name,Down=down,X=x,Y=y});
            }
        }
        internal List<Input> Take()
        {
            lock(pending){var result=new List<Input>(pending);pending.Clear();return result;}
        }
        internal void Clear(){lock(pending){pending.Clear();Overflow=false;heldButtons=0;}}
    }
}
