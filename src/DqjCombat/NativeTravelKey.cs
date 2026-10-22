using System;
using System.Runtime.InteropServices;

namespace DqjCombat
{
    // Observe input only on this game's UI/game thread. A short press/release
    // or GUI drag must not disappear between host ticks.
    internal sealed class NativeTravelKey : IDisposable
    {
        private delegate IntPtr Hook(int code,IntPtr remove,IntPtr message);
        [DllImport("user32.dll",SetLastError=true)] private static extern IntPtr SetWindowsHookExW(int kind,Hook callback,IntPtr module,uint thread);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr remove,IntPtr message);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [StructLayout(LayoutKind.Sequential)] private struct ClientRect {internal int Left,Top,Right,Bottom;}
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window,out ClientRect rectangle);
        [DllImport("user32.dll")] private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        private readonly Action pressed;
        private readonly Action nativeModePressed;
        private readonly Action<int> scrolled;
        private readonly Action<bool> sprintChanged;
        private readonly Action<string,bool,int,int> controlChanged;
        private readonly Action<int,int> pointerChanged;
        private Hook callback;
        private GCHandle root;
        private IntPtr handle;
        private bool disposed;
        private IntPtr inputWindow;
        internal bool Attached {get{return handle!=IntPtr.Zero&&!disposed;}}
        internal bool HasFocus {get{return inputWindow==IntPtr.Zero||inputWindow==GetForegroundWindow();}}
        internal bool TryGetPointerClientSize(out int width,out int height)
        {
            width=height=0;if(inputWindow==IntPtr.Zero||disposed)return false;
            IntPtr previous=IntPtr.Zero;
            try{
                // Match the source window's message-coordinate space, even if
                // the caller thread temporarily has a different DPI context.
                var context=GetWindowDpiAwarenessContext(inputWindow);
                if(context!=IntPtr.Zero)previous=SetThreadDpiAwarenessContext(context);
                ClientRect rectangle;if(!GetClientRect(inputWindow,out rectangle))return false;
                width=rectangle.Right-rectangle.Left;height=rectangle.Bottom-rectangle.Top;
                return width>0&&height>0;
            }catch{return false;}
            finally{if(previous!=IntPtr.Zero)try{SetThreadDpiAwarenessContext(previous);}catch{}}
        }
        internal NativeTravelKey(Action pressed,Action<int> scrolled=null,Action<bool> sprintChanged=null,
            Action<string,bool,int,int> controlChanged=null,Action<int,int> pointerChanged=null,Action nativeModePressed=null)
        {
            this.pressed=pressed;
            this.nativeModePressed=nativeModePressed;
            this.scrolled=scrolled;
            this.sprintChanged=sprintChanged;
            this.controlChanged=controlChanged;this.pointerChanged=pointerChanged;
            try{
                callback=OnMessage;root=GCHandle.Alloc(callback);
                handle=SetWindowsHookExW(3,callback,IntPtr.Zero,GetCurrentThreadId());
                if(handle==IntPtr.Zero){root.Free();Log.Write("travel-key","message hook unavailable; error="+Marshal.GetLastWin32Error());}
                else Log.Write("travel-key","F6/keyboard/mouse message hook attached to game thread="+GetCurrentThreadId());
            }catch(Exception e){if(root.IsAllocated)root.Free();Log.Write("travel-key","message hook: "+e.Message);}
        }
        private IntPtr OnMessage(int code,IntPtr remove,IntPtr message)
        {
            try{
                if(!disposed&&code>=0&&remove==new IntPtr(1)&&message!=IntPtr.Zero){
                    int id=Marshal.ReadInt32(message,IntPtr.Size);
                    long key=IntPtr.Size==8?Marshal.ReadInt64(message,16):Marshal.ReadInt32(message,8);
                    long flags=IntPtr.Size==8?Marshal.ReadInt64(message,24):Marshal.ReadInt32(message,12);
                    bool keyboard=id==0x100||id==0x104||id==0x101||id==0x105;
                    bool mouse=id>=0x200&&id<=0x20e;
                    if(keyboard||mouse)inputWindow=Marshal.ReadIntPtr(message,0);
                    if((id==0x100||id==0x104)&&key==0x75&&(flags&(1L<<30))==0){Log.Write("travel-key","F6 window message queued");pressed();}
                    if((id==0x100||id==0x104)&&key==0x77&&(flags&(1L<<30))==0&&HasFocus&&nativeModePressed!=null){Log.Write("control-mode","F8 toggle queued");nativeModePressed();}
                    if(id==0x20a&&scrolled!=null){int delta=unchecked((short)((key>>16)&0xffff));if(delta!=0)scrolled(delta);}
                    if(sprintChanged!=null&&(key==0x11||key==0xa2)&&(flags&(1L<<24))==0){
                        if((id==0x100||id==0x104)&&(flags&(1L<<30))==0)sprintChanged(true);
                        else if(id==0x101||id==0x105)sprintChanged(false);
                    }
                    if(keyboard&&controlChanged!=null){
                        string name=ControlName((int)key,flags);
                        bool press=id==0x100||id==0x104;
                        if(name!=null&&(!press||(flags&(1L<<30))==0))controlChanged(name,press,-1,-1);
                        // Preview47 text editing for MC chat/command screens. Repeats are
                        // kept (holding Backspace); the host forwards them only while an
                        // MC screen is open.
                        int edit=EditKey((int)key);
                        if(edit!=0)controlChanged("ScreenKey:"+edit,press,-1,-1);
                    }
                    // WM_CHAR after TranslateMessage: typed text in the user's layout/IME.
                    if(id==0x102&&controlChanged!=null&&key>=0x20&&key!=0x7f&&key<=0xffff)controlChanged("Char:"+key,true,-1,-1);
                    int x=unchecked((short)(flags&0xffff)),y=unchecked((short)((flags>>16)&0xffff));
                    if(id==0x200&&pointerChanged!=null)pointerChanged(x,y);
                    if(controlChanged!=null){
                        if(id==0x201||id==0x203||id==0x202)controlChanged("LeftMouseButton",id!=0x202,x,y);
                        if(id==0x204||id==0x206||id==0x205)controlChanged("RightMouseButton",id!=0x205,x,y);
                        if(id==0x207||id==0x209||id==0x208)controlChanged("MiddleMouseButton",id!=0x208,x,y);
                    }
                }
            }catch(Exception e){Log.Write("travel-key","message callback: "+e.Message);}
            return CallNextHookEx(handle,code,remove,message);
        }
        // Windows virtual key -> GLFW key for MC text fields; 0 if not an edit key.
        private static int EditKey(int key)
        {
            switch(key){
                case 0x0d:return 257;case 0x09:return 258;case 0x08:return 259;case 0x2e:return 261;
                case 0x27:return 262;case 0x25:return 263;case 0x28:return 264;case 0x26:return 265;
                case 0x21:return 266;case 0x22:return 267;case 0x24:return 268;case 0x23:return 269;
                default:return 0;
            }
        }
        private static string ControlName(int key,long flags)
        {
            switch(key){
                case 0x57:return "W";case 0x53:return "S";case 0x41:return "A";case 0x44:return "D";
                case 0x20:return "SpaceBar";case 0x45:return "E";case 0x51:return "Q";case 0x46:return "F";
                case 0x1b:return "Escape";case 0x74:return "F5";
                case 0x54:return "T";case 0xbf:return "Slash";
                case 0x10:return ((flags>>16)&0xff)==0x36?null:"LeftShift";
                case 0xa0:return "LeftShift";
                case 0x11:return (flags&(1L<<24))==0?"LeftControl":null;
                case 0xa2:return "LeftControl";
                case 0x31:return "One";case 0x32:return "Two";case 0x33:return "Three";
                case 0x34:return "Four";case 0x35:return "Five";case 0x36:return "Six";
                case 0x37:return "Seven";case 0x38:return "Eight";case 0x39:return "Nine";
                default:return null;
            }
        }
        public void Dispose()
        {
            disposed=true;
            if(handle!=IntPtr.Zero){
                if(!UnhookWindowsHookEx(handle)){Log.Write("travel-key","unhook failed; retain callback until process exit");return;}
                handle=IntPtr.Zero;
            }
            if(root.IsAllocated)root.Free();
        }
    }
}
