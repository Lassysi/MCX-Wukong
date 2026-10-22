package dev.rehan.passthrough;

import java.util.Map;
import net.minecraft.core.component.DataComponents;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.entity.EquipmentSlot;
import net.minecraft.world.entity.ai.attributes.Attributes;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.item.component.ItemAttributeModifiers;

/** Bridge-only damage curve. Does not rewrite saved items, vanilla attributes or enchantments. */
public final class EquipmentBalance {
    private EquipmentBalance() {}
    private static final String[] MATERIALS={"wooden","stone","golden","iron","diamond","netherite"};
    private static final double[] SWORDS={6,7.5,9,11,13,15},AXES={8,10,12,14,16,19};
    private static final double[] SWORD_BASE={4,5,4,6,7,8},AXE_BASE={7,9,7,9,9,10};
    private static final Map<String,Double> ARMOR=Map.of("leather",.25,"golden",.35,"chainmail",.45,"iron",.55,"diamond",.70,"netherite",.80,"turtle",.55);

    public static double meleeScale(ItemStack stack){
        var id=BuiltInRegistries.ITEM.getKey(stack.getItem());
        if(!id.getNamespace().equals("minecraft"))return 1.35;
        String path=id.getPath();
        for(int i=0;i<MATERIALS.length;i++)if(path.startsWith(MATERIALS[i]+"_")){
            if(path.endsWith("_sword"))return SWORDS[i]/SWORD_BASE[i];
            if(path.endsWith("_axe"))return AXES[i]/AXE_BASE[i];
            if(path.endsWith("_pickaxe"))return (3+i)/(double)(new int[]{2,3,2,4,5,6})[i];
            if(path.endsWith("_shovel"))return (3.5+i)/(new double[]{2.5,3.5,2.5,4.5,5.5,6.5})[i];
            if(path.endsWith("_hoe"))return 2+i;
        }
        if(stack.is(net.minecraft.world.item.Items.TRIDENT))return 1.5;
        if(stack.is(net.minecraft.world.item.Items.MACE))return 1.4;
        // Empty hand and non-weapons keep their native damage.
        return 1;
    }

    static double armor(ServerPlayer p,double perPoint,double perToughness,double max){
        // Read the equipped stacks as one snapshot. Entity attribute caches update later in the tick,
        // which otherwise produces a transient wrong reduction immediately after changing equipment.
        double result=0;
        for(var slot:new EquipmentSlot[]{EquipmentSlot.HEAD,EquipmentSlot.CHEST,EquipmentSlot.LEGS,EquipmentSlot.FEET}){
            var stack=p.getItemBySlot(slot);if(stack.isEmpty())continue;
            var id=BuiltInRegistries.ITEM.getKey(stack.getItem());
            String name=id.getPath();Double full=null;
            String suffix=switch(slot){case HEAD->"helmet";case CHEST->"chestplate";case LEGS->"leggings";default->"boots";};
            if(id.getNamespace().equals("minecraft"))for(var e:ARMOR.entrySet())if(name.equals(e.getKey()+"_"+suffix)){full=e.getValue();break;}
            double[] vanilla={0};
            stack.getOrDefault(DataComponents.ATTRIBUTE_MODIFIERS,ItemAttributeModifiers.EMPTY).forEach(slot,(attribute,modifier)->{
                if(attribute.equals(Attributes.ARMOR))vanilla[0]+=modifier.amount()*perPoint;
                if(attribute.equals(Attributes.ARMOR_TOUGHNESS))vanilla[0]+=modifier.amount()*perToughness;
            });
            double weight=switch(slot){case HEAD,FEET->.15;case CHEST->.4;default->.3;};
            result+=full==null?vanilla[0]:full*weight;
        }
        return Math.max(0,Math.min(max,result));
    }
}
