package dev.rehan.passthrough;

import java.util.concurrent.ConcurrentLinkedQueue;
import net.minecraft.world.item.ItemStack;

/** Preview106 (user: show MC items like Wukong's right-side pickup message, with icon, name and count).
 *  The server side pushes what the player received (emeralds from Will, picked-up item entities); the client
 *  (ItemToasts) draws them on the right with MC's own item renderer, so vanilla and mod items need no extracted art. */
public final class PickupToasts {
    private PickupToasts() {}
    public static final ConcurrentLinkedQueue<ItemStack> queue=new ConcurrentLinkedQueue<>();
    public static void push(ItemStack stack,int count){
        if(stack==null||stack.isEmpty()||count<=0||queue.size()>64)return;
        queue.add(stack.copyWithCount(count));
    }
}
