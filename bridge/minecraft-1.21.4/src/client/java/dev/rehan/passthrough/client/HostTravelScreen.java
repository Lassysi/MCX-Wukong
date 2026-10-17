package dev.rehan.passthrough.client;

import com.google.gson.JsonObject;
import dev.rehan.passthrough.Passthrough;
import java.util.ArrayList;
import java.util.List;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.client.gui.components.Button;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;

/** Native MC widgets; only destination requests cross to the host's loading FSM. */
public final class HostTravelScreen extends Screen {
    private record Destination(int id,String name,String area) {}
    private final String token,origin;
    private final int expected;
    private final List<Destination> destinations=new ArrayList<>();
    private int page;
    private Destination selected;
    private boolean ready,waiting,committed;
    private String status="正在读取已解锁的传送点…";

    private HostTravelScreen(JsonObject message) {
        super(Component.literal("土地庙传送"));
        token=message.get("token").getAsString();origin=message.get("origin").getAsString();expected=message.get("count").getAsInt();
        if(token.length()>128||expected<0||expected>512)throw new IllegalArgumentException("Invalid host travel menu");
    }
    static void handle(JsonObject message) {
        var mc=Minecraft.getInstance();String type=message.get("t").getAsString();
        if(type.equals("travel_open")){mc.setScreen(new HostTravelScreen(message));return;}
        if(!(mc.screen instanceof HostTravelScreen screen)||!screen.token.equals(message.get("token").getAsString()))return;
        if(type.equals("travel_entries")){
            for(var entry:message.getAsJsonArray("entries")){
                var row=entry.getAsJsonObject();
                if(screen.destinations.size()>=screen.expected)throw new IllegalArgumentException("Extra travel destinations");
                screen.destinations.add(new Destination(row.get("id").getAsInt(),row.get("name").getAsString(),row.get("area").getAsString()));
            }
            screen.ready=message.get("done").getAsBoolean()&&screen.destinations.size()==screen.expected;
            screen.status=screen.ready?(screen.destinations.isEmpty()?"当前没有可传送的地点":"仅显示黑神话已解锁的土地庙"):"正在读取已解锁的传送点…";
            screen.rebuildWidgets();
        }else if(type.equals("travel_result")){
            if(message.get("accepted").getAsBoolean()){screen.committed=true;mc.setScreen(null);}
            else{screen.waiting=false;screen.status=message.get("error").getAsString();screen.rebuildWidgets();}
        }
    }
    @Override protected void init() {
        int w=Math.min(360,width-32),x=(width-w)/2,y=58;
        int rows=rows();
        if(selected!=null){
            addRenderableWidget(Button.builder(Component.literal("传送至："+selected.name),button->request()).bounds(x,y,w,20).build()).active=!waiting;
            addRenderableWidget(Button.builder(Component.literal("返回地点列表"),button->{selected=null;rebuildWidgets();}).bounds(x,y+28,w,20).build()).active=!waiting;
        }else{
            int first=page*rows;
            for(int i=first;i<Math.min(first+rows,destinations.size());i++){
                Destination destination=destinations.get(i);
                String label=destination.area.isEmpty()?destination.name:destination.area+" · "+destination.name;
                addRenderableWidget(Button.builder(Component.literal(label),button->{selected=destination;status="选择下方传送按钮确认目的地";rebuildWidgets();})
                    .bounds(x,y+(i-first)*24,w,20).build()).active=ready;
            }
            int pages=Math.max(1,(destinations.size()+rows-1)/rows),buttonsY=height-58;
            addRenderableWidget(Button.builder(Component.literal("上一页"),button->{page--;rebuildWidgets();}).bounds(x,buttonsY,(w-8)/2,20).build()).active=page>0&&ready;
            addRenderableWidget(Button.builder(Component.literal("下一页"),button->{page++;rebuildWidgets();}).bounds(x+(w+8)/2,buttonsY,(w-8)/2,20).build()).active=page+1<pages&&ready;
        }
        addRenderableWidget(Button.builder(Component.literal("关闭"),button->onClose()).bounds(x,height-30,w,20).build());
    }
    private int rows(){return Math.max(1,Math.min(8,(height-122)/24));}
    private void request(){
        if(waiting||selected==null||!ready)return;
        waiting=true;status="正在请求传送…";
        JsonObject message=new JsonObject();message.addProperty("t","travel_request");message.addProperty("token",token);message.addProperty("id",selected.id);
        Passthrough.events.accept(message.toString());rebuildWidgets();
    }
    @Override public void onClose(){
        if(!committed){JsonObject message=new JsonObject();message.addProperty("t","travel_cancel");message.addProperty("token",token);Passthrough.events.accept(message.toString());}
        super.onClose();
    }
    @Override public boolean isPauseScreen(){return false;}
    @Override public void render(GuiGraphics graphics,int mouseX,int mouseY,float partialTick){
        graphics.fill(0,0,width,height,0xB8000000);
        graphics.drawCenteredString(font,title,width/2,12,0xFFFFFF);
        graphics.drawCenteredString(font,Component.literal(origin),width/2,27,0xBBBBBB);
        graphics.drawCenteredString(font,Component.literal(status),width/2,42,0xBBBBBB);
        super.render(graphics,mouseX,mouseY,partialTick);
    }
    JsonObject diagnostics(){
        JsonObject out=new JsonObject();out.addProperty("ready",ready);out.addProperty("count",destinations.size());out.addProperty("page",page);
        out.addProperty("selected",selected==null?-1:selected.id);out.addProperty("waiting",waiting);out.addProperty("status",status);
        out.addProperty("guiWidth",width);out.addProperty("guiHeight",height);
        var buttons=new com.google.gson.JsonArray();for(var child:children())if(child instanceof Button button){
            var row=new JsonObject();row.addProperty("text",button.getMessage().getString());row.addProperty("x",button.getX());row.addProperty("y",button.getY());
            row.addProperty("w",button.getWidth());row.addProperty("h",button.getHeight());row.addProperty("active",button.active);buttons.add(row);
        }out.add("buttons",buttons);return out;
    }
}
