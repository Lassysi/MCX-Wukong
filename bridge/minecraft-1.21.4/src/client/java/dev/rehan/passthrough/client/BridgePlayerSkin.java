package dev.rehan.passthrough.client;

import net.minecraft.client.resources.PlayerSkin;
import net.minecraft.resources.ResourceLocation;

/** One bundled texture for the MC player and the host's baked steve.mesh. No account upload required. */
public final class BridgePlayerSkin {
    public static final ResourceLocation TEXTURE=ResourceLocation.fromNamespaceAndPath("dqj","textures/entity/dasheng.png");
    private BridgePlayerSkin(){}
    public static PlayerSkin apply(PlayerSkin original){
        return new PlayerSkin(TEXTURE,null,original.capeTexture(),original.elytraTexture(),PlayerSkin.Model.WIDE,false);
    }
}
