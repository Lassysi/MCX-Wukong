package dev.rehan.passthrough.client;

import com.google.gson.JsonObject;
import dev.rehan.passthrough.Passthrough;
import dev.rehan.passthrough.client.mixin.KeyMappingAccessor;
import net.minecraft.client.KeyMapping;
import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.screens.ChatScreen;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.client.player.LocalPlayer;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.world.entity.player.Inventory;
import org.lwjgl.glfw.GLFW;
import dev.rehan.passthrough.client.mixin.MouseHandlerAccess;

/** Host input, applied on the client thread: the host window has the focus, so Minecraft never sees these itself. */
final class ClientInput {
	private ClientInput() {
	}

	static void handle(final Minecraft minecraft, final JsonObject m) {
		LocalPlayer player = minecraft.player;
		switch (m.get("t").getAsString()) {
            case "input_reset" -> NativeControl.releaseInput();
            case "travel_open", "travel_entries", "travel_result" -> HostTravelScreen.handle(m);
            case "travel_notice" -> {if(player!=null)player.displayClientMessage(net.minecraft.network.chat.Component.literal(m.get("text").getAsString()),true);}
			case "key" -> {
				String k = m.get("k").getAsString();
				boolean down = !m.has("down") || m.get("down").getAsBoolean();
                // Preview47: T/slash open vanilla chat; while it is open, game keys
                // (E, WASD...) must not close it or move Steve; text arrives as "char".
                if(minecraft.screen instanceof ChatScreen&&!k.equals("escape"))return;
                if(k.equals("chat")||k.equals("command")){
                    // Preview48 diagnostics (BUG-2026-10-16-01): why chat did or did not open.
                    if(down)Passthrough.LOG.info("DQJ input-diag {} screen={} player={} authority={}",k,
                        minecraft.screen==null?"none":minecraft.screen.getClass().getSimpleName(),player!=null,Passthrough.minecraftAuthority);
                    if(down&&minecraft.screen==null&&player!=null){
                        minecraft.setScreen(new ChatScreen(k.equals("command")?"/":""));
                        Passthrough.LOG.info("DQJ input-diag {} opened ChatScreen",k);
                    }
                    return;
                }
                // Preview105: at an activated shrine, right-click with an empty hand or emeralds opens the shrine trader.
                if(k.equals("use")&&down&&minecraft.screen==null&&player!=null&&dev.rehan.passthrough.ShrineShop.nearShrine){
                    var held=player.getMainHandItem();
                    // Live111: weapon in the main hand + enchanted book in the off hand -> free shrine enchanting.
                    if(!held.isEmpty()&&player.getOffhandItem().is(net.minecraft.world.item.Items.ENCHANTED_BOOK)){dev.rehan.passthrough.ShrineShop.enchant();return;}
                    if(held.isEmpty()||held.is(net.minecraft.world.item.Items.EMERALD)){dev.rehan.passthrough.ShrineShop.open();return;}
                }
                if(k.equals("attack")&&minecraft.screen==null)InputDiagnostics.attack(minecraft,down);
                if(k.equals("sneak"))NativeControl.shiftHeld=down;
                if(k.equals("sprint")){NativeControl.sprintKey(down);return;}
                if(k.equals("forward")&&!down)NativeControl.forwardReleased();
                if(minecraft.screen!=null && (k.equals("attack")||k.equals("use")||k.equals("pick"))){
                    NativeControl.button(
                        k.equals("attack")?GLFW.GLFW_MOUSE_BUTTON_LEFT:k.equals("use")?GLFW.GLFW_MOUSE_BUTTON_RIGHT:GLFW.GLFW_MOUSE_BUTTON_MIDDLE,
                        down);return;
                }
                if(minecraft.screen!=null && k.equals("inventory") && down){minecraft.screen.onClose();return;}
                if((k.equals("hotbarPrevious")||k.equals("hotbarNext")) && down && player!=null){
                    var inventory=player.getInventory();inventory.selected=Math.floorMod(inventory.selected+(k.equals("hotbarNext")?1:-1),9);return;
                }
				if (k.equals("escape")) {
					if (down && minecraft.screen != null) {
						minecraft.screen.onClose();
					}

					return;
				}

				KeyMapping key = switch (k) {
					case "use" -> minecraft.options.keyUse;
					case "attack" -> minecraft.options.keyAttack;
					case "pick" -> minecraft.options.keyPickItem;
					case "inventory" -> minecraft.options.keyInventory;
					case "drop" -> minecraft.options.keyDrop;
					case "swap" -> minecraft.options.keySwapOffhand;
					case "forward" -> minecraft.options.keyUp;
					case "back" -> minecraft.options.keyDown;
					case "left" -> minecraft.options.keyLeft;
					case "right" -> minecraft.options.keyRight;
					case "jump" -> minecraft.options.keyJump;
					case "sneak" -> minecraft.options.keyShift;
					case "sprint" -> minecraft.options.keySprint;
                    case "perspective" -> minecraft.options.keyTogglePerspective;
					default -> null;
				};
				if (k.equals("attack") && down && player != null
					&& BuiltInRegistries.ITEM.getKey(player.getMainHandItem().getItem()).getPath().endsWith("_sword")) {
					// a sword swing: the host hits what's in front of Steve in its own world
					Passthrough.events.accept("{\"t\":\"melee\"}");
				}

				if (key != null) {
					if (down && !key.isDown()) {
						KeyMappingAccessor access = (KeyMappingAccessor)key;
						access.passthrough$setClickCount(access.passthrough$getClickCount() + 1);
					}

					key.setDown(down);
				}
			}
			case "char" -> {
                Screen screen=minecraft.screen;int c=m.get("c").getAsInt();
                if(screen!=null&&c>=32&&c<=0xffff&&c!=0x7f)screen.charTyped((char)c,0);
            }
			case "screen_key" -> {
                Screen screen=minecraft.screen;int key=m.get("k").getAsInt();
                // Text-editing keys only: enter, tab, backspace, delete, arrows, page, home/end.
                if(screen instanceof ChatScreen chat&&key==GLFW.GLFW_KEY_ENTER&&m.get("down").getAsBoolean())
                    Passthrough.LOG.info("DQJ input-diag enter text='{}'",((dev.rehan.passthrough.client.mixin.ChatScreenAccess)chat).dqj$input().getValue());
                if(screen!=null&&(key>=GLFW.GLFW_KEY_ENTER&&key<=GLFW.GLFW_KEY_BACKSPACE||key>=GLFW.GLFW_KEY_DELETE&&key<=GLFW.GLFW_KEY_END)){
                    if(m.get("down").getAsBoolean())screen.keyPressed(key,0,0);else screen.keyReleased(key,0,0);
                }
            }
			case "look" -> NativeControl.look(m.get("dx").getAsDouble(), m.get("dy").getAsDouble());
            case "pointer" -> {
                NativeControl.pointer(m.get("x").getAsDouble(),m.get("y").getAsDouble());
            }
			case "slot" -> {
				if (player != null) {
					player.getInventory().selected = Math.clamp(m.get("n").getAsInt(), 0, 8);
				}
			}
			case "scroll" -> {
				NativeControl.scroll(m.get("d").getAsDouble());
			}
			case "hud" -> {
				minecraft.options.hideGui = m.get("hidden").getAsBoolean();
			}
			case "view" -> {
				BridgeWindow.resize(m.get("w").getAsInt(),m.get("h").getAsInt());
			}
			default -> {
			}
		}
	}
}
