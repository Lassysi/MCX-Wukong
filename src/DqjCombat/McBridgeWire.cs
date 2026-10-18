using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using LitJson;

namespace DqjCombat
{
    // Loopback transport only. No Unreal calls run on this background thread.
    internal sealed class McBridgeWire : IDisposable
    {
        internal sealed class Frame
        {
            internal long Received, Number, Tick;
            internal double X,Y,Z, EyeX,EyeY,EyeZ, Yaw,Pitch,Roll,Fov,TerrainRayReach;
            internal string Screen;
            internal bool TravelKeyAvailable;
            internal bool GroundReady,GroundCommitted,GroundAnchorResolved;
            internal string GroundEpoch;
            internal string GroundFailure;
            internal BridgePoint GroundSpawn;
            internal BridgePoint TerrainRayDirection;
            internal bool HasGroundSpawn;
            internal string WorldId;
            internal double Health;
            internal double ForwardImpulse,LeftImpulse;
            internal bool Sprinting,SprintKeyDown,OnGround,HorizontalCollision,MinorHorizontalCollision,UsingItem,Crouching,Blindness;
        }
        internal sealed class TerrainRequest
        {
            internal string Epoch;
            internal double X,Y,Z;
            internal bool Deep;
        }
        private TerrainRequest terrainRequest;
        internal TerrainRequest TakeTerrainRequest(){return Interlocked.Exchange(ref terrainRequest,null);}
        private readonly ConcurrentQueue<string> outgoing = new ConcurrentQueue<string>();
        // Live67: CSharpLoader type-checks every mod type at startup; a LitJson-typed generic field
        // forced LitJson to load before the game had loaded it, so the whole mod failed to load.
        // Fields stay LitJson-free; JsonData only appears in method bodies, JIT-compiled later.
        private readonly ConcurrentQueue<object> events = new ConcurrentQueue<object>();
        private readonly ConcurrentQueue<object> travelEvents = new ConcurrentQueue<object>();
        private double[] allyGround;
        internal double[] TakeAllyGround(){return Interlocked.Exchange(ref allyGround,null);}
        internal bool TryEvent(out JsonData value){object item;bool ok=events.TryDequeue(out item);value=item as JsonData;return ok;}
        internal bool TryTravelEvent(out JsonData value){object item;bool ok=travelEvents.TryDequeue(out item);value=item as JsonData;return ok;}
        private volatile bool running = true, connected;
        private string heartbeat;
        private Frame frame;
        private TcpClient client;
        private long lastContact, pongCount;
        internal long PongCount {get{return Interlocked.Read(ref pongCount);}}
        internal bool Connected { get { return connected; } }
        internal Frame Latest { get { return Volatile.Read(ref frame); } }
        internal McBridgeWire()
        {
            var thread=new Thread(Loop);thread.IsBackground=true;thread.Name="DqjMcWire";thread.Start();
        }
        internal void Send(string json) { if(connected && outgoing.Count<256)outgoing.Enqueue(json); }
        internal void Heartbeat(string json) { Interlocked.Exchange(ref heartbeat,json); }
        private void Loop()
        {
            while(running)
            {
                try
                {
                    using(var tcp=new TcpClient())
                    {
                        client=tcp;tcp.NoDelay=true;tcp.ReceiveTimeout=1000;tcp.SendTimeout=1000;
                        var connect=tcp.BeginConnect("127.0.0.1",25599,null,null);
                        if(!connect.AsyncWaitHandle.WaitOne(1500))throw new IOException("MC loopback not ready");
                        tcp.EndConnect(connect);connect.AsyncWaitHandle.Close();
                        var stream=tcp.GetStream();Handshake(stream);
                        lastContact=WindowsClock.Nanoseconds();long nextPing=0;
                        while(running && tcp.Connected)
                        {
                            if(tcp.Available>0)
                            {
                                string message=ReadFrame(stream);
                                if(message!=null)Accept(message);
                            }
                            string data;
                            while(outgoing.TryDequeue(out data))WriteFrame(stream,data);
                            data=Interlocked.Exchange(ref heartbeat,null);if(data!=null)WriteFrame(stream,data);
                            long now=WindowsClock.Nanoseconds();
                            if(connected&&now-lastContact>3000000000L)throw new IOException("MC peer stopped responding to WebSocket heartbeat");
                            if(connected&&now>=nextPing){nextPing=now+500000000L;WriteFrame(stream,new byte[0],9);}
                            Thread.Sleep(3);
                        }
                    }
                }
                catch(Exception e){if(running)Log.Write("bridge-link",e.GetType().Name+": "+e.Message);}
                finally
                {
                    connected=false;Volatile.Write(ref frame,null);client=null;
                    string ignored;while(outgoing.TryDequeue(out ignored)){}
                    object old;while(events.TryDequeue(out old)){}
                    while(travelEvents.TryDequeue(out old)){}
                }
                if(running)Thread.Sleep(1500);
            }
        }
        private void Accept(string text)
        {
            var data=JsonMapper.ToObject(text);string kind=(string)data["t"];
            if(kind=="hello")
            {
                if((string)data["mc"]!="1.21.4")throw new IOException("Expected real Minecraft 1.21.4");
                bool compatible=false;foreach(string key in data.Keys)if(key=="bridge")compatible=Number(data["bridge"])>=16;
                if(!compatible)throw new IOException("MC anchored-block bridge 16 required; restart MC with matching JAR before takeover");
                connected=true;Log.Write("bridge-link","real Minecraft 1.21.4 connected");return;
            }
            if(kind=="travel_request"||kind=="travel_cancel"){
                if(travelEvents.Count>=32)throw new IOException("Travel event queue exceeded capacity");
                travelEvents.Enqueue(data);return;
            }
            if(kind=="health_delta"||kind=="block_cells"||kind=="block_reset"||kind=="target_block"||kind=="mine"||kind=="explosion"||kind=="pearl"||kind=="proxy_fx"||kind=="player_fx"||kind=="player_cure"){
                if(events.Count>=1024)throw new IOException("World event queue exceeded capacity; reconnect for full resync");
                events.Enqueue(data);return;
            }
            if(kind=="ally_ground"){
                // Preview88: positions of spawn-egg allies; the host rasters native ground around them.
                var list=data["p"];var values=new double[Math.Min(list.Count,18)];
                for(int i=0;i<values.Length;i++)values[i]=Number(list[i]);
                Interlocked.Exchange(ref allyGround,values);return;
            }
            if(kind=="terrain_wait"){
                var request=new TerrainRequest{Epoch=(string)data["epoch"],X=Number(data["x"]),Y=Number(data["y"]),Z=Number(data["z"]),Deep=data.Keys.Contains("deep")};
                if(request.Epoch.Length==32&&Finite(request.X)&&Finite(request.Y)&&Finite(request.Z))Interlocked.Exchange(ref terrainRequest,request);
                return;
            }
            if(kind!="mcstate" || !(bool)data["ready"])return;
            var pos=data["pos"];var eye=data["eye"];var rot=data["rot"];
            var value=new Frame {Received=WindowsClock.Nanoseconds(),Number=(long)Number(data["f"]),Tick=(long)Number(data["tick"]),
                X=Number(pos[0]),Y=Number(pos[1]),Z=Number(pos[2]),
                EyeX=Number(eye[0]),EyeY=Number(eye[1]),EyeZ=Number(eye[2]),
                Yaw=Number(rot[0]),Pitch=Number(rot[1]),Roll=Number(rot[2]),Fov=Number(data["fov"]),Screen=(string)data["screen"]};
            foreach(string key in data.Keys)if(key=="hostTravelKeyAvailable")value.TravelKeyAvailable=(bool)data[key];
            foreach(string key in data.Keys){
                if(key=="groundEpoch")value.GroundEpoch=(string)data[key];
                else if(key=="groundFailure")value.GroundFailure=(string)data[key];
                else if(key=="groundSpawn"&&data[key].Count==3){var p=data[key];value.GroundSpawn=new BridgePoint(Number(p[0]),Number(p[1]),Number(p[2]));value.HasGroundSpawn=value.GroundSpawn.Finite;}
                else if(key=="worldId")value.WorldId=(string)data[key];
                else if(key=="groundReady")value.GroundReady=(bool)data[key];
                else if(key=="groundCommitted")value.GroundCommitted=(bool)data[key];
                else if(key=="groundAnchorResolved")value.GroundAnchorResolved=(bool)data[key];
                else if(key=="health")value.Health=Number(data[key]);
                else if(key=="forwardImpulse")value.ForwardImpulse=Number(data[key]);
                else if(key=="leftImpulse")value.LeftImpulse=Number(data[key]);
                else if(key=="sprinting")value.Sprinting=(bool)data[key];
                else if(key=="sprintKeyDown")value.SprintKeyDown=(bool)data[key];
                else if(key=="onGround")value.OnGround=(bool)data[key];
                else if(key=="horizontalCollision")value.HorizontalCollision=(bool)data[key];
                else if(key=="minorHorizontalCollision")value.MinorHorizontalCollision=(bool)data[key];
                else if(key=="usingItem")value.UsingItem=(bool)data[key];
                else if(key=="crouching")value.Crouching=(bool)data[key];
                else if(key=="blindness")value.Blindness=(bool)data[key];
                else if(key=="terrainRayReach")value.TerrainRayReach=Number(data[key]);
                else if(key=="terrainRayDirection"&&data[key].Count==3){var d=data[key];value.TerrainRayDirection=new BridgePoint(Number(d[0]),Number(d[1]),Number(d[2]));}
            }
            if(!Finite(value.X)||!Finite(value.Y)||!Finite(value.Z)||!Finite(value.EyeX)||!Finite(value.EyeY)||!Finite(value.EyeZ)
                ||!Finite(value.Yaw)||!Finite(value.Pitch)||!Finite(value.Roll)||!Finite(value.Fov)||value.Fov<1||value.Fov>179)return;
            Volatile.Write(ref frame,value);
        }
        private static double Number(JsonData data){return double.Parse(data.ToString(),CultureInfo.InvariantCulture);}
        private static bool Finite(double value){return !double.IsNaN(value)&&!double.IsInfinity(value);}
        private static void Handshake(NetworkStream stream)
        {
            var random=new byte[16];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(random);
            string key=Convert.ToBase64String(random);
            byte[] request=Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: 127.0.0.1:25599\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Key: "+key+"\r\n\r\n");
            stream.Write(request,0,request.Length);var header=new StringBuilder();
            while(header.Length<8192){int b=stream.ReadByte();if(b<0)throw new EndOfStreamException();header.Append((char)b);if(header.ToString().EndsWith("\r\n\r\n",StringComparison.Ordinal))break;}
            string accept;using(var sha=SHA1.Create())accept=Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key+"258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            string response=header.ToString();if(!response.StartsWith("HTTP/1.1 101",StringComparison.Ordinal)||response.IndexOf(accept,StringComparison.Ordinal)<0)throw new IOException("Invalid local WebSocket handshake");
        }
        private static byte[] ReadBytes(Stream stream,int count)
        {
            byte[] bytes=new byte[count];int offset=0;
            while(offset<count){int n=stream.Read(bytes,offset,count-offset);if(n<=0)throw new EndOfStreamException();offset+=n;}return bytes;
        }
        private string ReadFrame(NetworkStream stream)
        {
            byte[] header=ReadBytes(stream,2);int op=header[0]&15;
            if((header[0]&128)==0)throw new IOException("Fragmented local WebSocket frame");
            int count=header[1]&127;
            if(count==126){var size=ReadBytes(stream,2);count=(size[0]<<8)|size[1];}
            else if(count==127)throw new IOException("Oversized local WebSocket frame");
            if((header[1]&128)!=0)throw new IOException("Unexpected masked server frame");
            var payload=ReadBytes(stream,count);
            if(op==8)throw new EndOfStreamException();
            if(op==1||op==9||op==10)lastContact=WindowsClock.Nanoseconds();
            if(op==9){WriteFrame(stream,payload,10);return null;}
            if(op==10){Interlocked.Increment(ref pongCount);return null;}
            return op==1?Encoding.UTF8.GetString(payload):null;
        }
        private static void WriteFrame(NetworkStream stream,string data){WriteFrame(stream,Encoding.UTF8.GetBytes(data),1);}
        private static void WriteFrame(NetworkStream stream,byte[] data,int opcode)
        {
            if(data.Length>65535)throw new IOException("Oversized bridge command");
            var mask=new byte[4];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(mask);
            int prefix=data.Length<126?2:4;byte[] packet=new byte[prefix+4+data.Length];packet[0]=(byte)(128|opcode);
            if(prefix==2)packet[1]=(byte)(128|data.Length);else{packet[1]=254;packet[2]=(byte)(data.Length>>8);packet[3]=(byte)data.Length;}
            Buffer.BlockCopy(mask,0,packet,prefix,4);
            for(int i=0;i<data.Length;i++)packet[prefix+4+i]=(byte)(data[i]^mask[i&3]);stream.Write(packet,0,packet.Length);
        }
        public void Dispose(){running=false;connected=false;try{if(client!=null)client.Close();}catch{} }
    }
}
