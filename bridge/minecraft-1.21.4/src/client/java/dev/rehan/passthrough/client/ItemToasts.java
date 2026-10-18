package dev.rehan.passthrough.client;

import dev.rehan.passthrough.PickupToasts;
import java.util.ArrayList;
import java.util.List;
import net.fabricmc.fabric.api.client.rendering.v1.HudRenderCallback;
import net.minecraft.client.DeltaTracker;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiGraphics;
import net.minecraft.world.item.ItemStack;

/** Preview106: Wukong-style pickup list on the right side of the screen (icon, name, x count), drawn by MC so every
 *  vanilla and mod item has its real icon. Same item within 2 s merges; up to 6 rows; each row fades after 4 s. */
final class ItemToasts {
    private ItemToasts() {}
    private static final class Row { ItemStack stack; int count; long born; }
    private static final List<Row> rows=new ArrayList<>();
    private static final long LIFE=4000,FADE=600,MERGE=2000;

    static void register(){HudRenderCallback.EVENT.register(ItemToasts::render);}

    private static void render(GuiGraphics g,DeltaTracker tick){
        var mc=Minecraft.getInstance();if(mc.player==null||mc.options.hideGui)return;
        long now=System.currentTimeMillis();
        ItemStack in;
        while((in=PickupToasts.queue.poll())!=null){
            Row same=null;for(var r:rows)if(now-r.born<MERGE&&ItemStack.isSameItemSameComponents(r.stack,in)){same=r;break;}
            if(same!=null){same.count+=in.getCount();same.born=now;}
            else{var r=new Row();r.stack=in.copyWithCount(1);r.count=in.getCount();r.born=now;rows.add(0,r);}
        }
        rows.removeIf(r->now-r.born>LIFE);
        while(rows.size()>6)rows.remove(rows.size()-1);
        if(rows.isEmpty())return;
        int width=g.guiWidth(),height=g.guiHeight(),rowH=20,boxW=150;
        int top=height/2-10;
        for(int i=0;i<rows.size();i++){
            var r=rows.get(i);long age=now-r.born;
            float alpha=age>LIFE-FADE?(LIFE-age)/(float)FADE:1f;
            float slide=Math.min(1f,age/180f);
            int x=width-(int)(boxW*slide)-4,y=top+i*(rowH+2);
            int a=(int)(alpha*170)&0xff;
            g.fillGradient(x,y,x+boxW,y+rowH,(a<<24),((int)(alpha*60)&0xff)<<24);
            g.fill(x,y,x+2,y+rowH,(((int)(alpha*255)&0xff)<<24)|0xC9A44A); // gold edge like Wukong's tips
            g.renderItem(r.stack,x+5,y+2);
            int textAlpha=Math.max(8,(int)(alpha*255))<<24;
            String name=r.stack.getHoverName().getString();
            var font=mc.font;
            String count="x"+r.count;
            int maxName=boxW-30-font.width(count)-6;
            if(font.width(name)>maxName)name=font.plainSubstrByWidth(name,maxName-6)+"…";
            g.drawString(font,name,x+25,y+6,textAlpha|0xF2E6C8,true);
            g.drawString(font,count,x+boxW-font.width(count)-6,y+6,textAlpha|0xFFD966,true);
        }
    }
}
